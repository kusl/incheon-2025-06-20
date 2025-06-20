// Incheon.WebApi/Controllers/TimeController.cs
using Microsoft.AspNetCore.Mvc;
using System; // Required for DateTime

namespace Incheon.WebApi.Controllers
{
    /// <summary>
    /// Controller for retrieving current time information.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")] // Sets the base route for this controller to /api/Time
    public class TimeController : ControllerBase
    {
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
    }
}
