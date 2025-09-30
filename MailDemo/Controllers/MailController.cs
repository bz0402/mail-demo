using MailDemo.Data;
using MailDemo.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Web;

namespace MailDemo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MailController : ControllerBase
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<MailController> _logger;
        private readonly CampaignsConfig _campaignsConfig;


        public MailController(IKafkaProducer kafkaProducer, IOptions<SmtpSettings> smtpSettings, ILogger<MailController> logger, IOptions<CampaignsConfig> campaignsConfig)
        {
            _kafkaProducer = kafkaProducer;
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
            _campaignsConfig = new CampaignsConfig();
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] MailRequest request)
        {
            try
            {
                var emailId = Guid.NewGuid();
                var emailLog = new EmailLog
                {
                    EmailId = emailId.ToString(),
                    ToEmail = request.ToEmail,
                    Subject = request.Subject,
                    Body = request.Body,
                    ImageUrl = request.ImageUrl,
                    Status = "Sent"
                };

                var baseUrl = _smtpSettings.BaseUrl.EndsWith("/") ? _smtpSettings.BaseUrl : $"{_smtpSettings.BaseUrl}/";
                var unsubscribeUrl = $"{baseUrl}api/mail/unsubscribe/{emailId}";

                // 👉 Convert markdown + tracking into HTML
                var htmlBody = ConstructHtmlEmailTemplateFromMarkdown(request.Body, emailId, unsubscribeUrl);

                using var client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port)
                {
                    EnableSsl = _smtpSettings.EnableSsl,
                    Credentials = new System.Net.NetworkCredential(_smtpSettings.UserName, _smtpSettings.Password)
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_smtpSettings.UserName),
                    Subject = request.Subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                mailMessage.To.Add(request.ToEmail);

                await client.SendMailAsync(mailMessage);
                EmailRepository.AddEmail(emailLog);

                await _kafkaProducer.SendEmailSentEventAsync(emailId.ToString(), request.ToEmail, request.Subject);

                _logger.LogInformation($"📧 Email sent to {request.ToEmail} with ID {emailId}");
                return Ok(new
                {
                    Success = true,
                    EmailId = emailId,
                    Message = "Email sent and tracked via Kafka",
                    TrackingUrl = $"/api/mail/track/{emailId}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to send email to {request.ToEmail}");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }
        private string ConstructHtmlEmailTemplateFromMarkdown(string markdown, Guid traceId, string unsubscribeUrl)
        {
            var unsubscribeLinkHtml = $"<a href='{unsubscribeUrl}' target='_blank'>Unsubscribe</a>";

            // Hardcoded pixel tracking URL using your format
            var pixelUrl = $"https://mailtracker-bmbjeehyg7gwandq.centralindia-01.azurewebsites.net/api/Mail/click/{traceId}?redirect=swagger";

            var trackingPixel = $"<a href='https://mailtracker-bmbjeehyg7gwandq.centralindia-01.azurewebsites.net/api/mail/track/{traceId}?redirect=swagger'>" +
                                $"<img src='{pixelUrl}' alt='Tracking Pixel111'/></a>";

            var htmlContent = MarkdownHelper.ToHtml(markdown);

            return htmlContent + $"<br><br>{unsubscribeLinkHtml}<br>{trackingPixel}";
        }



        [HttpGet("track/{emailId}")]
        public async Task<IActionResult> TrackEmail(string emailId)
        {
            try
            {
                var userAgent = Request.Headers.UserAgent.ToString();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                await _kafkaProducer.SendEmailOpenedEventAsync(emailId, userAgent, ipAddress);

                // Return a 1x1 pixel image
                var pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==");
                return File(pixel, "image/gif");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to track email {emailId}");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("click/{emailId}")]
        public async Task<IActionResult> ClickEmail(string emailId, [FromQuery] string redirect = "swagger")
        {
            try
            {
                var userAgent = Request.Headers.UserAgent.ToString();
                var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                // Send click event to Kafka
                await _kafkaProducer.SendEmailEventAsync(new EmailTrackingEvent
                {
                    EmailId = emailId,
                    EventType = "clicked",
                    UserAgent = userAgent,
                    IpAddress = ipAddress,
                    Metadata = { { "tracking_method", "image_click" } }
                });

                // Ensure BaseUrl ends with a slash
                var baseUrl = _smtpSettings.BaseUrl.EndsWith("/") ? _smtpSettings.BaseUrl : $"{_smtpSettings.BaseUrl}/";

                // Redirect to the specified URL
                var redirectUrl = redirect.ToLower() == "stats"
                    ? $"{baseUrl}api/mail/stats"
                    : $"{baseUrl}swagger";
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Failed to track click for email {emailId}");
                return StatusCode(500, new { Success = false, Error = ex.Message });
            }
        }

        [HttpGet("logs")]
        public IActionResult GetLogs()
        {
            var logs = EmailRepository.GetAll();
            return Ok(new { Success = true, Logs = logs });
        }

        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var stats = EmailRepository.GetStatistics();
            return Ok(new { Success = true, Stats = stats });
        }
    }
}