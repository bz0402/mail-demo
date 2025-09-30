using Confluent.Kafka;
using MailDemo.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace MailDemo
{
    public class KafkaProducer : IKafkaProducer, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly string _topicName;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
        {
            _logger = logger;
            _topicName = configuration["Kafka:EmailTrackingTopic"] ?? "email-tracking-events";

            var config = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = 30000,
                RequestTimeoutMs = 30000
            };

            _producer = new ProducerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => _logger.LogError($"Kafka Error: {e.Reason}"))
                .Build();
        }

        public async Task SendEmailEventAsync(EmailTrackingEvent trackingEvent)
        {
            try
            {
                var message = new Message<string, string>
                {
                    Key = trackingEvent.EmailId,
                    Value = trackingEvent.ToJson(),
                    Headers = new Headers
                    {
                        { "event-type", Encoding.UTF8.GetBytes(trackingEvent.EventType) },
                        { "timestamp", Encoding.UTF8.GetBytes(trackingEvent.Timestamp.ToString("O")) }
                    }
                };

                var result = await _producer.ProduceAsync(_topicName, message);
                _logger.LogInformation($"📨 Kafka Event Sent: {trackingEvent.EventType} for Email {trackingEvent.EmailId} to partition {result.Partition}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send Kafka event for Email {trackingEvent.EmailId}");
                throw;
            }
        }

        public async Task SendEmailSentEventAsync(string emailId, string toEmail, string subject)
        {
            var trackingEvent = new EmailTrackingEvent
            {
                EmailId = emailId,
                EventType = "sent",
                ToEmail = toEmail,
                Subject = subject,
                Metadata = { { "status", "delivered_to_smtp" } }
            };

            await SendEmailEventAsync(trackingEvent);
        }

        public async Task SendEmailOpenedEventAsync(string emailId, string userAgent, string ipAddress)
        {
            var trackingEvent = new EmailTrackingEvent
            {
                EmailId = emailId,
                EventType = "opened",
                UserAgent = userAgent,
                IpAddress = ipAddress,
                Metadata = {
                    { "tracking_method", "pixel" },
                    { "read_status", "confirmed" }
                }
            };

            await SendEmailEventAsync(trackingEvent);
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}