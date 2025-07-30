// Analytics.Data.Tests/AnalyticsServiceTests.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Analytics.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Analytics.Data.Tests
{
    public class AnalyticsServiceTests : IDisposable
    {
        private readonly AnalyticsDbContext _context;
        private readonly AnalyticsService _service;
        private readonly SqliteConnection _connection;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AnalyticsService> _logger;

        public AnalyticsServiceTests()
        {
            // Create and open a SQLite in-memory connection
            // The connection must remain open for the in-memory database to persist
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Configure DbContext options to use this connection
            var options = new DbContextOptionsBuilder<AnalyticsDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new AnalyticsDbContext(options);

            // Ensure the database is created
            _context.Database.EnsureCreated();

            // Create memory cache and logger for the service
            _cache = new MemoryCache(new MemoryCacheOptions());
            _logger = new TestLogger<AnalyticsService>();

            _service = new AnalyticsService(_context, _cache, _logger);
        }

        [Fact]
        public async Task AddEventAsync_AddsNewEventToDatabase()
        {
            var newEvent = new WebAnalyticsEvent
            {
                EventType = "PageView",
                PageUrl = "/home",
                UserId = "user123"
            };

            await _service.AddEventAsync(newEvent);

            var retrievedEvent = await _context.WebAnalyticsEvents.FirstOrDefaultAsync(e => e.EventType == "PageView");

            Assert.NotNull(retrievedEvent);
            Assert.Equal("PageView", retrievedEvent.EventType);
            Assert.Equal("/home", retrievedEvent.PageUrl);
            Assert.Equal("user123", retrievedEvent.UserId);
        }

        [Fact]
        public async Task AddEventsBatchAsync_AddsMultipleEventsToDatabase()
        {
            var events = new List<WebAnalyticsEvent>
            {
                new() { EventType = "Click", PageUrl = "/button1", UserId = "user1" },
                new() { EventType = "Click", PageUrl = "/button2", UserId = "user2" },
                new() { EventType = "PageView", PageUrl = "/about", UserId = "user3" }
            };

            await _service.AddEventsBatchAsync(events);

            var allEvents = await _context.WebAnalyticsEvents.ToListAsync();
            Assert.Equal(3, allEvents.Count);
        }

        [Fact]
        public async Task GetEventsByEventTypeAsync_ReturnsCorrectEvents()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click", PageUrl = "/button1" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "PageView", PageUrl = "/about" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click", PageUrl = "/button2" });

            var clickEvents = await _service.GetEventsByEventTypeAsync("Click");

            IEnumerable<WebAnalyticsEvent> webAnalyticsEvents = clickEvents.ToList();
            Assert.Equal(2, webAnalyticsEvents.Count());
            Assert.Contains(webAnalyticsEvents, e => e.PageUrl == "/button1");
            Assert.Contains(webAnalyticsEvents, e => e.PageUrl == "/button2");
        }

        [Fact]
        public async Task GetAllEventsAsync_ReturnsAllEvents()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Load" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Scroll" });

            var allEvents = await _service.GetAllEventsAsync();

            Assert.Equal(2, allEvents.Count());
        }

        [Fact]
        public async Task GetEventCountsByTypeAsync_ReturnsCorrectCounts()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "PageView" });

            var counts = await _service.GetEventCountsByTypeAsync();

            Assert.Equal(2, counts["Click"]);
            Assert.Equal(1, counts["PageView"]);
        }

        [Fact]
        public async Task GetTotalEventCountAsync_ReturnsCorrectCount()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "PageView" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Scroll" });

            var totalCount = await _service.GetTotalEventCountAsync();

            Assert.Equal(3, totalCount);
        }

        [Fact]
        public async Task GetEventsByDateRangeAsync_ReturnsEventsInRange()
        {
            var baseTime = DateTime.UtcNow.Date;
            
            await _service.AddEventAsync(new WebAnalyticsEvent 
            { 
                EventType = "Old", 
                Timestamp = baseTime.AddDays(-2) 
            });
            
            await _service.AddEventAsync(new WebAnalyticsEvent 
            { 
                EventType = "InRange", 
                Timestamp = baseTime 
            });
            
            await _service.AddEventAsync(new WebAnalyticsEvent 
            { 
                EventType = "Future", 
                Timestamp = baseTime.AddDays(2) 
            });

            var eventsInRange = await _service.GetEventsByDateRangeAsync(
                baseTime.AddDays(-1), 
                baseTime.AddDays(1));

            Assert.Single(eventsInRange);
            Assert.Equal("InRange", eventsInRange.First().EventType);
        }

        [Fact]
        public async Task GetRecentEventsAsync_ReturnsSpecifiedCount()
        {
            // Add more events than we want to retrieve
            for (int i = 0; i < 10; i++)
            {
                await _service.AddEventAsync(new WebAnalyticsEvent 
                { 
                    EventType = $"Event{i}",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i) // Make them have different timestamps
                });
            }

            var recentEvents = await _service.GetRecentEventsAsync(5);

            Assert.Equal(5, recentEvents.Count());
            // Should be ordered by timestamp descending (most recent first)
            Assert.Equal("Event0", recentEvents.First().EventType);
        }

        // IDisposable for cleanup
        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose(); // This will also close the connection
            _cache.Dispose();
        }
    }

    // Simple test logger implementation
    public class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // For testing, we can just ignore logging or write to console if needed
            // Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}