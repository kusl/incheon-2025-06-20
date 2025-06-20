// Analytics.Data.Tests/AnalyticsServiceTests.cs
using Analytics.Data.Context;
using Analytics.Data.Models;
using Analytics.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite; // Add this using directive

namespace Analytics.Data.Tests
{
    public class AnalyticsServiceTests : IDisposable
    {
        private readonly AnalyticsDbContext _context;
        private readonly AnalyticsService _service;
        private readonly SqliteConnection _connection; // Store the connection created by CreateInMemorySqliteOptions

        public AnalyticsServiceTests()
        {
            // Use a unique name for each test class instance to ensure isolation.
            // This allows tests to run in parallel without interfering with each other's in-memory DB.
            var databaseName = Guid.NewGuid().ToString();

            // Get the options, which now creates and opens a new connection
            var options = AnalyticsDbContext.CreateInMemorySqliteOptions(databaseName);

            // Store the connection for explicit disposal later
            _connection = (SqliteConnection)options.Extensions.OfType<Microsoft.EntityFrameworkCore.Sqlite.Infrastructure.Internal.SqliteOptionsExtension>().FirstOrDefault()!.Connection!;

            _context = new AnalyticsDbContext(options);

            // Ensure the database is created and empty for each test run
            // This is key for "pristine on every run" within the test fixture's lifecycle.
            _context.Database.EnsureDeleted();
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
            _context.Dispose(); // Dispose the DbContext instance

            // Explicitly close and dispose the SqliteConnection to ensure the in-memory database is truly gone.
            // This is crucial for "pristine on every run" behavior between test methods.
            if (_connection != null && _connection.State == System.Data.ConnectionState.Open)
            {
                _connection.Close();
            }
            _connection?.Dispose();
        }
    }
}