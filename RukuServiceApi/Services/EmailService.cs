using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RukuServiceApi.Models;

namespace RukuServiceApi.Services;

public class EmailService
{
    private const string HttpClientName = "Resend";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailSettings> emailSettings,
        ILogger<EmailService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = emailSettings.Value;
        _logger = logger;
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

        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        );

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ResendApiKey);

        using var response = await httpClient.SendAsync(request);

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
