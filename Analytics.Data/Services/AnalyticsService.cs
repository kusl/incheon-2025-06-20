// Analytics.Data/Services/AnalyticsService.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Data.Services
{
    public class AnalyticsService(AnalyticsDbContext context) : IAnalyticsService
    {
        public async Task AddEventAsync(WebAnalyticsEvent analyticsEvent)
        {
            ArgumentNullException.ThrowIfNull(analyticsEvent);

            context.WebAnalyticsEvents.Add(analyticsEvent);
            await context.SaveChangesAsync();
        }

        public async Task<WebAnalyticsEvent?> GetEventByIdAsync(long id)
        {
            return await context.WebAnalyticsEvents.FindAsync(id);
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetEventsByEventTypeAsync(string eventType)
        {
            return await context.WebAnalyticsEvents
                                 .Where(e => e.EventType == eventType)
                                 .ToListAsync();
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetAllEventsAsync()
        {
            return await context.WebAnalyticsEvents.ToListAsync();
        }
    }
}