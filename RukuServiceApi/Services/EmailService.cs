using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public class EmailService
{
    private readonly HttpClient _httpClient;
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> emailSettings)
    {
        _settings = emailSettings.Value;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add(
            "Authorization",
            $"Bearer {_settings.ResendApiKey}"
        );
    }

    public async Task<bool> SendEmailAsync(string subject, string body)
    {
        var payload = new
        {
            from = "onboarding@resend.dev",
            to = new[] { _settings.RecipientEmail },
            subject = subject,
            html = body
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _httpClient.PostAsync(
            "https://api.resend.com/emails",
            content
        );

        return response.IsSuccessStatusCode;
    }
}