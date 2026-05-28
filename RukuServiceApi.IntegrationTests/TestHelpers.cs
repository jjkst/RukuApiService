using System.Text;
using System.Text.Json;

namespace RukuServiceApi.IntegrationTests;

public static class TestHelpers
{
    private const string BaseUrl = "http://localhost:5002";
    private static readonly HttpClient Client = new() { BaseAddress = new Uri(BaseUrl) };
    private static string? _adminToken;
    private static string? _ownerToken;
    private static string? _subscriberToken;
    private static string? _subscriberUid;

    public static HttpClient GetClient() => Client;

    public static async Task<string?> GetAdminTokenAsync()
    {
        if (_adminToken != null)
            return _adminToken;

        var loginRequest = new { email = "admin@rukuit.com", uid = "admin-uid-12345" };

        var response = await Client.PostAsync(
            "/api/auth/login",
            new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"
            )
        );

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            _adminToken = json.RootElement.GetProperty("token").GetString();
        }

        return _adminToken;
    }

    public static async Task<(string? token, string? uid)> GetSubscriberAsync()
    {
        if (_subscriberToken != null)
            return (_subscriberToken, _subscriberUid);

        var uid = $"sub-test-{Guid.NewGuid():N}";
        var registerRequest = new
        {
            email = $"subscriber-{Guid.NewGuid():N}@test.local",
            uid,
            displayName = "Test Subscriber",
            emailVerified = true,
            provider = 5
        };

        var response = await Client.PostAsync(
            "/api/auth/register",
            CreateJsonContent(registerRequest)
        );

        if (!response.IsSuccessStatusCode)
            return (null, null);

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);
        _subscriberToken = json.RootElement.GetProperty("token").GetString();
        _subscriberUid = uid;
        return (_subscriberToken, _subscriberUid);
    }

    public static async Task<string?> GetOwnerTokenAsync()
    {
        if (_ownerToken != null)
            return _ownerToken;

        var loginRequest = new { email = "owner@rukuit.com", uid = "owner-uid-67890" };

        var response = await Client.PostAsync(
            "/api/auth/login",
            new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"
            )
        );

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonDocument.Parse(content);
            _ownerToken = json.RootElement.GetProperty("token").GetString();
        }

        return _ownerToken;
    }

    public static HttpRequestMessage CreateAuthenticatedRequest(
        HttpMethod method,
        string endpoint,
        string? token
    )
    {
        var request = new HttpRequestMessage(method, endpoint);
        if (token != null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                token
            );
        }
        return request;
    }

    public static StringContent CreateJsonContent<T>(T obj)
    {
        return new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");
    }
}
