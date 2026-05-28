using System.Text.Json;

namespace RukuServiceApi.IntegrationTests;

[TestClass]
public sealed class SchedulesControllerTests
{
    private static readonly HttpClient Client = TestHelpers.GetClient();
    private static string? _adminToken;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _adminToken = await TestHelpers.GetAdminTokenAsync();
    }

    [TestMethod]
    public async Task GetAllSchedules_WithAuth_ShouldReturnList()
    {
        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, "/api/schedules", _adminToken);
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var schedules = JsonDocument.Parse(content);

        Assert.IsNotNull(schedules);
    }

    [TestMethod]
    public async Task GetAllSchedules_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/schedules");

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateSchedule_WithValidData_ShouldReturnCreated()
    {
        var schedule = new
        {
            contactName = "Test User",
            selectedDate = DateTime.Now.AddDays(5),
            services = new[] { "Web Development" },
            timeslots = new[] { "10:00" },
            note = "Test schedule",
            uid = "test-uid-123",
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(schedule);
        var response = await Client.SendAsync(request);

        Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
        var responseContent = await response.Content.ReadAsStringAsync();
        var createdSchedule = JsonDocument.Parse(responseContent);

        Assert.IsTrue(createdSchedule.RootElement.TryGetProperty("id", out _));
    }

    [TestMethod]
    public async Task Subscriber_CreateSchedule_StampsUidFromJwt()
    {
        var (subscriberToken, subscriberUid) = await TestHelpers.GetSubscriberAsync();
        Assert.IsNotNull(subscriberToken);
        Assert.IsNotNull(subscriberUid);

        var schedule = new
        {
            contactName = "Subscriber User",
            selectedDate = DateTime.Now.AddDays(6),
            services = new[] { "Mobile App Development" },
            timeslots = new[] { "11:00" },
            note = "Subscriber schedule",
            uid = "malicious-uid"
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", subscriberToken);
        request.Content = TestHelpers.CreateJsonContent(schedule);
        var response = await Client.SendAsync(request);

        Assert.AreEqual(System.Net.HttpStatusCode.Created, response.StatusCode);
        var createdSchedule = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(subscriberUid, createdSchedule.RootElement.GetProperty("uid").GetString());
    }

    [TestMethod]
    public async Task Subscriber_GetById_OtherUsersSchedule_ReturnsNotFound()
    {
        var adminRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", _adminToken);
        adminRequest.Content = TestHelpers.CreateJsonContent(new
        {
            contactName = "Admin Owned",
            selectedDate = DateTime.Now.AddDays(7),
            services = new[] { "Web Development" },
            timeslots = new[] { "12:00" },
            note = "Admin schedule",
        });
        var adminResponse = await Client.SendAsync(adminRequest);
        adminResponse.EnsureSuccessStatusCode();
        var adminSchedule = JsonDocument.Parse(await adminResponse.Content.ReadAsStringAsync());
        var scheduleId = adminSchedule.RootElement.GetProperty("id").GetInt32();

        var (subscriberToken, _) = await TestHelpers.GetSubscriberAsync();
        var getRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/schedules/{scheduleId}", subscriberToken);
        var getResponse = await Client.SendAsync(getRequest);

        Assert.AreEqual(System.Net.HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [TestMethod]
    public async Task GetScheduleById_WithValidId_ShouldReturnSchedule()
    {
        var schedule = new
        {
            contactName = "Get Test User",
            selectedDate = DateTime.Now.AddDays(6),
            services = new[] { "Mobile App Development" },
            timeslots = new[] { "11:00" },
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(schedule);
        var createResponse = await Client.SendAsync(request);
        createResponse.EnsureSuccessStatusCode();

        var createdSchedule = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var scheduleId = createdSchedule.RootElement.GetProperty("id").GetInt32();

        var getRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/schedules/{scheduleId}", _adminToken);
        var getResponse = await Client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var getContent = await getResponse.Content.ReadAsStringAsync();
        var fetchedSchedule = JsonDocument.Parse(getContent);

        Assert.AreEqual(scheduleId, fetchedSchedule.RootElement.GetProperty("id").GetInt32());
    }

    [TestMethod]
    public async Task UpdateSchedule_WithValidData_ShouldReturnOk()
    {
        var schedule = new
        {
            contactName = "Update Test User",
            selectedDate = DateTime.Now.AddDays(7),
            services = new[] { "Web Development" },
            timeslots = new[] { "14:00" },
        };

        var createRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", _adminToken);
        createRequest.Content = TestHelpers.CreateJsonContent(schedule);
        var createResponse = await Client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdSchedule = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var scheduleId = createdSchedule.RootElement.GetProperty("id").GetInt32();

        var updateSchedule = new
        {
            id = scheduleId,
            contactName = "Updated Test User",
            selectedDate = DateTime.Now.AddDays(8),
            services = new[] { "Mobile App Development" },
            timeslots = new[] { "15:00" },
            note = "Updated note",
        };

        var updateRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Put, $"/api/schedules/{scheduleId}", _adminToken);
        updateRequest.Content = TestHelpers.CreateJsonContent(updateSchedule);
        var updateResponse = await Client.SendAsync(updateRequest);

        updateResponse.EnsureSuccessStatusCode();
    }

    [TestMethod]
    public async Task DeleteSchedule_WithValidId_ShouldReturnNoContent()
    {
        var schedule = new
        {
            contactName = "Delete Test User",
            selectedDate = DateTime.Now.AddDays(9),
            services = new[] { "Web Development" },
            timeslots = new[] { "16:00" },
        };

        var createRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/schedules", _adminToken);
        createRequest.Content = TestHelpers.CreateJsonContent(schedule);
        var createResponse = await Client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createdSchedule = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var scheduleId = createdSchedule.RootElement.GetProperty("id").GetInt32();

        var deleteRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/schedules/{scheduleId}", _adminToken);
        var deleteResponse = await Client.SendAsync(deleteRequest);

        Assert.AreEqual(System.Net.HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }
}
