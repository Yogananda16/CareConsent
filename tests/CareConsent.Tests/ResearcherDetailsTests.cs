using System.Net;
using CareConsent.Web.Models;

namespace CareConsent.Tests;

// Integration tests for the researcher-only page /Researcher/Details/{id}.
public class ResearcherDetailsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static string NewEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.test";

    private async Task<HttpClient> LoginAsResearcherAsync()
    {
        var email = NewEmail("researcher");
        await factory.CreateUserAsync(email, Roles.Researcher);
        return await factory.LoginAsync(email);
    }

    // Adds a participant with an enrollment. Answers are for the seeded
    // questions 1 to 5, in order.
    private async Task<(string Email, int EnrollmentId)> AddParticipantAsync(
        EnrollmentStatus status, string typedName = "Test Name", params string[] answers)
    {
        var email = NewEmail("participant");
        var userId = await factory.CreateUserAsync(email, Roles.Participant);
        var enrollmentId = await factory.AddEnrollmentAsync(userId, status, typedName);

        await factory.QueryAsync(db =>
        {
            for (var i = 0; i < answers.Length; i++)
            {
                db.ScreeningAnswers.Add(new ScreeningAnswer
                {
                    EnrollmentId = enrollmentId,
                    ScreeningQuestionId = i + 1,
                    Value = answers[i]
                });
            }
            return db.SaveChangesAsync();
        });

        return (email, enrollmentId);
    }

    [Fact]
    public async Task Researcher_sees_enrollment_consent_and_answers()
    {
        var (email, id) = await AddParticipantAsync(EnrollmentStatus.Completed, "Test Name", "Yes", "7", "4", "No", "A unique synthetic note");
        var client = await LoginAsResearcherAsync();

        var response = await client.GetAsync($"/Researcher/Details/{id}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(email, html);
        Assert.Contains($"<h1>Enrollment {id}</h1>", html);
        Assert.Contains("id=\"detail-status\">Completed<", html);
        Assert.Contains("id=\"detail-consent\">Given<", html);
        Assert.Contains("id=\"detail-consent-version\">1.0<", html);
        // All five questions are listed, each with its answer.
        Assert.Contains("Are you 18 years of age or older?", html);
        Assert.Contains("how many glasses of water", html);
        Assert.Contains("rate your sleep", html);
        Assert.Contains("take a walk", html);
        Assert.Contains("anything else you would like to tell", html);
        Assert.Matches(@"how many glasses of water[^<]*</td>\s*<td>\s*7\s*</td>", html);
        Assert.Contains("A unique synthetic note", html);
    }

    [Fact]
    public async Task Details_do_not_show_the_typed_signature_or_login_data()
    {
        var (_, id) = await AddParticipantAsync(EnrollmentStatus.Completed, "Very Private Signature", "Yes", "7", "4", "No");
        var client = await LoginAsResearcherAsync();

        var html = await client.GetStringAsync($"/Researcher/Details/{id}");

        Assert.DoesNotContain("Very Private Signature", html);
        Assert.DoesNotContain("PasswordHash", html);
        Assert.DoesNotContain("SecurityStamp", html);
    }

    [Fact]
    public async Task Enrollment_without_consent_or_answers_is_shown_safely()
    {
        var (email, id) = await AddParticipantAsync(EnrollmentStatus.Started);
        var client = await LoginAsResearcherAsync();

        var html = await client.GetStringAsync($"/Researcher/Details/{id}");

        Assert.Contains(email, html);
        Assert.Contains("id=\"detail-status\">Started<", html);
        Assert.Contains("id=\"detail-consent\">Not given<", html);
        Assert.Equal(5, html.Split("No answer").Length - 1); // one per question
    }

    [Fact]
    public async Task Text_typed_by_a_participant_is_encoded_not_run()
    {
        const string attack = "<script>alert('xss')</script>";
        var (_, id) = await AddParticipantAsync(EnrollmentStatus.Completed, "Test Name", "Yes", "7", "4", "No", attack);
        var client = await LoginAsResearcherAsync();

        var html = await client.GetStringAsync($"/Researcher/Details/{id}");

        Assert.DoesNotContain("<script>alert(", html);
        Assert.Contains("&lt;script&gt;alert(", html);
    }

    [Theory]
    [InlineData("/Researcher/Details/999999")] // no such enrollment
    [InlineData("/Researcher/Details/0")]
    [InlineData("/Researcher/Details/-1")]
    [InlineData("/Researcher/Details/abc")]    // not a number
    [InlineData("/Researcher/Details")]        // no ID at all
    public async Task Unknown_or_invalid_id_gets_404(string url)
    {
        var client = await LoginAsResearcherAsync();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_is_denied_even_for_own_enrollment()
    {
        var (email, id) = await AddParticipantAsync(EnrollmentStatus.Completed);
        var client = await factory.LoginAsync(email);

        var response = await client.GetAsync($"/Researcher/Details/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Anonymous_user_is_sent_to_login()
    {
        var (email, id) = await AddParticipantAsync(EnrollmentStatus.Completed);

        var response = await factory.NewClient().GetAsync($"/Researcher/Details/{id}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Identity/Account/Login", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Dashboard_links_each_row_to_its_details_page()
    {
        var (email, id) = await AddParticipantAsync(EnrollmentStatus.Consented);
        var client = await LoginAsResearcherAsync();

        var html = await client.GetStringAsync("/Researcher");

        Assert.Contains($"<td>{email}</td>", html); // email is still shown
        Assert.Contains($"href=\"/Researcher/Details/{id}\">View Details</a>", html);
    }
}
