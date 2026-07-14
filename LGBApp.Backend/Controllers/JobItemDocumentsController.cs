using LGBApp.Backend.Data;
using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGBApp.Backend.Controllers;

[Route("api/jobs/{jobId:int}/documents")]
[ApiController]
[Authorize]
public class JobItemDocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;

    public JobItemDocumentsController(AppDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    [HttpGet("folders")]
    public async Task<ActionResult<JobItemFoldersResponse>> GetFolders(
        int jobId,
        [FromQuery] int? unitNumber)
    {
        var job = await LoadJobAsync(jobId);
        if (job == null) return NotFound();
        if (!await CanAccessJobAsync(job))
            return Forbid();

        JobRequestUnit? unit = null;
        if (unitNumber.HasValue)
            unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);

        var moiQuery = _context.MOIForms.Where(f => f.JobRequestId == jobId);
        var moaQuery = _context.MOAForms.Where(f => f.JobRequestId == jobId);
        if (unit != null)
        {
            moiQuery = moiQuery.Where(f => f.JobRequestUnitId == unit.JobRequestUnitId);
            moaQuery = moaQuery.Where(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        }

        var moi = await moiQuery.FirstOrDefaultAsync();
        var moa = await moaQuery.FirstOrDefaultAsync();
        var docs = await QueryVisibleDocuments(job, moi, unit)
            .OrderByDescending(d => d.UploadedAt)
            .ToListAsync();

        var grouped = new[] { "moi", "moa", "supporting" }
            .Select(folder => new JobItemFolderDto
            {
                Folder = folder,
                Documents = docs
                    .Where(d => d.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
                    .Select(ToDto)
                    .ToList(),
            })
            .ToList();

        return new JobItemFoldersResponse
        {
            JobId = jobId,
            Service = job.Service,
            MoiFormId = moi?.MOIFormId,
            MoaFormId = moa?.MOAFormId,
            MoiWorkflowState = moi?.WorkflowState,
            Folders = grouped,
        };
    }

    [HttpPost]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<JobItemDocumentDto>> Upload(
        int jobId,
        [FromQuery] string folder,
        [FromQuery] int? unitNumber,
        IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "File is required." });

        if (!UploadFilePolicy.TryResolve(file.FileName, out _, out var contentType, out var uploadError))
            return BadRequest(new { message = uploadError });

        var job = await LoadJobAsync(jobId);
        if (job == null) return NotFound();
        if (!await CanUploadAsync(job))
            return Forbid();

        if (job.TotalQty > 1 && !unitNumber.HasValue)
            return BadRequest(new { message = "unitNumber is required for multi-session items." });

        var moi = await ResolveMoiForJobAsync(job, unitNumber);
        var normalizedFolder = NormalizeFolder(folder);
        var userId = AuthHelper.CurrentUserId(User) ?? 0;
        var user = await _context.Users.FindAsync(userId);
        var safeFileName = Path.GetFileName(file.FileName);
        var storageKey = JobItemDocumentStorage.BuildStorageKey(jobId, normalizedFolder, safeFileName);

        await JobItemDocumentStorage.SaveAsync(_env, storageKey, file.OpenReadStream());

        JobRequestUnit? unit = null;
        if (unitNumber.HasValue)
            unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);

        if (unit == null && job.TotalQty > 1)
            return BadRequest(new { message = "Session not found for this item." });

        var doc = new JobItemDocument
        {
            JobRequestId = jobId,
            JobRequestUnitId = unit?.JobRequestUnitId,
            Folder = normalizedFolder,
            FileName = safeFileName,
            StorageKey = storageKey.Replace('\\', '/'),
            ContentType = contentType,
            UploadedByUserId = userId,
            UploadedByName = user?.Name ?? string.Empty,
            UploadedAt = DateTime.UtcNow,
            VisibleToInternal = !MoiVisibilityHelper.IsClientOnlyPhase(moi, job),
        };

        _context.JobItemDocuments.Add(doc);
        await _context.SaveChangesAsync();
        return ToDto(doc);
    }

    [HttpGet("{documentId:int}/download")]
    public async Task<IActionResult> Download(int jobId, int documentId)
    {
        var job = await LoadJobAsync(jobId);
        if (job == null) return NotFound();

        var doc = await _context.JobItemDocuments.FirstOrDefaultAsync(d =>
            d.JobItemDocumentId == documentId && d.JobRequestId == jobId);
        if (doc == null) return NotFound();
        if (!await CanAccessDocumentAsync(job, doc))
            return Forbid();

        var path = JobItemDocumentStorage.FullPath(_env, doc.StorageKey);
        if (!System.IO.File.Exists(path))
            return NotFound("File not found on disk.");

        return PhysicalFile(path, doc.ContentType, doc.FileName);
    }

    [HttpDelete("{documentId:int}")]
    public async Task<IActionResult> Delete(int jobId, int documentId)
    {
        var job = await LoadJobAsync(jobId);
        if (job == null) return NotFound();

        var doc = await _context.JobItemDocuments.FirstOrDefaultAsync(d =>
            d.JobItemDocumentId == documentId && d.JobRequestId == jobId);
        if (doc == null) return NotFound();
        if (!await CanUploadAsync(job))
            return Forbid();

        JobItemDocumentStorage.DeleteFile(_env, doc.StorageKey);
        _context.JobItemDocuments.Remove(doc);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<JobRequest?> LoadJobAsync(int jobId) =>
        await _context.JobRequests
            .Include(j => j.Units).ThenInclude(u => u.Assignees)
            .FirstOrDefaultAsync(j => j.JobRequestId == jobId);

    private async Task<MOIForm?> ResolveMoiForJobAsync(JobRequest job, int? unitNumber)
    {
        var query = _context.MOIForms.Where(f => f.JobRequestId == job.JobRequestId);
        if (unitNumber.HasValue)
        {
            var unit = job.Units.FirstOrDefault(u => u.UnitNumber == unitNumber.Value);
            if (unit != null)
                return await query.FirstOrDefaultAsync(f => f.JobRequestUnitId == unit.JobRequestUnitId);
        }

        if (job.TotalQty <= 1)
            return await query.FirstOrDefaultAsync(f => f.JobRequestUnitId == null)
                ?? await query.FirstOrDefaultAsync();

        return null;
    }

    private IQueryable<JobItemDocument> QueryVisibleDocuments(
        JobRequest job,
        MOIForm? moi,
        JobRequestUnit? unit = null)
    {
        var query = _context.JobItemDocuments.Where(d => d.JobRequestId == job.JobRequestId);
        if (unit != null)
        {
            query = job.TotalQty > 1
                ? query.Where(d => d.JobRequestUnitId == unit.JobRequestUnitId)
                : query.Where(d =>
                    d.JobRequestUnitId == unit.JobRequestUnitId || d.JobRequestUnitId == null);
        }

        if (AuthHelper.IsExternalUser(User))
            return query;

        if (MoiVisibilityHelper.HasClientReleasedToInternal(moi, job))
            return query;

        return query.Where(d => d.VisibleToInternal);
    }

    private async Task<bool> CanAccessJobAsync(JobRequest job)
    {
        if (AuthHelper.IsAdmin(User))
            return true;

        if (AuthHelper.IsExternalUser(User))
            return AuthHelper.CanAccessCustomer(User, job.CustomerId);

        if (!AuthHelper.IsInternalStaff(User))
            return false;

        var moi = await _context.MOIForms.FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId);
        if (MoiVisibilityHelper.IsClientOnlyPhase(moi, job))
            return false;

        return AuthHelper.CanAccessJob(User, job);
    }

    private async Task<bool> CanAccessDocumentAsync(JobRequest job, JobItemDocument doc)
    {
        if (!await CanAccessJobAsync(job))
            return false;

        if (AuthHelper.IsExternalUser(User))
            return true;

        return doc.VisibleToInternal || MoiVisibilityHelper.HasClientReleasedToInternal(
            await _context.MOIForms.FirstOrDefaultAsync(f => f.JobRequestId == job.JobRequestId),
            job);
    }

    private Task<bool> CanUploadAsync(JobRequest job)
    {
        if (AuthHelper.IsExternalUser(User))
            return Task.FromResult(AuthHelper.CanAccessCustomer(User, job.CustomerId));

        return Task.FromResult(AuthHelper.CanAccessJob(User, job));
    }

    private static string NormalizeFolder(string folder)
    {
        var f = (folder ?? "supporting").Trim().ToLowerInvariant();
        return f is "moi" or "moa" or "supporting" ? f : "supporting";
    }

    private static JobItemDocumentDto ToDto(JobItemDocument doc) => new()
    {
        Id = doc.JobItemDocumentId,
        JobId = doc.JobRequestId,
        Folder = doc.Folder,
        FileName = doc.FileName,
        ContentType = doc.ContentType,
        UploadedByName = doc.UploadedByName,
        UploadedAt = doc.UploadedAt.ToString("yyyy-MM-dd"),
        VisibleToInternal = doc.VisibleToInternal,
    };
}
