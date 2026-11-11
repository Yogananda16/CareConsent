using System.Net;
using CareConsent.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CareConsent.Tests;

// Integration tests for the participant flow: real HTTP requests against the
// app, then a look in the database to see what was stored.
public class ParticipantFlowTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static readonly Dictionary<string, string> NoFields = [];

    private static Dictionary<string, string> Consent(string agreed, string name) =>
        new() { ["Agreed"] = agreed, ["TypedName"] = name };

    // Builds the questionnaire form. The seeded questions have IDs 1 to 5:
    // YesNo, Number, Scale, YesNo, Text.
    private static Dictionary<string, string> Answers(params string[] values)
    {
        var form = new Dictionary<string, string>();
        for (var i = 0; i < values.Length; i++)
        {
            form[$"Items[{i}].QuestionId"] = (i + 1).ToString();
            form[$"Items[{i}].Answer"] = values[i];
        }
        return form;
    }

    private static readonly string[] GoodAnswers = ["Yes", "6", "4", "No", "A synthetic note"];

    private async Task<(HttpClient Client, string UserId)> NewParticipantAsync()
    {
        var email = $"flow-{Guid.NewGuid():N}@example.test";
        var userId = await factory.CreateUserAsync(email, Roles.Participant);
        return (await factory.LoginAsync(email), userId);
    }

    private async Task<(HttpClient Client, string UserId)> NewConsentedParticipantAsync()
    {
        var (client, userId) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);
        await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent("true", "Test Participant"));
        return (client, userId);
    }

    private Task<Enrollment?> EnrollmentOfAsync(string userId) =>
        factory.QueryAsync(db => db.Enrollments
            .Include(e => e.ConsentRecord)
            .Include(e => e.Answers)
            .SingleOrDefaultAsync(e => e.ParticipantId == userId));

    private static void AssertRedirectsTo(string path, HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith(path, response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Full_flow_stores_consent_and_answers_and_completes_the_enrollment()
    {
        var (client, userId) = await NewParticipantAsync();

        AssertRedirectsTo("/Study/Consent", await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields));
        AssertRedirectsTo("/Study/Questionnaire", await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent("true", "  Test Participant  ")));
        AssertRedirectsTo("/Study/Confirmation", await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers(GoodAnswers)));

        var enrollment = await EnrollmentOfAsync(userId);
        Assert.NotNull(enrollment);
        Assert.Equal(EnrollmentStatus.Completed, enrollment.Status);
        Assert.NotNull(enrollment.CompletedAt);
        Assert.True(enrollment.ConsentRecord!.Agreed);
        Assert.Equal("Test Participant", enrollment.ConsentRecord.TypedName); // spaces trimmed
        Assert.Equal("1.0", enrollment.ConsentRecord.ConsentVersion);          // the version the study had
        Assert.Equal(GoodAnswers, enrollment.Answers.OrderBy(a => a.ScreeningQuestionId).Select(a => a.Value));

        var confirmation = await client.GetStringAsync("/Study/Confirmation");
        Assert.Contains("Thank you", confirmation);
        Assert.Contains("A synthetic note", confirmation);
    }

    [Fact]
    public async Task Starting_twice_creates_only_one_enrollment()
    {
        var (client, userId) = await NewParticipantAsync();

        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);

        Assert.Equal(1, await factory.QueryAsync(db => db.Enrollments.CountAsync(e => e.ParticipantId == userId)));
    }

    [Theory]
    [InlineData("false", "Test Participant", "You must tick the box")] // box not ticked
    [InlineData("true", "", "Type your name to sign.")]                // no name
    [InlineData("true", "   ", "Type your name to sign.")]             // only spaces
    [InlineData("true", "A", "2 to 200 characters")]                   // too short
    public async Task Invalid_consent_shows_an_error_and_stores_nothing(string agreed, string name, string expectedError)
    {
        var (client, userId) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent(agreed, name));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // the form is shown again
        Assert.Contains(expectedError, await response.Content.ReadAsStringAsync());
        var enrollment = await EnrollmentOfAsync(userId);
        Assert.Equal(EnrollmentStatus.Started, enrollment!.Status);
        Assert.Null(enrollment.ConsentRecord);
    }

    [Fact]
    public async Task Consent_name_longer_than_200_characters_is_rejected()
    {
        var (client, userId) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent("true", new string('a', 201)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await EnrollmentOfAsync(userId))!.ConsentRecord);
    }

    [Fact]
    public async Task Consent_without_starting_first_is_sent_back_to_the_study_page()
    {
        var (client, userId) = await NewParticipantAsync();

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent("true", "Test Participant"));

        AssertRedirectsTo("/Study", response);
        Assert.Null(await EnrollmentOfAsync(userId));
    }

    [Fact]
    public async Task Second_consent_is_refused_and_the_first_one_is_kept()
    {
        var (client, userId) = await NewConsentedParticipantAsync();

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Consent", Consent("true", "Someone Else"));

        AssertRedirectsTo("/Study/Questionnaire", response);
        Assert.Equal("Test Participant", (await EnrollmentOfAsync(userId))!.ConsentRecord!.TypedName);
        Assert.Equal(1, await factory.QueryAsync(db => db.ConsentRecords.CountAsync(c => c.Enrollment.ParticipantId == userId)));
    }

    [Fact]
    public async Task Answers_posted_before_consent_are_refused_and_not_stored()
    {
        var (client, userId) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers(GoodAnswers));

        AssertRedirectsTo("/Study/Consent", response);
        var enrollment = await EnrollmentOfAsync(userId);
        Assert.Equal(EnrollmentStatus.Started, enrollment!.Status);
        Assert.Empty(enrollment.Answers);
    }

    [Fact]
    public async Task Questionnaire_page_before_consent_redirects_to_consent()
    {
        var (client, _) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Start", NoFields);

        AssertRedirectsTo("/Study/Consent", await client.GetAsync("/Study/Questionnaire"));
        AssertRedirectsTo("/Study/Consent", await client.GetAsync("/Study/Confirmation"));
    }

    [Theory]
    [InlineData("Maybe", "6", "4", "No", "Choose Yes or No.")]   // bad yes/no
    [InlineData("Yes", "99", "4", "No", "from 0 to 50")]         // number too big
    [InlineData("Yes", "6", "9", "No", "from 1 to 5")]           // scale out of range
    [InlineData("Yes", "6", "4", "", "Choose Yes or No.")]       // required answer left empty
    public async Task One_invalid_answer_shows_an_error_and_stores_no_answers(string q1, string q2, string q3, string q4, string expectedError)
    {
        var (client, userId) = await NewConsentedParticipantAsync();

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers(q1, q2, q3, q4, "note"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedError, await response.Content.ReadAsStringAsync());
        var enrollment = await EnrollmentOfAsync(userId);
        Assert.Equal(EnrollmentStatus.Consented, enrollment!.Status); // not completed
        Assert.Empty(enrollment.Answers);                             // nothing saved, not even the valid ones
    }

    [Fact]
    public async Task Empty_questionnaire_submission_is_rejected()
    {
        var (client, userId) = await NewConsentedParticipantAsync();

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", NoFields);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await EnrollmentOfAsync(userId))!.Answers);
    }

    [Fact]
    public async Task Answers_for_question_ids_that_do_not_exist_are_ignored()
    {
        var (client, userId) = await NewConsentedParticipantAsync();
        var form = new Dictionary<string, string>
        {
            ["Items[0].QuestionId"] = "999", ["Items[0].Answer"] = "Yes",
            ["Items[1].QuestionId"] = "-1", ["Items[1].Answer"] = "6"
        };

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // real questions are still unanswered
        Assert.Empty((await EnrollmentOfAsync(userId))!.Answers);
    }

    [Fact]
    public async Task Optional_text_question_can_be_left_empty()
    {
        var (client, userId) = await NewConsentedParticipantAsync();

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers("Yes", "6", "4", "No", ""));

        AssertRedirectsTo("/Study/Confirmation", response);
        var enrollment = await EnrollmentOfAsync(userId);
        Assert.Equal(EnrollmentStatus.Completed, enrollment!.Status);
        Assert.Equal(4, enrollment.Answers.Count); // no empty row for the skipped question
    }

    [Fact]
    public async Task Second_questionnaire_submission_is_refused_and_answers_are_unchanged()
    {
        var (client, userId) = await NewConsentedParticipantAsync();
        await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers(GoodAnswers));

        var response = await TestAppFactory.PostFormAsync(client, "/Study/Questionnaire", Answers("No", "1", "1", "Yes", "changed"));

        AssertRedirectsTo("/Study/Confirmation", response);
        var enrollment = await EnrollmentOfAsync(userId);
        Assert.Equal(GoodAnswers, enrollment!.Answers.OrderBy(a => a.ScreeningQuestionId).Select(a => a.Value));
    }

    [Fact]
    public async Task Post_without_anti_forgery_token_is_rejected()
    {
        var (client, userId) = await NewParticipantAsync();

        var response = await client.PostAsync("/Study/Start", new FormUrlEncodedContent(NoFields));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(await EnrollmentOfAsync(userId));
    }

    [Fact]
    public async Task One_participants_answers_do_not_affect_another_participant()
    {
        var (clientA, userA) = await NewConsentedParticipantAsync();
        var (clientB, userB) = await NewParticipantAsync();
        await TestAppFactory.PostFormAsync(clientA, "/Study/Questionnaire", Answers(GoodAnswers));

        // B has not started. B must still see "Take part" and have no enrollment.
        Assert.Contains("Take part", await clientB.GetStringAsync("/Study"));
        Assert.Null(await EnrollmentOfAsync(userB));
        Assert.Equal(EnrollmentStatus.Completed, (await EnrollmentOfAsync(userA))!.Status);
    }
}
