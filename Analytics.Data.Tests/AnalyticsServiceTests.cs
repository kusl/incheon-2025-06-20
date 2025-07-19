// Analytics.Data.Tests/AnalyticsServiceTests.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Analytics.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace Analytics.Data.Tests
{
    public class AnalyticsServiceTests : IDisposable
    {
        private readonly AnalyticsDbContext _context;
        private readonly AnalyticsService _service;
        private readonly SqliteConnection _connection;

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

            _service = new AnalyticsService(_context);
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
        public async Task GetEventsByEventTypeAsync_ReturnsCorrectEvents()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click", PageUrl = "/button1" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "PageView", PageUrl = "/about" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Click", PageUrl = "/button2" });

            var clickEvents = await _service.GetEventsByEventTypeAsync("Click");

            Assert.Equal(2, clickEvents.Count());
            Assert.Contains(clickEvents, e => e.PageUrl == "/button1");
            Assert.Contains(clickEvents, e => e.PageUrl == "/button2");
        }

        [Fact]
        public async Task GetAllEventsAsync_ReturnsAllEvents()
        {
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Load" });
            await _service.AddEventAsync(new WebAnalyticsEvent { EventType = "Scroll" });

            var allEvents = await _service.GetAllEventsAsync();

            Assert.Equal(2, allEvents.Count());
        }

        // IDisposable for cleanup
        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose(); // This will also close the connection
        }
    }
}
