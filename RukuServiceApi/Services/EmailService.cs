using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public class EmailService
{
    private readonly HttpClient _httpClient;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _settings = emailSettings.Value;
        _logger = logger;

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

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Resend send failed: {StatusCode} {ReasonPhrase} — {Body}",
                (int)response.StatusCode,
                response.ReasonPhrase,
                responseBody
            );
        }

        return response.IsSuccessStatusCode;
    }
}