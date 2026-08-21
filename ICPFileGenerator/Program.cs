using ICPFileGenerator.Infrastructure;
using ICPFileGenerator.Infrastructure.Database;
using ICPFileGenerator.Models;
using ICPFileGenerator.Repositories;
using ICPFileGenerator.Services;
using ICPFileGenerator.Workers;

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

builder.Services.Configure<FileGeneratorOptions>(
    builder.Configuration.GetSection(FileGeneratorOptions.SectionName));

builder.Services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IJobRepository, JobRepository>();
builder.Services.AddScoped<IFileGenerationService, FileGenerationService>();
builder.Services.AddScoped<IHubNotificationService, HubNotificationService>();
builder.Services.AddHttpClient("IntegrationHub", (sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileGeneratorOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(options.Hub.BaseUrl)
        ? "http://localhost:5261"
        : options.Hub.BaseUrl.TrimEnd('/') + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHostedService<FileGenerationWorker>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Logger.LogInformation(
    "ICPFileGenerator starting on {ComputerName}; AppSettings={AppSettings}",
    Environment.MachineName,
    appSettingsProfile);

if (app.Environment.IsDevelopment() || isAgaComputer)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

static string ResolveAppSettingsProfile(bool isAgaComputer, IWebHostEnvironment environment) =>
    isAgaComputer
        ? environment.IsDevelopment()
            ? "appsettings.json, appsettings.Development.json"
            : "appsettings.json"
        : "appsettings.TEL.json";
