using MailDemo.Models;

namespace MailDemo
{
    public interface IKafkaProducer
    {
        Task SendEmailEventAsync(EmailTrackingEvent trackingEvent);
        Task SendEmailSentEventAsync(string emailId, string toEmail, string subject);
        Task SendEmailOpenedEventAsync(string emailId, string userAgent, string ipAddress);
    }
}