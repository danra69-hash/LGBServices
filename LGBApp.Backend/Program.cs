using System.Text;
using LGBApp.Backend.Data;
using LGBApp.Backend.Middleware;
using LGBApp.Backend.Tools;
using LGBApp.Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
var useRestrictedCors = !builder.Environment.IsDevelopment() && corsOrigins.Length > 0;

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (useRestrictedCors)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        options.UseSqlite(connectionString);
    else
        options.UseSqlServer(connectionString);
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<SignatoryAccessService>();
builder.Services.AddScoped<SignatoryDedupService>();
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

if (args.Contains("reset-dev-db"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DevDatabaseReset.RunAsync(context, builder.Configuration);
    return;
}

if (args.Contains("repair-jobs"))
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await RepairCustomerJobs.RunAsync(context);
    return;
}

if (args.Contains("reset-dev-password"))
{
    var resetEmail = args.SkipWhile(a => a != "reset-dev-password").Skip(1).FirstOrDefault();
    if (string.IsNullOrWhiteSpace(resetEmail))
    {
        Console.Error.WriteLine("Usage: dotnet run -- reset-dev-password <email>");
        return;
    }

    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var user = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == resetEmail.Trim().ToLower());
    if (user == null)
    {
        Console.Error.WriteLine($"User not found: {resetEmail}");
        return;
    }

    user.PasswordHash = PasswordHasher.Hash("password123");
    user.MustChangePassword = false;
    await context.SaveChangesAsync();
    Console.WriteLine($"Reset password for {user.Email} to password123 (mustChangePassword cleared).");
    return;
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.EnsureCreated();
        SqliteSchemaMigrator.Apply(context);
        WorkflowConfigSeeder.Seed(context);
        InternalStaffSeeder.Seed(context, resetPasswordsInDevelopment: false);
        BillingPartyService.SeedFromLegacyCustomerFieldsAsync(context).GetAwaiter().GetResult();
        CustomerClientAdminProvisioner.EnsureAllCustomersHaveClientAdminAsync(context).GetAwaiter().GetResult();
        CustomerSignatoryProvisioner.EnsureAllCustomerSignatoriesAsync(context).GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<SignatoryAccessService>()
            .BackfillFromHoldersAsync(context).GetAwaiter().GetResult();
        FigmaProductCatalog.SyncCatalog(context);
        JobRequestSyncService.LinkOrphanJobs(context);
        context.SaveChanges();

        // Full package/job sync is expensive (hundreds of units). Run once on empty DB;
        // per-customer sync runs from CustomersController on create/update.
        var needsFullJobBootstrap = !context.JobRequests.Any();
        if (needsFullJobBootstrap)
        {
            JobRequestSyncService.SyncAllCustomersAsync(context).GetAwaiter().GetResult();
            JobWorkflowIntegrityService.RepairAllAsync(context).GetAwaiter().GetResult();
            var allJobs = context.JobRequests.ToList();
            JobFormProvisioner.EnsureFormsForJobsAsync(context, allJobs).GetAwaiter().GetResult();
        }
        else
        {
            // Backfill draft MOI shells for single-qty service lines created after initial bootstrap.
            var jobsMissingMoi = context.JobRequests
                .Where(j => j.TaskType == "Service"
                    && j.CustomerPackageId != null
                    && j.TotalQty <= 1
                    && !context.MOIForms.Any(m => m.JobRequestId == j.JobRequestId))
                .ToList();
            if (jobsMissingMoi.Count > 0)
                JobFormProvisioner.EnsureFormsForJobsAsync(context, jobsMissingMoi).GetAwaiter().GetResult();
        }
    }
    else
    {
        context.Database.Migrate();
        WorkflowConfigSeeder.SeedReferenceData(context);
        context.SaveChanges();
        FigmaProductCatalog.SyncCatalog(context);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AppCors");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.MapControllers();

app.Run();
