using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CryptoMonitor.Core.Interfaces.Notification
{
    public interface INotificationChannel
    {
        Task SendNotificationAsync<T>(string subject, List<T> items, string source);
        bool IsEnabled { get; }
    }
}