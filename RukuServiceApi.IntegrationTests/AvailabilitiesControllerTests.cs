using System.Text.Json;

namespace RukuServiceApi.IntegrationTests;

[TestClass]
public sealed class AvailabilitiesControllerTests
{
    private static readonly HttpClient Client = TestHelpers.GetClient();
    private static string? _adminToken;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext context)
    {
        _adminToken = await TestHelpers.GetAdminTokenAsync();
    }

    [TestMethod]
    public async Task GetAllAvailabilities_WithAuth_ShouldReturnList()
    {
        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, "/api/availabilities", _adminToken);
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var availabilities = JsonDocument.Parse(content);

        Assert.IsNotNull(availabilities);
    }

    [TestMethod]
    public async Task GetAllAvailabilities_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/availabilities");

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAvailableDates_WithAuth_ShouldReturnDates()
    {
        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, "/api/availabilities/dates", _adminToken);
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var dates = JsonDocument.Parse(content);

        Assert.IsNotNull(dates);
    }

    [TestMethod]
    public async Task GetAvailableDates_WithoutAuth_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/availabilities/dates");

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAvailableServices_WithDate_ShouldReturnServices()
    {
        var futureDate = DateTime.Now.AddDays(30).ToString("yyyy-MM-dd");
        var request = TestHelpers.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/availabilities/services?date={futureDate}",
            _adminToken
        );
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        var services = JsonDocument.Parse(content);

        Assert.IsNotNull(services);
    }

    [TestMethod]
    public async Task GetTimeSlots_WithValidRequest_ShouldReturnTimeSlots()
    {
        var timeslotRequest = new
        {
            date = DateTime.Now.AddDays(30),
            services = new[] { "Web Development", "Mobile App Development" },
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities/timeslots", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(timeslotRequest);
        var response = await Client.SendAsync(request);

        Assert.IsTrue(
            response.StatusCode == System.Net.HttpStatusCode.OK
                || response.StatusCode == System.Net.HttpStatusCode.NotFound
        );
    }

    [TestMethod]
    public async Task CreateAvailability_WithValidData_ShouldReturnCreated()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(1),
            endDate = DateTime.Now.AddDays(7),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00", "10:00", "11:00", "14:00", "15:00" },
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(availability);
        var response = await Client.SendAsync(request);

        Assert.IsTrue(
            response.StatusCode == System.Net.HttpStatusCode.Created
                || response.StatusCode == System.Net.HttpStatusCode.Conflict
        );
    }

    [TestMethod]
    public async Task CreateAvailability_WithoutAuth_ShouldReturnUnauthorized()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(1),
            endDate = DateTime.Now.AddDays(7),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00", "10:00", "11:00" },
        };

        var content = TestHelpers.CreateJsonContent(availability);
        var response = await Client.PostAsync("/api/availabilities", content);

        Assert.AreEqual(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAvailabilityById_WithValidId_ShouldReturnAvailability()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(10),
            endDate = DateTime.Now.AddDays(17),
            services = new[] { "Mobile App Development" },
            timeslots = new[] { "09:00", "10:00" },
        };

        var createRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        createRequest.Content = TestHelpers.CreateJsonContent(availability);
        var createResponse = await Client.SendAsync(createRequest);

        if (createResponse.IsSuccessStatusCode)
        {
            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdAvailability = JsonDocument.Parse(createResponseContent);
            var availabilityId = createdAvailability.RootElement.GetProperty("id").GetInt32();

            var getRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Get, $"/api/availabilities/{availabilityId}", _adminToken);
            var getResponse = await Client.SendAsync(getRequest);
            getResponse.EnsureSuccessStatusCode();

            var getContent = await getResponse.Content.ReadAsStringAsync();
            var fetchedAvailability = JsonDocument.Parse(getContent);

            Assert.AreEqual(
                availabilityId,
                fetchedAvailability.RootElement.GetProperty("id").GetInt32()
            );
        }
    }

    [TestMethod]
    public async Task UpdateAvailability_WithValidData_ShouldReturnOk()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(20),
            endDate = DateTime.Now.AddDays(27),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00", "10:00" },
        };

        var createRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        createRequest.Content = TestHelpers.CreateJsonContent(availability);
        var createResponse = await Client.SendAsync(createRequest);

        if (createResponse.IsSuccessStatusCode)
        {
            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdAvailability = JsonDocument.Parse(createResponseContent);
            var availabilityId = createdAvailability.RootElement.GetProperty("id").GetInt32();

            var updateAvailability = new
            {
                id = availabilityId,
                startDate = DateTime.Now.AddDays(21),
                endDate = DateTime.Now.AddDays(26),
                services = new[] { "Mobile App Development" },
                timeslots = new[] { "14:00", "15:00" },
            };

            var updateRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Put, $"/api/availabilities/{availabilityId}", _adminToken);
            updateRequest.Content = TestHelpers.CreateJsonContent(updateAvailability);
            var updateResponse = await Client.SendAsync(updateRequest);

            updateResponse.EnsureSuccessStatusCode();
        }
    }

    [TestMethod]
    public async Task DeleteAvailability_WithValidId_ShouldReturnNoContent()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(30),
            endDate = DateTime.Now.AddDays(37),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00" },
        };

        var createRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        createRequest.Content = TestHelpers.CreateJsonContent(availability);
        var createResponse = await Client.SendAsync(createRequest);

        if (createResponse.IsSuccessStatusCode)
        {
            var createResponseContent = await createResponse.Content.ReadAsStringAsync();
            var createdAvailability = JsonDocument.Parse(createResponseContent);
            var availabilityId = createdAvailability.RootElement.GetProperty("id").GetInt32();

            var deleteRequest = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Delete, $"/api/availabilities/{availabilityId}", _adminToken);
            var deleteResponse = await Client.SendAsync(deleteRequest);

            Assert.AreEqual(System.Net.HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }
    }

    [TestMethod]
    public async Task CreateAvailability_WithPastStartDate_ShouldReturnBadRequest()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(-1),
            endDate = DateTime.Now.AddDays(5),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00" },
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(availability);
        var response = await Client.SendAsync(request);

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task CreateAvailability_WithEndDateBeforeStartDate_ShouldReturnBadRequest()
    {
        var availability = new
        {
            startDate = DateTime.Now.AddDays(10),
            endDate = DateTime.Now.AddDays(5),
            services = new[] { "Web Development" },
            timeslots = new[] { "09:00" },
        };

        var request = TestHelpers.CreateAuthenticatedRequest(HttpMethod.Post, "/api/availabilities", _adminToken);
        request.Content = TestHelpers.CreateJsonContent(availability);
        var response = await Client.SendAsync(request);

        Assert.AreEqual(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
