// Analytics.Data/Services/IAnalyticsService.cs
using Analytics.Data.Models;

namespace Analytics.Data.Services
{
    public interface IAnalyticsService
    {
        Task AddEventAsync(WebAnalyticsEvent analyticsEvent);
        Task AddEventsBatchAsync(IEnumerable<WebAnalyticsEvent> analyticsEvents);
        Task<WebAnalyticsEvent?> GetEventByIdAsync(long id);
        Task<IEnumerable<WebAnalyticsEvent>> GetEventsByEventTypeAsync(string eventType);
        Task<IEnumerable<WebAnalyticsEvent>> GetAllEventsAsync();
        Task<IEnumerable<WebAnalyticsEvent>> GetEventsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<WebAnalyticsEvent>> GetEventsByUserIdAsync(string userId);
        Task<Dictionary<string, int>> GetEventCountsByTypeAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<Dictionary<string, int>> GetPageViewCountsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<IEnumerable<WebAnalyticsEvent>> GetRecentEventsAsync(int count = 100);
        Task<long> GetTotalEventCountAsync();
        Task<IEnumerable<object>> GetHourlyEventStatsAsync(DateTime date);
        Task<double> GetAverageResponseTimeAsync(DateTime? startDate = null, DateTime? endDate = null);
    }
}