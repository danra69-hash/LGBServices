using System.Text;
using System.Threading.RateLimiting;
using LGBApp.Backend.Data;
using LGBApp.Backend.Middleware;
using LGBApp.Backend.Tools;
using LGBApp.Backend.Services;
using LGBApp.Backend.Services.Email;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
corsOrigins = corsOrigins.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

// §7.4: fail closed in non-Development — never AllowAnyOrigin in production
if (!builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Cors:AllowedOrigins must be configured in non-Development environments "
        + "(e.g. Cors__AllowedOrigins__0=https://your-frontend.vercel.app).");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCors", policy =>
    {
        if (builder.Environment.IsDevelopment() && corsOrigins.Length == 0)
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

var isSqlite = string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);
var isPostgres = string.Equals(dbProvider, "Postgres", StringComparison.OrdinalIgnoreCase)
    || string.Equals(dbProvider, "Postgresql", StringComparison.OrdinalIgnoreCase);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isSqlite)
        options.UseSqlite(connectionString);
    else if (isPostgres)
        options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure());
    else
        options.UseSqlServer(connectionString);

    // Dual-provider migrations live in one assembly; filter by provider (see ProviderFilteredMigrationsAssembly).
    options.ReplaceService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsAssembly, ProviderFilteredMigrationsAssembly>();
});

builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<SignatoryAccessService>();
builder.Services.AddScoped<SignatoryDedupService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<WorkflowNotifier>();
builder.Services.AddSingleton<IAppClock, SystemAppClock>();
builder.Services.AddScoped<ReminderEvaluationService>();
builder.Services.AddHostedService<ReminderWorker>();
builder.Services.AddHttpClient<ResendEmailSender>();
var resendKey = builder.Configuration["Email:ResendApiKey"];
if (!string.IsNullOrWhiteSpace(resendKey))
    builder.Services.AddScoped<IEmailSender>(sp => sp.GetRequiredService<ResendEmailSender>());
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");

// C2: refuse known/placeholder JWT keys (forgeable Admin tokens)
var committedJwtPlaceholders = new HashSet<string>(StringComparer.Ordinal)
{
    "LGBApp-Dev-Secret-Key-Change-In-Production-32chars!",
    "REPLACE_WITH_RANDOM_SECRET_AT_LEAST_32_CHARS",
};
if (!builder.Environment.IsDevelopment())
{
    if (string.IsNullOrWhiteSpace(jwtKey)
        || jwtKey.Length < 32
        || committedJwtPlaceholders.Contains(jwtKey))
    {
        throw new InvalidOperationException(
            "Jwt:Key must be a random secret of at least 32 characters in non-Development environments. "
            + "Set Jwt__Key on the host (never use the appsettings placeholder).");
    }
}

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

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
builder.Services.AddProblemDetails();
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

if (args.Contains("seed-full"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: context.Database.IsSqlite());
        WorkflowConfigSeeder.Seed(context);
    }

    await SeedFullCommand.RunAsync(app.Services);
    return;
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (isSqlite || isPostgres)
    {
        Console.WriteLine(isPostgres
            ? "[Startup] Postgres init via EF Migrate…"
            : "[Startup] Sqlite init via EF Migrate…");
        // Wave 4: Migrate() + stamp legacy DBs; SQLite hand migrator never runs on Postgres.
        DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: isSqlite);
        WorkflowConfigSeeder.Seed(context);

        // C2: seeded default-password staff only in Development or when SEED_STAFF=true
        var seedStaff = builder.Environment.IsDevelopment()
            || string.Equals(Environment.GetEnvironmentVariable("SEED_STAFF"), "true", StringComparison.OrdinalIgnoreCase);
        if (seedStaff)
        {
            var seedPassword = Environment.GetEnvironmentVariable("SEED_STAFF_PASSWORD");
            if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(seedPassword))
            {
                throw new InvalidOperationException(
                    "SEED_STAFF=true requires SEED_STAFF_PASSWORD in non-Development environments.");
            }

            InternalStaffSeeder.Seed(
                context,
                resetPasswordsInDevelopment: builder.Environment.IsDevelopment(),
                initialPassword: string.IsNullOrWhiteSpace(seedPassword) ? "password123" : seedPassword);
        }

        FigmaProductCatalog.SyncCatalog(context);
        FormCustomerIdBackfill.Apply(context);
        context.SaveChanges();

        if (string.Equals(Environment.GetEnvironmentVariable("SEED_FULL"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                "[Startup] SEED_FULL env is ignored for healthcheck safety. "
                + "Run once: dotnet run -- seed-full");
        }
        else
        {
            Console.WriteLine("[Startup] Light seed complete (staff + catalog). Run: dotnet run -- seed-full");
        }
    }
    else
    {
        Console.WriteLine("[Startup] SqlServer init via EF Migrate…");
        DatabaseBootstrap.ApplyMigrations(context, runSqliteHandMigrator: false);
        WorkflowConfigSeeder.SeedReferenceData(context);
        FormCustomerIdBackfill.Apply(context);
        context.SaveChanges();
        FigmaProductCatalog.SyncCatalog(context);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseLgbExceptionHandler();
app.UseCors("AppCors");
app.UseRateLimiter();
// Behind Railway/Render/etc. TLS terminates at the proxy — skip redirect there.
var disableHttpsRedirection = string.Equals(
    Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECTION"),
    "true",
    StringComparison.OrdinalIgnoreCase);
if (!disableHttpsRedirection)
    app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();

app.Run();
