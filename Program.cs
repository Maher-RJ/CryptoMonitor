using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CryptoMonitor.Configuration;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Services.DataSources.Api;
using CryptoMonitor.Services.Mapping;
using CryptoMonitor.Services.Notification;
using CryptoMonitor.Services.Storage;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Add HttpClient as singleton
        services.AddSingleton<HttpClient>();

        // Register configuration
        services.AddSingleton<IConfigurationProvider, AppConfigurationProvider>();

        // Register mappers
        services.AddSingleton<IExchangeMapper<CoinbaseProduct>, CoinbaseMapper>();

        // Register services with updated interfaces
        services.AddSingleton<ITokenProvider, CoinbaseApiService>();
        services.AddSingleton<ITokenRepository, BlobTokenRepository>();

        // Register notification
        services.AddSingleton<NotificationFactory>();
    })

    .ConfigureLogging(logging =>
    {
        logging.Services.Configure<LoggerFilterOptions>(options =>
        {
            var rule = options.Rules.FirstOrDefault(rule => rule.ProviderName
                == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
            if (rule is not null)
            {
                options.Rules.Remove(rule);
            }
        });
    })
    .Build();

host.Run();