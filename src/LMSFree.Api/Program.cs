using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using LMSFree.Api;
using LMSFree.Api.Endpoints;
using LMSFree.Api.Security;
using LMSFree.Domain;
using LMSFree.Infrastructure;
using LMSFree.Infrastructure.Data;
using LMSFree.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Host.UseSerilog((ctx, log) => log
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret is required and must be at least 32 characters. Set Jwt__Secret.");

builder.Services.AddInfrastructure(config);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CourseAccess>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.SigningKey,
        ClockSkew = TimeSpan.FromSeconds(30)
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy("Staff", p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Teacher)));

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = AssignmentEndpoints.MaxUploadBytes + 1024 * 1024);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = AssignmentEndpoints.MaxUploadBytes + 1024 * 1024);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<LmsDbContext>("database", HealthStatus.Unhealthy, ["ready"]);

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = config.GetValue("RateLimiting:AuthPermitsPerMinute", 10)
        }));
});

var origins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (origins.Contains("*"))
    throw new InvalidOperationException("Cors:Origins can't be *. List the web app origins.");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services, config, app.Logger);

app.UseForwardedHeaders();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || config.GetValue<bool>("EnableApiDocs"))
{
    app.MapOpenApi();
    app.MapScalarApiReference("/docs");
}

app.MapAuth();
app.MapUsers();
app.MapCourses();
app.MapAssignments();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

public partial class Program;
