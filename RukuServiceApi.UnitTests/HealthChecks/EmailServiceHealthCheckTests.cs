using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RukuServiceApi.HealthChecks;
using RukuServiceApi.Models;

namespace RukuServiceApi.UnitTests.HealthChecks;

[TestClass]
public sealed class EmailServiceHealthCheckTests
{
    private Mock<ILogger<EmailServiceHealthCheck>> _loggerMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _loggerMock = new Mock<ILogger<EmailServiceHealthCheck>>();
    }

    private EmailServiceHealthCheck CreateHealthCheck(EmailSettings settings)
    {
        var options = Options.Create(settings);
        return new EmailServiceHealthCheck(options, _loggerMock.Object);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithCompleteSettings_ShouldReturnHealthy()
    {
        var healthCheck = CreateHealthCheck(new EmailSettings
        {
            ResendApiKey = "test-api-key",
            RecipientEmail = "recipient@example.com"
        });
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("configured");
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithEmptyRecipientEmail_ShouldReturnDegraded()
    {
        var healthCheck = CreateHealthCheck(new EmailSettings
        {
            ResendApiKey = "test-api-key",
            RecipientEmail = string.Empty
        });
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("incomplete");
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithEmptyResendApiKey_ShouldReturnDegraded()
    {
        var healthCheck = CreateHealthCheck(new EmailSettings
        {
            ResendApiKey = string.Empty,
            RecipientEmail = "recipient@example.com"
        });
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [TestMethod]
    public async Task CheckHealthAsync_WithCompleteSettings_ShouldIncludeDataFields()
    {
        var healthCheck = CreateHealthCheck(new EmailSettings
        {
            ResendApiKey = "test-api-key",
            RecipientEmail = "recipient@example.com"
        });
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        result.Data.Should().ContainKey("recipientEmail");
        result.Data.Should().ContainKey("resendApiKey");
        result.Data.Should().ContainKey("timestamp");
    }
}
