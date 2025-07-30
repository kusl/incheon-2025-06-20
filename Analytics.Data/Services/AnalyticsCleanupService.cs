using Microsoft.Extensions.Hosting;

namespace Analytics.Data.Services
{
    public class AnalyticsCleanupService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // Clean up analytics data older than retention period
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}