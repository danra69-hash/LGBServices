using LGBApp.Backend.Data;
using LGBApp.Backend.Models.DTOs;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LGBApp.Backend.Controllers;

[Route("api/signatory-dedup")]
[ApiController]
[Authorize(Roles = "Admin")]
public class SignatoryDedupController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly SignatoryDedupService _dedupService;

    public SignatoryDedupController(AppDbContext context, SignatoryDedupService dedupService)
    {
        _context = context;
        _dedupService = dedupService;
    }

    [HttpGet("overlaps")]
    public async Task<ActionResult<List<SignatoryOverlapDto>>> GetOverlaps()
    {
        return await _dedupService.FindOverlapsAsync(_context);
    }

    [HttpPost("link")]
    public async Task<ActionResult<SignatoryLinkResultDto>> LinkByEmail([FromBody] LinkSignatoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Email is required." });

        try
        {
            return await _dedupService.LinkByEmailAsync(_context, request.Email);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}

public class LinkSignatoryRequest
{
    public string Email { get; set; } = string.Empty;
}
