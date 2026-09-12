using Serilog;
using TEL.IntegrationHub;
using TEL.IntegrationHub.Infrastructure;

var isAgaComputer = HostEnvironmentExtensions.IsAgaComputer();
var telAppSettingsFile = ResolveTelAppSettingsFile();

var builder = WebApplication.CreateBuilder(args);

if (!isAgaComputer)
{
    builder.Configuration.Sources.Clear();
    builder.Configuration
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile(telAppSettingsFile, optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

var appSettingsProfile = ResolveAppSettingsProfile(isAgaComputer, builder.Environment, telAppSettingsFile);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ComputerName", Environment.MachineName)
    .Enrich.WithProperty("AppSettings", appSettingsProfile)
    .WriteTo.Console());

builder.Services.AddHubServices(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "TEL Integration Hub API",
        Version = "v1",
        Description = "Phase 1: MessageLog + consumers for Deposit / ARUR / Export (Export reserved)."
    });
});

var app = builder.Build();

Log.Information(
    "TEL.IntegrationHub starting on {ComputerName}; AppSettings={AppSettings}",
    Environment.MachineName,
    appSettingsProfile);

await app.Services.EnsureHubDatabaseAsync();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "TEL.IntegrationHub",
    appSettings = appSettingsProfile
}));

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

static string ResolveTelAppSettingsFile() =>
    string.Equals(Environment.MachineName, "TETIS87181", StringComparison.OrdinalIgnoreCase)
        ? "appsettings.TEL.TETIS87181.json"
        : "appsettings.TEL.json";

static string ResolveAppSettingsProfile(
    bool isAgaComputer,
    IWebHostEnvironment environment,
    string telAppSettingsFile) =>
    isAgaComputer
        ? environment.IsDevelopment()
            ? "appsettings.json, appsettings.Development.json"
            : "appsettings.json"
        : telAppSettingsFile;
