using ICP;

using ICP.Data;

using ICP.Filters;

using ICP.Infrastructure;

using ICP.Models.Auth;
using ICP.Models;
using ICP.Models.ShipInfo;
using ICP.Models.Forwarder;
using ICP.Models.Integration;

using ICP.Repositories;

using ICP.Services;
using ICP.Services.Integration;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Server.IISIntegration;

using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.FileProviders;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Options;

using Serilog;



var isAgaComputer = HostEnvironmentExtensions.IsAgaComputer();

var builder = WebApplication.CreateBuilder(args);

if (!isAgaComputer)
{
    builder.Configuration.Sources.Clear();
    builder.Configuration
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.TEL.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

var appSettingsProfile = ResolveAppSettingsProfile(isAgaComputer, builder.Environment);
var logDirectory = Path.Combine(builder.Environment.ContentRootPath, "Logs");
Directory.CreateDirectory(logDirectory);
var logFilePath = Path.GetFullPath(Path.Combine(logDirectory, $"icp-{DateTime.Now:yyyyMMdd}.log"));

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.File(
            Path.Combine(logDirectory, "icp-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            shared: true)
        .Enrich.WithProperty("ComputerName", Environment.MachineName)
        .Enrich.WithProperty("AppSettings", appSettingsProfile)
        .Enrich.WithProperty("LogPath", logFilePath));

builder.Services.Configure<AppAuthOptions>(
    builder.Configuration.GetSection(AppAuthOptions.SectionName));

builder.Services.Configure<ForwarderDataUploadOptions>(
    builder.Configuration.GetSection(ForwarderDataUploadOptions.SectionName));

builder.Services.Configure<TariffDataOptions>(
    builder.Configuration.GetSection(TariffDataOptions.SectionName));

builder.Services.Configure<IntegrationOptions>(
    builder.Configuration.GetSection(IntegrationOptions.SectionName));

var shipInfoTableFieldsConfiguration = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("Config/shipinfo-table-fields.json", optional: false, reloadOnChange: true)
    .Build();

builder.Services
    .AddOptions<ShipInfoTableFieldsOptions>()
    .Bind(shipInfoTableFieldsConfiguration)
    .ValidateOnStart();

var forwarderTableFieldsConfiguration = new ConfigurationBuilder()
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("Config/forwarder-table-fields.json", optional: false, reloadOnChange: true)
    .Build();

builder.Services
    .AddOptions<ForwarderTableFieldsOptions>()
    .Bind(forwarderTableFieldsConfiguration)
    .ValidateOnStart();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "zh-TW", "en", "ja" };

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("zh-TW");
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.Configure<IISOptions>(options =>
{
    options.AutomaticAuthentication = true;
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<PermissionScannerService>();
builder.Services.AddScoped<PermissionResourceSyncService>();
builder.Services.AddSingleton<ResourceRouteRegistryService>();
builder.Services.AddScoped<UserAuthService>();
builder.Services.AddScoped<UserResourcePermissionService>();
builder.Services.AddScoped<ForwarderDataImportService>();
builder.Services.AddSingleton<ForwarderPendingFileStore>();
builder.Services.AddScoped<IShipInfoRepository, ShipInfoRepository>();
builder.Services.AddScoped<IIntegrationEventOutboxRepository, IntegrationEventOutboxRepository>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddSingleton<IShipInfoCaseEventFactory, ShipInfoCaseEventFactory>();
builder.Services.AddHostedService<IntegrationEventOutboxPublisherWorker>();
builder.Services.AddScoped<ShipInfoMetadataProvider>();
builder.Services.AddScoped<ForwarderTableMetadataProvider>();
builder.Services.AddScoped<ShipInfoLookupService>();
builder.Services.AddScoped<IShipInfoService, ShipInfoService>();
builder.Services.AddScoped<ShipInfoApiExceptionFilter>();
builder.Services.AddScoped<TariffDataImportService>();
builder.Services.AddScoped<RequireLoginFilter>();
builder.Services.AddScoped<RequireResourcePermissionFilter>();

builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add<RequireLoginFilter>();
        options.Filters.Add<RequireResourcePermissionFilter>();
    })
    .AddDataAnnotationsLocalization(options =>
        options.DataAnnotationLocalizerProvider = (_, factory) =>
            factory.Create(typeof(SharedResource)))
    .AddRazorOptions(options =>
    {
        options.ViewLocationExpanders.Add(new PermissionViewLocationExpander());
        options.ViewLocationExpanders.Add(new SettingViewLocationExpander());
    });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ICP_Connection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddDbContext<IlcDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ILC_Connection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
});

builder.Services.AddDbContext<FiestaDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("FIESTA_Connection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlServer(connectionString);
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var routeRegistry = scope.ServiceProvider.GetRequiredService<ResourceRouteRegistryService>();
    await routeRegistry.RefreshAsync();

    var icpConnectionString = app.Configuration.GetConnectionString("ICP_Connection");
    if (!string.IsNullOrWhiteSpace(icpConnectionString))
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ShipInfoSchema");
        await ShipInfoSchemaInitializer.EnsureAuditLogTableAsync(db, logger);
        await IntegrationSchemaInitializer.EnsureOutboxTableAsync(db, logger);
        await ForwarderSchemaInitializer.EnsureArchiveTableAsync(db, logger);
    }
}

var authOptions = app.Services.GetRequiredService<IOptions<AppAuthOptions>>().Value;
var windowsIdentityMode = isAgaComputer &&
    !string.IsNullOrWhiteSpace(authOptions.SimulatedWindowsIdentity)
        ? "Simulated"
        : "Negotiate";

Log.Information(
    "Application starting. ComputerName={ComputerName}, AppSettings={AppSettings}, Environment={EnvironmentName}, WindowsIdentityMode={WindowsIdentityMode}, SimulatedWindowsIdentity={SimulatedWindowsIdentity}, SuperUser={SuperUser}",
    Environment.MachineName,
    appSettingsProfile,
    app.Environment.EnvironmentName,
    windowsIdentityMode,
    authOptions.SimulatedWindowsIdentity,
    authOptions.SuperUser);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.ContentRootPath, "Files")),
    RequestPath = "/Files"
});

app.UseRouting();

var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseSession();

app.UseAuthentication();

app.UseMiddleware<WindowsIdentityMiddleware>();

app.UseAuthorization();

app.UseSerilogRequestLogging();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

try
{
    Log.Information(
        "ICP 啟動。ComputerName={ComputerName}, AppSettings={AppSettings}, LogPath={LogPath}",
        Environment.MachineName,
        appSettingsProfile,
        logFilePath);

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;

        if (addresses is null || addresses.Count == 0)
        {
            Log.Warning("ICP 已啟動，但無法取得監聽位址");
            return;
        }

        foreach (var address in addresses)
        {
            Log.Information("ICP 監聽於 {ListenUrl}", address);
        }
    });

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

static string ResolveAppSettingsProfile(bool isAgaComputer, IWebHostEnvironment environment) =>
    isAgaComputer
        ? environment.IsDevelopment()
            ? "appsettings.json, appsettings.Development.json"
            : "appsettings.json"
        : "appsettings.TEL.json";
