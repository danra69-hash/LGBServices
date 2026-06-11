using System.Text;
using LGBApp.Backend.Data;
using LGBApp.Backend.Middleware;
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

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        context.Database.EnsureCreated();
        SqliteSchemaMigrator.Apply(context);
        DevDataSeeder.SeedIfEmpty(context);
        WorkflowConfigSeeder.Seed(context);
        InternalStaffSeeder.Seed(context, app.Environment.IsDevelopment());
        BillingPartyService.SeedFromLegacyCustomerFieldsAsync(context).GetAwaiter().GetResult();
        CustomerClientAdminProvisioner.EnsureAllCustomersHaveClientAdminAsync(context).GetAwaiter().GetResult();
        CustomerSignatoryProvisioner.EnsureAllCustomerSignatoriesAsync(context).GetAwaiter().GetResult();
        scope.ServiceProvider.GetRequiredService<SignatoryAccessService>()
            .BackfillFromHoldersAsync(context).GetAwaiter().GetResult();
        FigmaProductCatalog.SyncCatalog(context);
        JobRequestSyncService.LinkOrphanJobs(context);
        context.SaveChanges();
        JobRequestSyncService.SyncAllCustomersAsync(context).GetAwaiter().GetResult();
        JobWorkflowIntegrityService.RepairAllAsync(context).GetAwaiter().GetResult();
        var allJobs = context.JobRequests.ToList();
        JobFormProvisioner.EnsureFormsForJobsAsync(context, allJobs).GetAwaiter().GetResult();
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
