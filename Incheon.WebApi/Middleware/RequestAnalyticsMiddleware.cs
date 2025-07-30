// Incheon.WebApi/Middleware/RequestAnalyticsMiddleware.cs
using Analytics.Data.Models;
using Analytics.Data.Services;
using System.Text.Json;

namespace Incheon.WebApi.Middleware
{
    /// <summary>
    /// Custom middleware to log web analytics events for every incoming HTTP request.
    /// </summary>
    public class RequestAnalyticsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestAnalyticsMiddleware> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestAnalyticsMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="logger">Logger for error handling.</param>
        public RequestAnalyticsMiddleware(RequestDelegate next, ILogger<RequestAnalyticsMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Invokes the middleware to process the HTTP request.
        /// </summary>
        /// <param name="context">The HTTP context for the current request.</param>
        /// <param name="analyticsService">The analytics service to record the event, injected by DI.</param>
        public async Task InvokeAsync(HttpContext context, IAnalyticsService analyticsService)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Exception? requestException = null;

            try
            {
                // Call the next middleware in the pipeline first
                await _next(context);
            }
            catch (Exception ex)
            {
                requestException = ex;
                throw; // Re-throw to maintain normal error handling
            }
            finally
            {
                stopwatch.Stop();
                
                // Record analytics event regardless of success/failure
                await RecordAnalyticsEventAsync(context, analyticsService, stopwatch.ElapsedMilliseconds, requestException);
            }
        }

        private async Task RecordAnalyticsEventAsync(
            HttpContext context, 
            IAnalyticsService analyticsService, 
            long responseTimeMs,
            Exception? requestException)
        {
            try
            {
                // Skip recording for certain endpoints if needed
                if (ShouldSkipAnalytics(context.Request.Path))
                {
                    return;
                }

                var webAnalyticsEvent = new WebAnalyticsEvent
                {
                    Timestamp = DateTime.UtcNow,
                    EventType = GetEventType(context.Request.Method, context.Response.StatusCode),
                    PageUrl = GetSanitizedUrl(context.Request),
                    UserId = GetUserId(context),
                    SessionId = GetSessionId(context),
                    UserAgent = GetSanitizedUserAgent(context.Request.Headers["User-Agent"].ToString()),
                    IpAddress = GetClientIpAddress(context),
                    ReferrerUrl = context.Request.Headers["Referer"].ToString(),
                    DataPayload = CreateDataPayload(context, responseTimeMs, requestException)
                };

                await analyticsService.AddEventAsync(webAnalyticsEvent);
                
                _logger.LogDebug("Analytics event recorded for {Method} {Path} - Status: {StatusCode}, Duration: {Duration}ms",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, responseTimeMs);
            }
            catch (Exception ex)
            {
                // Log the error but don't let analytics failures affect the main request
                _logger.LogError(ex, "Failed to record analytics event for {Method} {Path}", 
                    context.Request.Method, context.Request.Path);
            }
        }

        private static bool ShouldSkipAnalytics(PathString path)
        {
            // Skip analytics for certain paths to reduce noise
            var pathValue = path.Value?.ToLowerInvariant();
            return pathValue != null && (
                pathValue.StartsWith("/health") ||
                pathValue.StartsWith("/metrics") ||
                pathValue.StartsWith("/favicon.ico") ||
                pathValue.StartsWith("/_framework") ||
                pathValue.StartsWith("/swagger")
            );
        }

        private static string GetEventType(string httpMethod, int statusCode)
        {
            var baseType = httpMethod.ToUpperInvariant() switch
            {
                "GET" => "PageView",
                "POST" => "DataSubmission",
                "PUT" => "DataUpdate",
                "DELETE" => "DataDeletion",
                "PATCH" => "DataPatch",
                _ => "OtherRequest"
            };

            // Add status code context
            return statusCode >= 400 ? $"{baseType}_Error" : baseType;
        }

        private static string GetSanitizedUrl(HttpRequest request)
        {
            var url = $"{request.Path}{request.QueryString}";
            
            // Remove sensitive query parameters
            if (request.QueryString.HasValue)
            {
                var sanitized = RemoveSensitiveQueryParams(url);
                return sanitized.Length > 2048 ? sanitized[..2048] : sanitized;
            }
            
            return url.Length > 2048 ? url[..2048] : url;
        }

        private static string RemoveSensitiveQueryParams(string url)
        {
            // Remove common sensitive parameters
            var sensitiveParams = new[] { "password", "token", "key", "secret", "auth" };
            var uri = new Uri($"http://dummy{url}"); // Dummy host for parsing
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            
            foreach (var param in sensitiveParams)
            {
                if (query.AllKeys.Any(k => k?.Contains(param, StringComparison.OrdinalIgnoreCase) == true))
                {
                    var keysToRemove = query.AllKeys.Where(k => k?.Contains(param, StringComparison.OrdinalIgnoreCase) == true).ToList();
                    foreach (var key in keysToRemove)
                    {
                        if (key != null) query.Remove(key);
                    }
                }
            }
            
            var cleanQuery = query.Count > 0 ? $"?{query}" : "";
            return $"{uri.LocalPath}{cleanQuery}";
        }

        private static string GetUserId(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return context.User.Identity.Name ?? "AuthenticatedUser";
            }
            return "Anonymous";
        }

        private static string? GetSessionId(HttpContext context)
        {
            try
            {
                return context.Session.Id;
            }
            catch
            {
                return null; // Session might not be available
            }
        }

        private static string? GetSanitizedUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return null;
                
            return userAgent.Length > 512 ? userAgent[..512] : userAgent;
        }

        private static string? GetClientIpAddress(HttpContext context)
        {
            // Check for forwarded IP first (common in load balancer scenarios)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            return context.Connection.RemoteIpAddress?.ToString();
        }

        private static string CreateDataPayload(HttpContext context, long responseTimeMs, Exception? requestException)
        {
            var payload = new
            {
                ResponseTimeMs = responseTimeMs,
                StatusCode = context.Response.StatusCode,
                ContentType = context.Response.ContentType,
                ContentLength = context.Response.ContentLength,
                HasError = requestException != null,
                ErrorType = requestException?.GetType().Name,
                RequestSize = context.Request.ContentLength
            };

            return JsonSerializer.Serialize(payload);
        }
    }
}