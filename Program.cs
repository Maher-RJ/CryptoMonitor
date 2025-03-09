using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CryptoMonitor.Configuration;
using CryptoMonitor.Core.Interfaces.Configuration;
using CryptoMonitor.Core.Interfaces.DataSources;
using CryptoMonitor.Core.Interfaces.Storage;
using CryptoMonitor.Core.Models.Coinbase;
using CryptoMonitor.Services.DataSources.Api;
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

        // Register services
        services.AddSingleton<ITokenProvider<CoinbaseProduct>, CoinbaseApiService>();
        services.AddSingleton<ITokenRepository<CoinbaseProduct>, BlobTokenRepository>();

        // Register notification
        services.AddSingleton<NotificationFactory>();
    })
    .Build();

host.Run();