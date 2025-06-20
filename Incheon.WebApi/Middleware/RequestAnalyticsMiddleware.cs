// Incheon.WebApi/Middleware/RequestAnalyticsMiddleware.cs
using Analytics.Data.Models;
using Analytics.Data.Services;

namespace Incheon.WebApi.Middleware
{
    /// <summary>
    /// Custom middleware to log web analytics events for every incoming HTTP request.
    /// </summary>
    public class RequestAnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        // IAnalyticsService is resolved per request due to its Scoped lifetime
        // and the middleware's InvokeAsync signature.

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestAnalyticsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        public RequestAnalyticsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invokes the middleware to process the HTTP request.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <param name="analyticsService">The analytics service to record the event, injected by DI.</param>
        public async Task InvokeAsync(HttpContext context, IAnalyticsService analyticsService)
        {
            // Create a new WebAnalyticsEvent for the incoming request
            var webAnalyticsEvent = new WebAnalyticsEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = GetEventType(context.Request.Method), // Map HTTP method to an event type
                PageUrl = context.Request.Path.ToString(),
                // Attempt to get user ID (if authenticated) - adjust based on your authentication
                UserId = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "Anonymous",
                SessionId = context.Session.Id, // Requires app.UseSession() in Program.cs if you want actual session IDs
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                // You could add DataPayload for POST/PUT request bodies,
                // but be careful with performance and sensitive data.
                // For simplicity, we'll omit body logging here.
            };

            try
            {
                // Add the event to the analytics database
                await analyticsService.AddEventAsync(webAnalyticsEvent);
            }
            catch (Exception ex)
            {
                // Log the error without blocking the request pipeline.
                // In a real application, you'd use a proper logger (e.g., ILogger<RequestAnalyticsMiddleware>).
                Console.WriteLine($"Error recording analytics event: {ex.Message}");
            }

            // Call the next middleware in the pipeline
            await _next(context);
        }

        /// <summary>
        /// Helper to map HTTP method to a more descriptive event type.
        /// </summary>
        private static string GetEventType(string httpMethod)
        {
            return httpMethod.ToUpperInvariant() switch
            {
                "GET" => "PageView",
                "POST" => "DataSubmission",
                "PUT" => "DataUpdate",
                "DELETE" => "DataDeletion",
                _ => "OtherRequest"
            };
        }
    }
}
