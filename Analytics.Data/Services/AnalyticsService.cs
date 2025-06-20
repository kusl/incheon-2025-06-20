// Analytics.Data/Services/AnalyticsService.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Analytics.Data.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AnalyticsDbContext _context;

        public AnalyticsService(AnalyticsDbContext context)
        {
            _context = context;
            // Ensure the database schema is created.
            // For in-memory, this effectively means creating the tables.
            // For SQLite in-memory, this is important as no migrations are run.
            _context.Database.EnsureCreated();
        }

        public async Task AddEventAsync(WebAnalyticsEvent analyticsEvent)
        {
            if (analyticsEvent == null)
            {
                throw new ArgumentNullException(nameof(analyticsEvent));
            }

            _context.WebAnalyticsEvents.Add(analyticsEvent);
            await _context.SaveChangesAsync();
        }

        public async Task<WebAnalyticsEvent?> GetEventByIdAsync(long id)
        {
            return await _context.WebAnalyticsEvents.FindAsync(id);
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetEventsByEventTypeAsync(string eventType)
        {
            return await _context.WebAnalyticsEvents
                                 .Where(e => e.EventType == eventType)
                                 .ToListAsync();
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetAllEventsAsync()
        {
            return await _context.WebAnalyticsEvents.ToListAsync();
        }
    }
}