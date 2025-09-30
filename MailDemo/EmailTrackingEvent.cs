using System.Text.Json;

namespace MailDemo.Models
{
    public class EmailTrackingEvent
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString();
        public string EmailId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty; // "sent", "opened", "clicked"
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string UserAgent { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string> Metadata { get; set; } = new();

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        public static EmailTrackingEvent? FromJson(string json)
        {
            return JsonSerializer.Deserialize<EmailTrackingEvent>(json);
        }
    }
}