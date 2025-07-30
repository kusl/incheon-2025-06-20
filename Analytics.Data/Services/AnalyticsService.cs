// Analytics.Data/Services/AnalyticsService.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Analytics.Data.Services
{
    public class AnalyticsService : IAnalyticsService
    {
        private readonly AnalyticsDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AnalyticsService> _logger;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

        public AnalyticsService(
            AnalyticsDbContext context, 
            IMemoryCache cache, 
            ILogger<AnalyticsService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task AddEventAsync(WebAnalyticsEvent analyticsEvent)
        {
            ArgumentNullException.ThrowIfNull(analyticsEvent);

            try
            {
                _context.WebAnalyticsEvents.Add(analyticsEvent);
                await _context.SaveChangesAsync();
                
                // Invalidate relevant cache entries
                InvalidateCountCaches();
                
                _logger.LogDebug("Analytics event added: {EventType} for {UserId}", 
                    analyticsEvent.EventType, analyticsEvent.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add analytics event: {EventType}", analyticsEvent.EventType);
                throw;
            }
        }

        public async Task AddEventsBatchAsync(IEnumerable<WebAnalyticsEvent> analyticsEvents)
        {
            ArgumentNullException.ThrowIfNull(analyticsEvents);

            var eventsList = analyticsEvents.ToList();
            if (!eventsList.Any()) return;

            try
            {
                _context.WebAnalyticsEvents.AddRange(eventsList);
                await _context.SaveChangesAsync();
                
                // Invalidate relevant cache entries
                InvalidateCountCaches();
                
                _logger.LogDebug("Batch added {Count} analytics events", eventsList.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add batch of {Count} analytics events", eventsList.Count);
                throw;
            }
        }

        public async Task<WebAnalyticsEvent?> GetEventByIdAsync(long id)
        {
            try
            {
                return await _context.WebAnalyticsEvents.FindAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get event by ID: {Id}", id);
                throw;
            }
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetEventsByEventTypeAsync(string eventType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

            try
            {
                return await _context.WebAnalyticsEvents
                    .Where(e => e.EventType == eventType)
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get events by type: {EventType}", eventType);
                throw;
            }
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetAllEventsAsync()
        {
            try
            {
                return await _context.WebAnalyticsEvents
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get all events");
                throw;
            }
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetEventsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                throw new ArgumentException("Start date must be before end date");

            try
            {
                return await _context.WebAnalyticsEvents
                    .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get events by date range: {StartDate} to {EndDate}", 
                    startDate, endDate);
                throw;
            }
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetEventsByUserIdAsync(string userId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            try
            {
                return await _context.WebAnalyticsEvents
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get events by user ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetEventCountsByTypeAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var cacheKey = $"event-counts-by-type-{startDate?.ToString("yyyyMMdd")}-{endDate?.ToString("yyyyMMdd")}";
            
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, int>? cachedResult) && cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var query = _context.WebAnalyticsEvents.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(e => e.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(e => e.Timestamp <= endDate.Value);

                var result = await query
                    .GroupBy(e => e.EventType)
                    .Select(g => new { EventType = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.EventType, x => x.Count);

                _cache.Set(cacheKey, result, _cacheExpiry);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get event counts by type");
                throw;
            }
        }

        public async Task<Dictionary<string, int>> GetPageViewCountsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var cacheKey = $"page-view-counts-{startDate?.ToString("yyyyMMdd")}-{endDate?.ToString("yyyyMMdd")}";
            
            if (_cache.TryGetValue(cacheKey, out Dictionary<string, int>? cachedResult) && cachedResult != null)
            {
                return cachedResult;
            }

            try
            {
                var query = _context.WebAnalyticsEvents
                    .Where(e => e.EventType == "PageView" && e.PageUrl != null);

                if (startDate.HasValue)
                    query = query.Where(e => e.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(e => e.Timestamp <= endDate.Value);

                var result = await query
                    .GroupBy(e => e.PageUrl!)
                    .Select(g => new { PageUrl = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count)
                    .Take(50) // Limit to top 50 pages
                    .ToDictionaryAsync(x => x.PageUrl, x => x.Count);

                _cache.Set(cacheKey, result, _cacheExpiry);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get page view counts");
                throw;
            }
        }

        public async Task<IEnumerable<WebAnalyticsEvent>> GetRecentEventsAsync(int count = 100)
        {
            if (count <= 0 || count > 1000)
                throw new ArgumentException("Count must be between 1 and 1000");

            try
            {
                return await _context.WebAnalyticsEvents
                    .OrderByDescending(e => e.Timestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get recent events");
                throw;
            }
        }

        public async Task<long> GetTotalEventCountAsync()
        {
            const string cacheKey = "total-event-count";
            
            if (_cache.TryGetValue(cacheKey, out long cachedCount))
            {
                return cachedCount;
            }

            try
            {
                var count = await _context.WebAnalyticsEvents.CountAsync();
                _cache.Set(cacheKey, count, _cacheExpiry);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get total event count");
                throw;
            }
        }

        public async Task<IEnumerable<object>> GetHourlyEventStatsAsync(DateTime date)
        {
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            try
            {
                var hourlyStats = await _context.WebAnalyticsEvents
                    .Where(e => e.Timestamp >= startOfDay && e.Timestamp < endOfDay)
                    .GroupBy(e => e.Timestamp.Hour)
                    .Select(g => new
                    {
                        Hour = g.Key,
                        Count = g.Count(),
                        UniqueUsers = g.Select(e => e.UserId).Distinct().Count(),
                        PageViews = g.Count(e => e.EventType == "PageView"),
                        Errors = g.Count(e => e.EventType.EndsWith("_Error"))
                    })
                    .OrderBy(x => x.Hour)
                    .ToListAsync();

                return hourlyStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get hourly event stats for date: {Date}", date);
                throw;
            }
        }

        public async Task<double> GetAverageResponseTimeAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.WebAnalyticsEvents
                    .Where(e => e.DataPayload != null);

                if (startDate.HasValue)
                    query = query.Where(e => e.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(e => e.Timestamp <= endDate.Value);

                var events = await query.ToListAsync();
                
                var responseTimes = events
                    .Select(e => ExtractResponseTime(e.DataPayload))
                    .Where(rt => rt.HasValue)
                    .Select(rt => rt!.Value)
                    .ToList();

                return responseTimes.Any() ? responseTimes.Average() : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get average response time");
                throw;
            }
        }

        private static double? ExtractResponseTime(string? dataPayload)
        {
            if (string.IsNullOrEmpty(dataPayload)) return null;

            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(dataPayload);
                if (payload.TryGetProperty("ResponseTimeMs", out var responseTimeProperty))
                {
                    return responseTimeProperty.GetDouble();
                }
            }
            catch
            {
                // Ignore JSON parsing errors
            }

            return null;
        }

        private void InvalidateCountCaches()
        {
            // Remove cached counts that would be affected by new events
            _cache.Remove("total-event-count");
            
            // Remove event count caches (this is simplified - in production you might want more sophisticated cache invalidation)
            var keysToRemove = new List<string>();
            // In a real implementation, you'd track cache keys or use cache tagging
        }
    }
}