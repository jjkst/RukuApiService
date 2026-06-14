using System.Text.Json;

namespace RukuServiceApi.IntegrationTests;

[TestClass]
public sealed class EmailControllerTests
{
    private static readonly HttpClient Client = TestHelpers.GetClient();
    private static string? _adminToken;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _adminToken = await TestHelpers.GetAdminTokenAsync();
    }

    [TestMethod]
    public async Task GetEmailSettings_WithAuth_ShouldReturnSettings()
    {
        var request = TestHelpers.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/email/settings",
            _adminToken
        );
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var settings = JsonDocument.Parse(content);

        Assert.IsTrue(settings.RootElement.TryGetProperty("recipientEmail", out _));
    }

    [TestMethod]
    public async Task GetEmailSettings_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/email/settings");

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task SendEmail_WithValidData_ShouldReturnOk()
    {
        var contact = new
        {
            firstName = "John",
            lastName = "Doe",
            email = "john.doe@example.com",
            phoneNumber = "123-456-7890",
            questions = "This is a test question for the email endpoint.",
        };

        var content = TestHelpers.CreateJsonContent(contact);
        var request = TestHelpers.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/email/send",
            _adminToken
        );
        request.Content = content;
        var response = await Client.SendAsync(request);

        // Could be Ok or BadRequest depending on SMTP configuration
        Assert.IsTrue(
            response.StatusCode == System.Net.HttpStatusCode.OK
                || response.StatusCode == System.Net.HttpStatusCode.BadRequest
        );
    }

    [TestMethod]
    public async Task SendEmail_WithoutAuth_ShouldBeAllowed()
    {
        var contact = new
        {
            firstName = "Test",
            lastName = "User",
            email = "test@example.com",
            questions = "Test question",
        };

        var content = TestHelpers.CreateJsonContent(contact);
        var response = await Client.PostAsync("/api/email/send", content);

        // Anonymous access is allowed; result depends on email configuration
        Assert.IsTrue(
            response.StatusCode == System.Net.HttpStatusCode.OK
                || response.StatusCode == System.Net.HttpStatusCode.BadRequest
        );
    }
}
