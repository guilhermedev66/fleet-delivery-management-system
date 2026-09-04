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
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();

    // Liveness: process is up and able to respond. No dependency checks.
    app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

    // Readiness (M1): once Postgres and RabbitMQ are wired in, this endpoint
    // will run AddHealthChecks() checks against both (DB connectivity via
    // Npgsql, broker connectivity via RabbitMQ) and map to /health/ready.
    // Left unmapped for now — there's nothing to check yet.

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
