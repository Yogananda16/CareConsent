using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CareConsent.Web.Models;

namespace CareConsent.Tests;

// Tests for GET /api/study/stats. Each test starts its own app with its own
// empty database, so the counts are exact.
public class StatsApiTests
{
    private const string Url = "/api/study/stats";

    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    private static async Task<HttpClient> LoginAsync(TestAppFactory app, string role)
    {
        var email = NewEmail(role.ToLowerInvariant());
        await app.CreateUserAsync(email, role);
        return await app.LoginAsync(email);
    }

    private static async Task AddParticipantAsync(TestAppFactory app, EnrollmentStatus status, string typedName = "Test Name")
    {
        var userId = await app.CreateUserAsync(NewEmail("participant"), Roles.Participant);
        await app.AddEnrollmentAsync(userId, status, typedName);
    }

    [Fact]
    public async Task Researcher_gets_json_with_correct_counts()
    {
        await using var app = new TestAppFactory();
        await AddParticipantAsync(app, EnrollmentStatus.Started);
        for (var i = 0; i < 2; i++) await AddParticipantAsync(app, EnrollmentStatus.Consented);
        for (var i = 0; i < 3; i++) await AddParticipantAsync(app, EnrollmentStatus.Completed);
        var client = await LoginAsync(app, Roles.Researcher);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("started").GetInt32());
        Assert.Equal(2, json.GetProperty("consented").GetInt32());
        Assert.Equal(3, json.GetProperty("completed").GetInt32());
        Assert.Equal(1, json.GetProperty("studyId").GetInt32());
        Assert.Contains("(Fictional)", json.GetProperty("studyTitle").GetString());
    }

    [Fact]
    public async Task Empty_study_returns_zero_counts()
    {
        await using var app = new TestAppFactory();
        var client = await LoginAsync(app, Roles.Researcher);

        var json = await client.GetFromJsonAsync<JsonElement>(Url);

        Assert.Equal(0, json.GetProperty("total").GetInt32());
        Assert.Equal(0, json.GetProperty("started").GetInt32());
        Assert.Equal(0, json.GetProperty("consented").GetInt32());
        Assert.Equal(0, json.GetProperty("completed").GetInt32());
    }

    [Fact]
    public async Task Response_contains_counts_only_and_no_participant_data()
    {
        await using var app = new TestAppFactory();
        var participantEmail = NewEmail("participant");
        var userId = await app.CreateUserAsync(participantEmail, Roles.Participant);
        await app.AddEnrollmentAsync(userId, EnrollmentStatus.Completed, "Very Private Signature");
        var client = await LoginAsync(app, Roles.Researcher);

        var body = await client.GetStringAsync(Url);

        var fields = JsonDocument.Parse(body).RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(["completed", "consented", "started", "studyId", "studyTitle", "total"], fields);
        Assert.DoesNotContain(participantEmail, body);
        Assert.DoesNotContain("Very Private Signature", body);
    }

    [Fact]
    public async Task Anonymous_user_gets_401_and_no_statistics()
    {
        await using var app = new TestAppFactory();
        await AddParticipantAsync(app, EnrollmentStatus.Completed);

        var response = await app.NewClient().GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain("total", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Participant_gets_403_and_no_statistics()
    {
        await using var app = new TestAppFactory();
        await AddParticipantAsync(app, EnrollmentStatus.Completed);
        var client = await LoginAsync(app, Roles.Participant);

        var response = await client.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("total", await response.Content.ReadAsStringAsync());
    }
}
