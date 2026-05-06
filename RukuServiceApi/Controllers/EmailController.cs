using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace RukuServiceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController(IOptions<EmailSettings> emailSettings) : ControllerBase
    {
        private readonly EmailSettings _emailSettings = emailSettings.Value;

        [HttpPost("send")]
        [AllowAnonymous]
        [EnableRateLimiting("EmailLimit")]
        public async Task<IActionResult> SendEmail([FromBody] Contact contact)
        {
            if (string.IsNullOrWhiteSpace(contact.Email) || string.IsNullOrWhiteSpace(contact.Questions))
            {
                return BadRequest(new { message = "Email and question are required" });
            }

            try
            {
                var smtpServer = _emailSettings.SmtpServer;
                var smtpPort = _emailSettings.SmtpPort;
                var username = _emailSettings.SmtpUsername;
                var password = _emailSettings.SmtpPassword;
                var enableSsl = _emailSettings.EnableSsl;

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.Credentials = new NetworkCredential(username, password);
                    client.EnableSsl = enableSsl;
                    client.Timeout = 10000; // 10 seconds timeout

                    var message = new MailMessage
                    {
                        From = new MailAddress(username),
                        Subject = "Question from RukuService Application",
                        Body =
                            $"A new question has been submitted:<br><br>"
                            + $"<strong>Name:</strong> {WebUtility.HtmlEncode(contact.FirstName)} {WebUtility.HtmlEncode(contact.LastName)}<br>"
                            + $"<strong>Email:</strong> {WebUtility.HtmlEncode(contact.Email)}<br>"
                            + $"<strong>Phone:</strong> {WebUtility.HtmlEncode(contact.PhoneNumber)}<br>"
                            + $"<strong>Question:</strong> {WebUtility.HtmlEncode(contact.Questions)}",
                        IsBodyHtml = true,
                    };
                    message.To.Add(_emailSettings.RecipientEmail);

                    await client.SendMailAsync(message);
                }

                return Ok(new { message = "Email sent successfully" });
            }
            catch (SmtpException smtpEx)
            {
                return BadRequest(
                    new ErrorResponse
                    {
                        Message = "Failed to send email",
                        Details = $"SMTP Error: {smtpEx.Message}",
                        StatusCode = StatusCodes.Status400BadRequest,
                        CorrelationId = HttpContext.TraceIdentifier,
                        Path = HttpContext.Request.Path,
                    }
                );
            }
            catch (Exception ex)
            {
                return BadRequest(
                    new ErrorResponse
                    {
                        Message = "Error sending email",
                        Details = ex.Message,
                        StatusCode = StatusCodes.Status400BadRequest,
                        CorrelationId = HttpContext.TraceIdentifier,
                        Path = HttpContext.Request.Path,
                    }
                );
            }
        }

        [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
        [HttpGet("settings")]
        public IActionResult GetEmailSettings()
        {
            return Ok(
                new
                {
                    SmtpServer = _emailSettings.SmtpServer,
                    SmtpPort = _emailSettings.SmtpPort,
                    EnableSsl = _emailSettings.EnableSsl,
                }
            );
        }
    }
}
