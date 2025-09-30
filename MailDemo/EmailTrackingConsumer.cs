using Confluent.Kafka;
using MailDemo.Data;
using MailDemo.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MailDemo
{
    public class EmailTrackingConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<EmailTrackingConsumer> _logger;
        private readonly string _topicName;
        private readonly string _bootstrapServers;

        public EmailTrackingConsumer(IConfiguration configuration, ILogger<EmailTrackingConsumer> logger)
        {
            _logger = logger;
            _topicName = configuration["Kafka:EmailTrackingTopic"] ?? "email-tracking-events";
            _bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

            var config = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = "email-tracking-consumer-group",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true,
                AutoCommitIntervalMs = 5000,
                // Add timeout to prevent indefinite blocking
                SessionTimeoutMs = 10000,
                MaxPollIntervalMs = 300000
            };

            try
            {
                _logger.LogInformation("Initializing Kafka consumer with BootstrapServers: {BootstrapServers}", _bootstrapServers);
                _consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, e) => _logger.LogError($"Kafka Consumer Error: {e.Reason}"))
                    .SetLogHandler((_, log) => _logger.LogDebug($"Kafka Log: {log.Message}"))
                    .Build();
                _logger.LogInformation("Kafka consumer initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka consumer");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Starting Kafka consumer for topic: {Topic}", _topicName);
            try
            {
                _logger.LogInformation("Attempting to subscribe to topic: {Topic}", _topicName);
                _consumer.Subscribe(_topicName);
                _logger.LogInformation("Successfully subscribed to topic: {Topic}", _topicName);

                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        _logger.LogDebug("Polling for Kafka messages...");
                        var consumeResult = _consumer.Consume(stoppingToken);
                        _logger.LogInformation("Received message: Key={Key}, Value={Value}, Partition={Partition}, Offset={Offset}",
                            consumeResult?.Message?.Key ?? "null",
                            consumeResult?.Message?.Value ?? "null",
                            consumeResult?.Partition ?? -1,
                            consumeResult?.Offset ?? -1);

                        if (consumeResult?.Message?.Value != null)
                        {
                            await ProcessTrackingEvent(consumeResult.Message);
                        }
                        else
                        {
                            _logger.LogWarning("Received null or empty message from Kafka");
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Error consuming Kafka message: {Reason}", ex.Error.Reason);
                        if (ex.Error.IsFatal)
                        {
                            _logger.LogCritical("Fatal Kafka error, stopping consumer");
                            break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Kafka consumer cancelled by host");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error in Kafka consumer");
                        // Wait briefly to prevent tight loop on errors
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in Kafka consumer during subscription");
            }
            finally
            {
                _logger.LogInformation("Closing Kafka consumer...");
                try
                {
                    _consumer.Close();
                    _logger.LogInformation("Kafka consumer closed");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing Kafka consumer");
                }
            }
        }

        private async Task ProcessTrackingEvent(Message<string, string> message)
        {
            try
            {
                if (string.IsNullOrEmpty(message.Value))
                {
                    _logger.LogWarning("Received empty message value from Kafka");
                    return;
                }

                var trackingEvent = EmailTrackingEvent.FromJson(message.Value);
                if (trackingEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize tracking event: {Value}", message.Value);
                    return;
                }

                _logger.LogInformation("Processing {EventType} event for Email {EmailId}", trackingEvent.EventType, trackingEvent.EmailId);

                switch (trackingEvent.EventType.ToLower())
                {
                    case "sent":
                        await HandleEmailSent(trackingEvent);
                        break;

                    case "opened":
                        await HandleEmailOpened(trackingEvent);
                        break;

                    case "clicked":
                        await HandleEmailClicked(trackingEvent);
                        break;

                    default:
                        _logger.LogWarning("Unknown event type: {EventType}", trackingEvent.EventType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing tracking event");
            }
        }

        private async Task HandleEmailSent(EmailTrackingEvent trackingEvent)
        {
            _logger.LogInformation("Email SENT: {EmailId} to {ToEmail}", trackingEvent.EmailId, trackingEvent.ToEmail);

            var email = EmailRepository.GetEmail(trackingEvent.EmailId);
            if (email != null)
            {
                email.Status = "Sent";
                _logger.LogInformation("Email {EmailId} status updated to 'Sent'", trackingEvent.EmailId);
            }
        }

        private async Task HandleEmailOpened(EmailTrackingEvent trackingEvent)
        {
            _logger.LogInformation("📩 🎉 EMAIL OPENED VIA KAFKA! EmailId: {EmailId}", trackingEvent.EmailId);
            _logger.LogInformation("👤 User Agent: {UserAgent}", trackingEvent.UserAgent);
            _logger.LogInformation("🌍 IP Address: {IpAddress}", trackingEvent.IpAddress);
            _logger.LogInformation("⏰ Opened At: {Timestamp}", trackingEvent.Timestamp);

            EmailRepository.MarkAsRead(trackingEvent.EmailId);

            var email = EmailRepository.GetEmail(trackingEvent.EmailId);
            if (email != null)
            {
                _logger.LogInformation("KAFKA TRACKING SUCCESS: \"{Subject}\" opened by {ToEmail}", email.Subject, email.ToEmail);
                _logger.LogInformation("Email journey: Sent → Delivered → OPENED ✅");
            }
        }

        private async Task HandleEmailClicked(EmailTrackingEvent trackingEvent)
        {
            _logger.LogInformation("🖱️ Email CLICKED: {EmailId}", trackingEvent.EmailId);

            var email = EmailRepository.GetEmail(trackingEvent.EmailId);
            if (email != null)
            {
                _logger.LogInformation("Click tracking for: {Subject}", email.Subject);
            }
        }

        public override void Dispose()
        {
            _logger.LogInformation("Disposing Kafka consumer...");
            _consumer?.Dispose();
            base.Dispose();
        }
    }
}