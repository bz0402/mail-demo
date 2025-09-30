using MailDemo.Models;

namespace MailDemo.Data
{
    public static class EmailRepository
    {
        private static readonly List<EmailLog> _emails = new();

        public static void AddEmail(EmailLog email)
        {
            _emails.Add(email);
            Console.WriteLine($" Email logged: {email.EmailId} to {email.ToEmail}");
        }

        public static EmailLog? GetEmail(string emailId)
        {
            return _emails.FirstOrDefault(e => e.EmailId == emailId);
        }

        public static bool MarkAsRead(string emailId)
        {
            var email = GetEmail(emailId);
            if (email != null)
            {
                if (email.Status != "Read")
                {
                    email.Status = "Read";
                    email.OpenedAt = DateTime.UtcNow; 
                    Console.WriteLine($" Email {emailId} marked as READ at {email.OpenedAt}");
                    return false;  
                }
                else
                {
                    Console.WriteLine($" Email {emailId} was already marked as read");
                    return true;  
                }
            }
            else
            {
                Console.WriteLine($"Attempted to mark unknown email as read: {emailId}");
                return false;
            }
        }

        public static IEnumerable<EmailLog> GetAll()
        {
            return _emails.OrderByDescending(e => e.CreatedAt);
        }

        public static IEnumerable<EmailLog> GetByStatus(string status)
        {
            return _emails.Where(e => e.Status.Equals(status, StringComparison.OrdinalIgnoreCase))
                         .OrderByDescending(e => e.CreatedAt);
        }

        public static IEnumerable<EmailLog> GetReadEmails()
        {
            return GetByStatus("Read");
        }

        public static IEnumerable<EmailLog> GetUnreadEmails()
        {
            return GetByStatus("Sent");
        }

        public static void ClearAll()
        {
            _emails.Clear();
            Console.WriteLine("All email logs cleared");
        }

        public static object GetStatistics()
        {
            var total = _emails.Count;
            var read = _emails.Count(e => e.Status == "Read");
            var unread = total - read;

            return new
            {
                Total = total,
                Read = read,
                Unread = unread,
                ReadPercentage = total > 0 ? Math.Round((double)read / total * 100, 2) : 0
            };
        }
    }
}