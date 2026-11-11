using System.Net;
using CareConsent.Web.Models;

namespace CareConsent.Tests;

// Each test starts its own app with its own empty database, so the counts
// are exact and tests cannot affect each other.
public class DashboardTests
{
    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    private static async Task<HttpClient> LoginAsResearcherAsync(TestAppFactory app)
    {
        var email = NewEmail("researcher");
        await app.CreateUserAsync(email, Roles.Researcher);
        return await app.LoginAsync(email);
    }

    private static async Task<string> AddParticipantAsync(TestAppFactory app, EnrollmentStatus status, string typedName = "Test Name")
    {
        var email = NewEmail(status.ToString().ToLowerInvariant());
        var userId = await app.CreateUserAsync(email, Roles.Participant);
        await app.AddEnrollmentAsync(userId, status, typedName);
        return email;
    }

    [Fact]
    public async Task Empty_dashboard_shows_zero_counts()
    {
        await using var app = new TestAppFactory();
        var client = await LoginAsResearcherAsync(app);

        var html = await client.GetStringAsync("/Researcher");

        Assert.Contains("id=\"count-total\">0<", html);
        Assert.Contains("id=\"count-started\">0<", html);
        Assert.Contains("id=\"count-consented\">0<", html);
        Assert.Contains("id=\"count-completed\">0<", html);
        Assert.Contains("No one has enrolled yet.", html);
    }

    [Fact]
    public async Task Dashboard_counts_enrollments_by_status()
    {
        await using var app = new TestAppFactory();
        await AddParticipantAsync(app, EnrollmentStatus.Started);
        for (var i = 0; i < 2; i++) await AddParticipantAsync(app, EnrollmentStatus.Consented);
        for (var i = 0; i < 3; i++) await AddParticipantAsync(app, EnrollmentStatus.Completed);
        var client = await LoginAsResearcherAsync(app);

        var html = await client.GetStringAsync("/Researcher");

        Assert.Contains("id=\"count-total\">6<", html);
        Assert.Contains("id=\"count-started\">1<", html);
        Assert.Contains("id=\"count-consented\">2<", html);
        Assert.Contains("id=\"count-completed\">3<", html);
    }

    [Fact]
    public async Task Dashboard_lists_each_enrollment_with_its_status()
    {
        await using var app = new TestAppFactory();
        var startedEmail = await AddParticipantAsync(app, EnrollmentStatus.Started);
        var completedEmail = await AddParticipantAsync(app, EnrollmentStatus.Completed);
        var client = await LoginAsResearcherAsync(app);

        var html = await client.GetStringAsync("/Researcher");

        Assert.Matches($@"<td>{startedEmail}</td>\s*<td>Started</td>", html);
        Assert.Matches($@"<td>{completedEmail}</td>\s*<td>Completed</td>", html);
    }

    [Fact]
    public async Task Dashboard_does_not_show_private_participant_data()
    {
        await using var app = new TestAppFactory();
        await AddParticipantAsync(app, EnrollmentStatus.Completed, typedName: "Very Private Signature");
        var client = await LoginAsResearcherAsync(app);

        var html = await client.GetStringAsync("/Researcher");

        Assert.DoesNotContain("Very Private Signature", html);
        Assert.DoesNotContain("PasswordHash", html);
        Assert.DoesNotContain("SecurityStamp", html);
    }

    [Fact]
    public async Task Dashboard_is_closed_to_participants_and_anonymous_users()
    {
        await using var app = new TestAppFactory();
        var email = NewEmail("participant");
        await app.CreateUserAsync(email, Roles.Participant);
        var participant = await app.LoginAsync(email);

        var asParticipant = await participant.GetAsync("/Researcher");
        var asAnonymous = await app.NewClient().GetAsync("/Researcher");

        Assert.Equal(HttpStatusCode.Redirect, asParticipant.StatusCode);
        Assert.Contains("/Identity/Account/AccessDenied", asParticipant.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, asAnonymous.StatusCode);
        Assert.Contains("/Identity/Account/Login", asAnonymous.Headers.Location!.OriginalString);
    }
}
