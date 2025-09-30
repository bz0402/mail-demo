namespace MailDemo.Models
{
    public class EmailLog
    {
        public string EmailId { get; set; } = Guid.NewGuid().ToString();
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? ImageUrl { get; set; } 
        public string Status { get; set; } = "Sent"; 
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? OpenedAt { get; set; }
    }
}
