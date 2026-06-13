using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using RukuServiceApi.Services;

namespace RukuServiceApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController(EmailService emailService, IOptions<EmailSettings> emailSettings) : ControllerBase
{
    [HttpPost("send")]
    [Authorize(Policy = AuthorizationPolicies.AuthenticatedUser)]
    [EnableRateLimiting("EmailLimit")]
    public async Task<IActionResult> SendEmail([FromBody] Contact contact)
    {
        if (string.IsNullOrWhiteSpace(contact.Email) || string.IsNullOrWhiteSpace(contact.Questions))
            return BadRequest(new { message = "Email and question are required" });

        try
        {
            var success = await emailService.SendEmailAsync(
                $"New message from {contact.FirstName} {contact.LastName}",
                $@"
                    <h2>New message from {contact.FirstName} {contact.LastName}</h2>
                    <p><strong>Email:</strong> {contact.Email}</p>
                    <p><strong>Message:</strong></p>
                    <p>{contact.Questions}</p>
                "
            );

            if (success)
                return Ok(new { message = "Email sent successfully" });
            else
                return BadRequest(new { message = "Failed to send email. Check email service configuration." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("settings")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public IActionResult GetEmailSettings()
    {
        var settings = emailSettings.Value;
        return Ok(new
        {
            recipientEmail = settings.RecipientEmail,
            apiConfigured = !string.IsNullOrWhiteSpace(settings.ResendApiKey)
                            && settings.ResendApiKey != "test_key",
        });
    }
}
