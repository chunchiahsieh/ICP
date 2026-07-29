using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TEL.IntegrationHub.Consumers;
using TEL.IntegrationHub.Data;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Services;

namespace TEL.IntegrationHub;

public static class HubServiceCollectionExtensions
{
    public static IServiceCollection AddHubServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HubIntegrationOptions>(configuration.GetSection(HubIntegrationOptions.SectionName));

        var connectionString = configuration.GetConnectionString("HubDatabase")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=TEL_IntegrationHub;Trusted_Connection=True;TrustServerCertificate=True";

        services.AddDbContext<HubDbContext>(options => options.UseSqlServer(connectionString));

        var icpConnectionString = configuration.GetConnectionString("ICP_Connection");
        if (string.IsNullOrWhiteSpace(icpConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:ICP_Connection is required for Hub to mark ICP Outbox Completed.");
        }

        services.AddDbContext<IcpDbContext>(options => options.UseSqlServer(icpConnectionString));
        services.AddScoped<IIcpOutboxCompletionService, IcpOutboxCompletionService>();

        services.AddScoped<IMessageLogService, MessageLogService>();
        services.AddScoped<IExportDemoOrchestrationService, ExportDemoOrchestrationService>();
        services.AddSingleton<IRoutingService, StubRoutingService>();
        services.AddSingleton<ITargetWriter>(_ => new NoOpTargetWriter("GEM"));
        services.AddSingleton<ITargetWriter>(_ => new NoOpTargetWriter("ARUR"));
        services.AddSingleton<ITargetWriter>(_ => new NoOpTargetWriter("ICP"));

        var rabbitEnabled = configuration.GetValue("Integration:RabbitMq:Enabled", false);
        if (rabbitEnabled)
        {
            services.AddMassTransit(x =>
            {
                x.AddConsumer<DepositCaseInitiatedConsumer>();
                x.AddConsumer<ArurCaseInitiatedConsumer>();
                x.AddConsumer<ExportFileCompletedConsumer>();

                x.UsingRabbitMq((context, cfg) =>
                {
                    var options = context.GetRequiredService<IOptionsMonitor<HubIntegrationOptions>>().CurrentValue.RabbitMq;

                    cfg.Host(options.HostName, (ushort)options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.UserName);
                        h.Password(options.Password);
                    });

                    cfg.ClearSerialization();
                    cfg.UseRawJsonSerializer();

                    // ShipInfo case initiated (Deposit / ARUR via caseType — one consumer per event)
                    cfg.ReceiveEndpoint(options.QueueName, e =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind(options.Exchange, s =>
                        {
                            s.ExchangeType = options.ExchangeType;
                            s.RoutingKey = IcpIntegrationEventTypes.ShipInfoCaseInitiated;
                        });
                        e.ConfigureConsumer<DepositCaseInitiatedConsumer>(context);
                        e.ConfigureConsumer<ArurCaseInitiatedConsumer>(context);
                    });

                    // Function/Export completed (reserved; ICP publisher not ready yet)
                    cfg.ReceiveEndpoint(options.QueueName + ".export", e =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.Bind(options.Exchange, s =>
                        {
                            s.ExchangeType = options.ExchangeType;
                            s.RoutingKey = IcpIntegrationEventTypes.ExportCompleted;
                        });
                        e.ConfigureConsumer<ExportFileCompletedConsumer>(context);
                    });
                });
            });
        }
        else
        {
            services.AddSingleton<IHostedService, RabbitMqDisabledNotice>();
        }

        return services;
    }

    public static async Task EnsureHubDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<HubIntegrationOptions>>().CurrentValue;
        if (!options.Database.EnsureCreatedOnStartup)
            return;

        var db = scope.ServiceProvider.GetRequiredService<HubDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }

    private sealed class RabbitMqDisabledNotice : IHostedService
    {
        private readonly ILogger<RabbitMqDisabledNotice> _logger;

        public RabbitMqDisabledNotice(ILogger<RabbitMqDisabledNotice> logger) => _logger = logger;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogWarning(
                "Integration:RabbitMq:Enabled is false. MassTransit consumer is not started; API query endpoints remain available.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
