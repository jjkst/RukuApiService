using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RukuServiceApi.Context;
using RukuServiceApi.HealthChecks;
using RukuServiceApi.Middleware;
using RukuServiceApi.Models;
using RukuServiceApi.Services;
using RukuServiceApi.Validators;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

// Load environment variables from .env.local if it exists
static void LoadEnvFile()
{
    var envFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env.local");
    envFile = Path.GetFullPath(envFile);

    if (!File.Exists(envFile))
    {
        return;
    }

    var lines = File.ReadAllLines(envFile);
    foreach (var line in lines)
    {
        var trimmed = line.Trim();

        // Skip comments and empty lines
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            continue;

        // Parse KEY=VALUE or KEY="VALUE" or KEY='VALUE'
        var match = Regex.Match(trimmed, @"^\s*([^=]+)=(.*)$");
        if (match.Success)
        {
            var key = match.Groups[1].Value.Trim();
            var value = match.Groups[2].Value.Trim();

            // Remove surrounding quotes if present
            if (
                (value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))
            )
            {
                value = value.Substring(1, value.Length - 2);
            }

            // Only set if not already set (environment variables take precedence)
            if (Environment.GetEnvironmentVariable(key) == null)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }
}

LoadEnvFile();

var builder = WebApplication.CreateBuilder(args);

// Add environment variable substitution
builder.Configuration.AddEnvironmentVariables();

// Process configuration to substitute environment variables
var configuration = builder
    .Configuration.AsEnumerable()
    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
foreach (var kvp in configuration.ToList())
{
    if (kvp.Value != null && kvp.Value.StartsWith("${") && kvp.Value.EndsWith("}"))
    {
        var envVarName = kvp.Value.Substring(2, kvp.Value.Length - 3);
        var envVarValue = Environment.GetEnvironmentVariable(envVarName);
        if (envVarValue != null)
        {
            builder.Configuration[kvp.Key] = envVarValue;
        }
        else
        {
            // Log warning for missing environment variables (except for optional ones)
            if (!envVarName.Equals("ALLOWED_HOSTS", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"Warning: Environment variable '{envVarName}' not found for configuration key '{kvp.Key}'"
                );
            }
        }
    }
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/ruku-service-api-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10 * 1024 * 1024
    ) // 10MB
    .CreateLogger();

// Use Serilog
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddValidatorsFromAssemblyContaining<CreateServiceRequestValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure settings from appsettings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<FileUploadSettings>(
    builder.Configuration.GetSection("FileUploadSettings")
);

// Register HttpClient for OAuth flows
builder.Services.AddHttpClient();

// Configure rate limiting for email sending (3 emails per hour per IP)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("EmailLimit", opt =>
    {
        opt.PermitLimit = 3;              // 3 emails
        opt.Window = TimeSpan.FromHours(1); // per hour
        opt.QueueLimit = 0;               // no queuing
    });
});

// Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();
builder.Services.AddSingleton<EmailService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
if (jwtSettings?.SecretKey == null)
{
    throw new InvalidOperationException(
        "JWT settings are not configured properly. JWT_SECRET_KEY environment variable is required."
    );
}

// Validate JWT secret key length (HS256 requires at least 32 bytes/256 bits)
if (jwtSettings.SecretKey.Length < 32)
{
    throw new InvalidOperationException(
        $"JWT secret key must be at least 32 characters long. Current length: {jwtSettings.SecretKey.Length}. " +
        "Please set JWT_SECRET_KEY environment variable with a longer value (minimum 32 characters)."
    );
}

builder
    .Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.ASCII.GetBytes(jwtSettings.SecretKey)
            ),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

// Configure Authorization Policies
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthorizationPolicies.AdminOnly, policy => policy.RequireRole("Admin"))
    .AddPolicy(AuthorizationPolicies.AdminOrOwner, policy => policy.RequireRole("Admin", "Owner"))
    .AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy => policy.RequireAuthenticatedUser());

// Add CORS policy - configure based on environment
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy(
            "AllowAngularApp",
            policy =>
            {
                policy
                    .WithOrigins("http://localhost:4200", "https://localhost:4200", "https://jk-dev.site")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        );
    }
    else
    {
        // Production CORS - restrict origins, methods, and headers
        var allowedOrigins =
            builder.Configuration["AllowedOrigins"]?.Split(',')
            ?? ["https://jk-dev.site"];
        options.AddPolicy(
            "AllowAngularApp",
            policy =>
            {
                policy
                    .WithOrigins(allowedOrigins)
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                    .WithHeaders("Content-Type", "Authorization")
                    .AllowCredentials();
            }
        );
    }
});

// Register the database context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 42)),
        mySqlOptions =>
        {
            mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null
            );
        }
    );

    if (builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors();
        options.EnableSensitiveDataLogging();
    }
});

// Add health checks
builder
    .Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<EmailServiceHealthCheck>("email_service")
    .AddCheck<FileSystemHealthCheck>("file_system")
    .AddCheck<MemoryHealthCheck>("memory")
    .AddDbContextCheck<ApplicationDbContext>("db_context");

var app = builder.Build();

// Configure URLs - use port 5002 for development
if (app.Environment.IsDevelopment())
{
    app.Urls.Add("http://localhost:5002");
}

// Configure the HTTP request pipeline.
// Add middleware in the correct order
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<ValidationMiddleware>();

// Only enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RukuService API V1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAngularApp");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Map health check endpoints
app.MapHealthChecks(
    "/health",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var response = new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(entry => new
                {
                    name = entry.Key,
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    duration = entry.Value.Duration.TotalMilliseconds,
                    data = entry.Value.Data,
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds,
                timestamp = DateTime.UtcNow,
            };
            await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
        },
    }
);

app.MapHealthChecks(
    "/health/ready",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    }
);

app.MapHealthChecks(
    "/health/live",
    new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // Only basic liveness check
    }
);

app.MapControllers();

try
{
    Log.Information("Starting RukuServiceApi application");

    // Ensure database is created and seeded
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database is created
        await context.Database.EnsureCreatedAsync();

        // Seed admin user in all environments (reads from ADMIN_EMAIL, ADMIN_UID env vars)
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAdminAsync();

        // Seed dev test users only in Development environment
        if (app.Environment.IsDevelopment())
        {
            await seeder.SeedAsync();
        }
    }

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
