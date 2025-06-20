// Incheon.WebApi/Controllers/TimeController.cs
using Analytics.Data.Models;
using Analytics.Data.Services;
using Microsoft.AspNetCore.Mvc;

// Add these usings

namespace Incheon.WebApi.Controllers
{
    /// <summary>
    /// Controller for retrieving current time information.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")] // Sets the base route for this controller to /api/Time
    public class TimeController : ControllerBase
    {
        private readonly IAnalyticsService _analyticsService;

        // Inject the analytics service into the controller
        public TimeController(IAnalyticsService analyticsService)
        {
            _analyticsService = analyticsService;
        }

        /// <summary>
        /// Represents the JSON response for the current time.
        /// </summary>
        public record CurrentTimeResponse(string Iso8601Utc);

        /// <summary>
        /// Gets the current UTC date and time in ISO 8601 format.
        /// </summary>
        /// <returns>A JSON object containing the current UTC timestamp.</returns>
        /// <response code="200">Returns the current UTC date and time.</response>
        [HttpGet] // Maps to GET /api/Time
        [Produces("application/json")] // Specifies that this action produces JSON
        [ProducesResponseType(typeof(CurrentTimeResponse), StatusCodes.Status200OK)]
        public IActionResult GetCurrentUtcTime()
        {
            // Get the current UTC time
            DateTime utcNow = DateTime.UtcNow;

            // Format it to ISO 8601 string.
            // "o" format specifier is for ISO 8601 (e.g., "2023-10-27T10:00:00.0000000Z")
            string iso8601Time = utcNow.ToString("o");

            // Create the response object
            var response = new CurrentTimeResponse(iso8601Time);

            // Return the response as a JSON object
            return Ok(response);
        }

        // ... (existing GetCurrentUtcTime method)

        /// <summary>
        /// DEBUG ONLY: Gets all recorded web analytics events.
        /// </summary>
        /// <returns>A list of recorded WebAnalyticsEvent objects.</returns>
        /// <response code="200">Returns the list of analytics events.</response>
        [HttpGet("events")] // Maps to GET /api/Time/events
        [Produces("application/json")]
        [ProducesResponseType(typeof(IEnumerable<WebAnalyticsEvent>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAnalyticsEvents()
        {
            var events = await _analyticsService.GetAllEventsAsync();
            return Ok(events);
        }
    }
}
