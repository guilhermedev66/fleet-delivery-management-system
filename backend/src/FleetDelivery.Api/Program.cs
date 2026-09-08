using System.Text;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.Api.HealthChecks;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Application.Users;
using FleetDelivery.Modules.Identity.Infrastructure;
using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using FleetDelivery.Modules.Identity.Infrastructure.Security;
using FleetDelivery.Modules.Identity.Infrastructure.Seeding;
using FleetDelivery.Modules.Shipments.Infrastructure;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence;
using FleetDelivery.Modules.Vehicles.Infrastructure;
using FleetDelivery.Modules.Vehicles.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// Bootstrap logger: captures anything that happens before the host (and its
// configuration-driven logger) is fully built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FleetDelivery.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    builder.Services.AddOpenApi();

    // Identity module: DbContext, repositories, password hashing, JWT issuance.
    builder.Services.AddIdentityModule(builder.Configuration);

    // Shipments module: DbContext (own "shipments" schema), repository,
    // Outbox-wired SaveChanges. "Driver" is just an Identity User with
    // Role.Driver in M2 — no separate Drivers module yet — so its Application
    // layer calls back into Identity's own Application contract (via MediatR)
    // to validate an assignable driverId; see AssignCommand.
    builder.Services.AddShipmentsModule(builder.Configuration);

    // Vehicles module: DbContext (own "vehicles" schema), repository. No
    // cross-module calls out of Vehicles itself; Shipments' AssignCommand
    // calls back into it (mirroring the Identity driver-validation call) to
    // validate an assignable vehicleId.
    builder.Services.AddVehiclesModule(builder.Configuration);

    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(
        typeof(LoginCommand).Assembly,
        typeof(FleetDelivery.Modules.Shipments.Application.Shipments.CreateShipmentCommand).Assembly,
        typeof(FleetDelivery.Modules.Vehicles.Application.Vehicles.RegisterVehicleCommand).Assembly));

    const string FrontendCorsPolicy = "Frontend";

    builder.Services.AddCors(options =>
    {
        // Read lazily (inside this options-configuration callback, not eagerly
        // at top level) so overrides injected by WebApplicationFactory in
        // integration tests — added as part of builder.Build() — are actually
        // visible here. An eager `builder.Configuration[...]` read before
        // Build() would silently miss them.
        var corsAllowedOrigin = builder.Configuration["Cors:AllowedOrigin"];

        options.AddPolicy(FrontendCorsPolicy, policy =>
        {
            // AllowCredentials() requires a specific origin, never AllowAnyOrigin() —
            // the two are mutually exclusive by the CORS spec once cookies are involved.
            if (!string.IsNullOrWhiteSpace(corsAllowedOrigin))
            {
                policy.WithOrigins(corsAllowedOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            }
        });
    });

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
            var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

            // Keep claim types exactly as issued (no legacy short-name -> long-URI
            // remapping) — JwtTokenService already writes the role claim using the
            // full ClaimTypes.Role URI so [Authorize(Roles = "...")] works either way.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    string.IsNullOrEmpty(jwtOptions.SigningKey) ? new string('0', 32) : jwtOptions.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();

    // Fixed-window limiter on /api/auth/login only, per client IP. Defaults to
    // 5 requests/minute; overridable via config (e.g. loosened in integration
    // tests, which share one host/rate-limiter instance across many test-driven
    // login calls that don't themselves exercise rate-limiting behavior).
    builder.Services.AddRateLimiter(options =>
    {
        // Read lazily inside this options-configuration callback — see the CORS
        // comment above for why an eager top-level read would miss overrides
        // injected by WebApplicationFactory in integration tests.
        var loginRateLimitPermits = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", 5);
        var loginRateLimitWindowSeconds = builder.Configuration.GetValue("RateLimiting:Login:WindowSeconds", 60);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy("login", httpContext => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = loginRateLimitPermits,
                Window = TimeSpan.FromSeconds(loginRateLimitWindowSeconds),
                QueueLimit = 0,
            }));
    });

    builder.Services.AddHealthChecks()
        .AddCheck<IdentityDbHealthCheck>("identity-db")
        .AddCheck<RabbitMqHealthCheck>("rabbitmq");

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();

        // Migrations are applied automatically only in Development. Other
        // environments must apply them explicitly via `dotnet ef database
        // update` (see backend/README.md) as part of deployment.
        using (var scope = app.Services.CreateScope())
        {
            var provider = scope.ServiceProvider;
            var dbContext = provider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.MigrateAsync();

            await IdentityDevSeeder.SeedAsync(
                dbContext,
                provider.GetRequiredService<IUserRepository>(),
                provider.GetRequiredService<IPasswordHasher>(),
                provider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDevSeeder"));

            // Shipments module: own schema, own migration history table. No
            // dev seed — there's no meaningful default shipment to seed.
            var shipmentsDbContext = provider.GetRequiredService<ShipmentsDbContext>();
            await shipmentsDbContext.Database.MigrateAsync();

            // Vehicles module: own schema, own migration history table. No
            // dev seed either — same reasoning as Shipments.
            var vehiclesDbContext = provider.GetRequiredService<VehiclesDbContext>();
            await vehiclesDbContext.Database.MigrateAsync();
        }
    }

    app.UseHttpsRedirection();

    app.UseCors(FrontendCorsPolicy);

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    // Liveness: process is up and able to respond. No dependency checks.
    app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

    // Readiness: checks Postgres connectivity via IdentityDbContext.
    app.MapHealthChecks("/health/ready");

    app.MapAuthEndpoints();
    app.MapShipmentEndpoints();
    app.MapVehicleEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "FleetDelivery.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the implicit Program class to WebApplicationFactory<Program> in FleetDelivery.IntegrationTests.
public partial class Program
{
}
