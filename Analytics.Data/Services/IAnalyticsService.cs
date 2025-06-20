// Analytics.Data/Services/IAnalyticsService.cs
using Analytics.Data.Models;

namespace Analytics.Data.Services
{
    public interface IAnalyticsService
    {
        Task AddEventAsync(WebAnalyticsEvent analyticsEvent);
        Task<WebAnalyticsEvent?> GetEventByIdAsync(long id);
        Task<IEnumerable<WebAnalyticsEvent>> GetEventsByEventTypeAsync(string eventType);
        Task<IEnumerable<WebAnalyticsEvent>> GetAllEventsAsync();
        // Add more analytical query methods as needed, e.g., GetEventsByDateRange, CountEventsByEventType
    }
}