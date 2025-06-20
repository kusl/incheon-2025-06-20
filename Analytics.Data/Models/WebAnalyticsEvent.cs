// Analytics.Data/Models/WebAnalyticsEvent.cs
using System.ComponentModel.DataAnnotations;

namespace Analytics.Data.Models
{
    public class WebAnalyticsEvent
    {
        [Key] // Primary key
        public long Id { get; set; } // Using long for potentially many events
        public DateTime Timestamp { get; set; } = DateTime.UtcNow; // Record when the event occurred
        [Required]
        public string EventType { get; set; } = string.Empty; // e.g., "PageView", "Click", "Login"
        public string? UserId { get; set; } // Can be null if user is not logged in or anonymous
        public string? SessionId { get; set; } // For tracking user sessions
        public string? PageUrl { get; set; } // The URL where the event happened
        public string? ReferrerUrl { get; set; } // Where the user came from
        public string? UserAgent { get; set; } // Browser and OS info
        public string? IpAddress { get; set; } // User's IP address (consider GDPR/privacy)
        public string? DataPayload { get; set; } // Optional: JSON or serialized data for custom event details

        // You could add navigation properties if you had related entities,
        // but for simple analytics events, a flat structure is often sufficient.
    }
}