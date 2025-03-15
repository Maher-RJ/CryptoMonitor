# CryptoMonitor

CryptoMonitor is an Azure Functions application designed to track new cryptocurrency token listings and roadmap updates across various exchange sources. It allows traders to receive early notifications about new crypto assets, potentially providing a competitive advantage for trading opportunities.

## 🚀 Key Features

- **Real-time Exchange API Monitoring**: Tracks Coinbase API for new token listings
- **Web Scraping**: Monitors exchange roadmaps and blogs for upcoming token listings
- **Change Detection**: Identifies both new tokens and tokens removed from roadmaps (potential listings)
- **Flexible Notifications**: Email notifications when changes are detected
- **Multi-Exchange Support**: Architecture ready for multiple data sources (currently focusing on Coinbase)

## 🏗️ Architecture

CryptoMonitor is built using Azure Functions with a modular, interface-based architecture:

- **API Monitoring**: Polls exchange APIs for newly listed tokens
- **Web Scraping**: Parses web pages to extract token information 
- **Storage**: Uses Azure Blob Storage to track token state and detect changes
- **Notifications**: Sends email alerts when changes are detected

## 📋 Prerequisites

- .NET 8.0 SDK
- Azure subscription
- Azure Communication Services account (for email notifications)
- Azure Blob Storage account

## ⚙️ Configuration

Configuration is managed through Azure Function app settings or local.settings.json for development:

### Core Settings
```json
{
  "TestMode": "false",                        // Set to true for development/testing
  "ApiSchedule": "0 */20 * * *",             // CRON schedule for API monitoring (every 20 hours)
  "WebScrapingSchedule": "*/4 * * * *",      // CRON schedule for web scraping (every 4 minutes)
  "ProductionContainerName": "crypto-data",   // Azure storage container for production
  "TestContainerName": "crypto-data-test"     // Azure storage container for testing
}
```

### Data Source Settings
```json
{
  "ApiMonitoringEnabled": "true",
  "BlogMonitoringEnabled": "true",
  "WebScrapingEnabled": "true",
  "DataSourcesCoinbaseEnabled": "true",
  "DataSourcesCoinbaseApiEnabled": "true",
  "DataSourcesCoinbaseBlogEnabled": "true",
  "DataSourcesCoinbaseApiUrl": "https://api.exchange.coinbase.com/products",
  "DataSourcesCoinbaseBlogUrl": "https://blog.coinbase.com",
  "DataSourcesCoinbaseRoadmapUrl": "https://www.coinbase.com/blog/increasing-transparency-for-new-asset-listings-on-coinbase"
}
```

### Web Scraping Configuration
```json
{
  "WebScrapingPage0Name": "Coinbase Roadmap",
  "WebScrapingPage0Url": "https://www.coinbase.com/blog/increasing-transparency-for-new-asset-listings-on-coinbase",
  "WebScrapingPage0Source": "CoinbaseBlog",
  "WebScrapingPage0ParserType": "CoinbaseRoadmap",
  "WebScrapingPage0Enabled": "true"
}
```

### Notification Settings
```json
{
  "NotificationsEmailEnabled": "true",
  "EmailSenderAddress": "your-sender@example.com",
  "EmailRecipientAddress": "your-email@example.com",
  "AzureCommunicationServicesConnectionString": "your-connection-string"
}
```

## 🚀 Deployment

### Azure Deployment

1. Create a new Azure Function App (C# Isolated Worker Process)
2. Deploy the project using VS Code, Visual Studio, or Azure CLI
3. Configure application settings in the Azure portal

### Local Development

1. Clone the repository
2. Create a `local.settings.json` file with the settings described above
3. Run `func start` using the Azure Functions Core Tools

## 📝 Usage

Once deployed, CryptoMonitor runs automatically based on the configured schedules:

1. **API Monitoring**: Checks exchange APIs for new tokens at the `ApiSchedule` interval
2. **Web Scraping**: Checks monitored web pages at the `WebScrapingSchedule` interval

When new tokens are detected (or tokens are removed from roadmaps), email notifications are sent to the configured recipient.

## 🧩 Extending the System

### Adding New Data Sources

1. Implement `ITokenProvider` for new exchange APIs
2. Implement `IBlogMonitor` for new exchange blogs/roadmaps
3. Create a new parser implementing `IPageParser` for new web scraping sources
4. Register the new components in `Program.cs`

### Adding New Notification Channels

1. Implement `INotificationChannel` for your new notification method
2. Register in the `NotificationFactory`

## 🔒 Security Notes

- Store sensitive configuration (connection strings, API keys) using proper Azure Functions app settings
- Use managed identities where possible for accessing Azure resources
- The web scraping code includes anti-bot protection measures to avoid request blocking

## 👥 Authors

- **Maher Jabbar**
- **ChatGPT**

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
