using System;
using CryptoMonitor.Configuration.Models;

namespace CryptoMonitor.Core.Interfaces.Configuration
{
    public interface IConfigurationProvider
    {
        AppSettings GetSettings();
    }
}