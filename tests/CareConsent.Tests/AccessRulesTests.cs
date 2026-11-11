using System.Net;
using CareConsent.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CareConsent.Tests;

// Integration tests for who may see what.
// With cookie login, "not logged in" is a redirect to the login page and
// "logged in but not allowed" (403) is a redirect to the access denied page.
public class AccessRulesTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private const string LoginPath = "/Identity/Account/Login";
    private const string AccessDeniedPath = "/Identity/Account/AccessDenied";

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static void AssertRedirectsTo(string path, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(path, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Home_page_is_open_to_everyone()
    {
        var response = await factory.NewClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/Study")]
    [InlineData("/Study/Consent")]
    [InlineData("/Study/Record/1")]
    [InlineData("/Researcher")]
    public async Task Anonymous_user_is_redirected_to_login(string url)
    {
        var response = await factory.NewClient().GetAsync(url);
        AssertRedirectsTo(LoginPath, response);
    }

    [Fact]
    public async Task Participant_is_blocked_from_researcher_pages()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email, Roles.Participant);
        var client = await factory.LoginAsync(email);

        AssertRedirectsTo(AccessDeniedPath, await client.GetAsync("/Researcher"));
    }

    [Fact]
    public async Task Researcher_can_open_researcher_pages_but_not_the_participant_flow()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email, Roles.Researcher);
        var client = await factory.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Researcher")).StatusCode);
        AssertRedirectsTo(AccessDeniedPath, await client.GetAsync("/Study"));
    }

    [Fact]
    public async Task Participant_can_open_own_record()
    {
        var email = NewEmail();
        var userId = await factory.CreateUserAsync(email, Roles.Participant);
        var recordId = await factory.AddCompletedEnrollmentAsync(userId, "Owner Name");
        var client = await factory.LoginAsync(email);

        var response = await client.GetAsync($"/Study/Record/{recordId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Owner Name", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Participant_cannot_open_another_participants_record()
    {
        var emailA = NewEmail();
        await factory.CreateUserAsync(emailA, Roles.Participant);
        var userB = await factory.CreateUserAsync(NewEmail(), Roles.Participant);
        var recordOfB = await factory.AddCompletedEnrollmentAsync(userB, "Private Name Of B");
        var clientA = await factory.LoginAsync(emailA);

        var response = await clientA.GetAsync($"/Study/Record/{recordOfB}");

        AssertRedirectsTo(AccessDeniedPath, response);
        Assert.DoesNotContain("Private Name Of B", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Record_that_does_not_exist_gets_the_same_answer_as_someone_elses()
    {
        var email = NewEmail();
        await factory.CreateUserAsync(email, Roles.Participant);
        var client = await factory.LoginAsync(email);

        AssertRedirectsTo(AccessDeniedPath, await client.GetAsync("/Study/Record/999999"));
    }

    [Fact]
    public async Task Participant_only_sees_own_enrollment_on_the_study_pages()
    {
        // B has finished the study. A has not started, so A must see "Take part",
        // not B's confirmation.
        var emailA = NewEmail();
        await factory.CreateUserAsync(emailA, Roles.Participant);
        var userB = await factory.CreateUserAsync(NewEmail(), Roles.Participant);
        await factory.AddCompletedEnrollmentAsync(userB, "Private Name Of B");
        var clientA = await factory.LoginAsync(emailA);

        var studyPage = await clientA.GetStringAsync("/Study");
        Assert.Contains("Take part", studyPage);
        // A has no enrollment, so the confirmation page sends A back to the study page.
        var confirmation = await clientA.GetAsync("/Study/Confirmation");
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
        Assert.EndsWith("/Study", confirmation.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task New_registration_gets_the_participant_role_only()
    {
        var email = NewEmail();
        var client = factory.NewClient();
        var page = await client.GetStringAsync("/Identity/Account/Register");
        var response = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = TestAppFactory.Password,
            ["Input.ConfirmPassword"] = TestAppFactory.Password,
            ["__RequestVerificationToken"] = TestAppFactory.AntiForgeryToken(page)
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = await users.GetRolesAsync((await users.FindByEmailAsync(email))!);
        Assert.Equal([Roles.Participant], roles);

        // The new account is logged in and can open the study, but not the dashboard.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Study")).StatusCode);
        AssertRedirectsTo(AccessDeniedPath, await client.GetAsync("/Researcher"));
    }
}
