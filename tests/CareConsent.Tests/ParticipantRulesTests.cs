using CareConsent.Web.Models;
using CareConsent.Web.Services;

namespace CareConsent.Tests;

// Unit tests: plain function calls, no web app and no database.
public class ParticipantRulesTests
{
    private static Enrollment EnrollmentWith(EnrollmentStatus status, bool? agreed = null) => new()
    {
        Status = status,
        ConsentRecord = agreed is null ? null : new ConsentRecord { Agreed = agreed.Value }
    };

    private static ScreeningQuestion Question(QuestionType type) => new() { Type = type, Text = "Test question" };

    // ---- Consent rules ----

    [Theory]
    [InlineData(EnrollmentStatus.Started, true)]
    [InlineData(EnrollmentStatus.Consented, false)]
    [InlineData(EnrollmentStatus.Completed, false)]
    public void Consent_can_only_be_given_once_right_after_starting(EnrollmentStatus status, bool expected)
    {
        Assert.Equal(expected, ParticipantRules.CanConsent(EnrollmentWith(status)));
    }

    [Fact]
    public void Answers_are_allowed_after_consent()
    {
        Assert.True(ParticipantRules.CanAnswer(EnrollmentWith(EnrollmentStatus.Consented, agreed: true)));
    }

    [Fact]
    public void No_answers_before_consent()
    {
        Assert.False(ParticipantRules.CanAnswer(EnrollmentWith(EnrollmentStatus.Started)));
    }

    [Fact]
    public void No_answers_when_the_consent_record_is_missing()
    {
        // The status says Consented, but there is no record to back it up.
        Assert.False(ParticipantRules.CanAnswer(EnrollmentWith(EnrollmentStatus.Consented)));
    }

    [Fact]
    public void No_answers_when_the_consent_record_says_not_agreed()
    {
        Assert.False(ParticipantRules.CanAnswer(EnrollmentWith(EnrollmentStatus.Consented, agreed: false)));
    }

    [Fact]
    public void No_second_set_of_answers_after_completion()
    {
        Assert.False(ParticipantRules.CanAnswer(EnrollmentWith(EnrollmentStatus.Completed, agreed: true)));
    }

    // ---- Screening answer validation ----

    [Theory]
    [InlineData("Yes")]
    [InlineData("No")]
    [InlineData("  Yes  ")] // spaces around the answer are ignored
    public void YesNo_accepts_yes_and_no(string answer)
    {
        Assert.Null(ParticipantRules.ValidateAnswer(Question(QuestionType.YesNo), answer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yes")]   // wrong case, not a value the form can send
    [InlineData("Maybe")]
    [InlineData("1")]
    public void YesNo_rejects_anything_else(string? answer)
    {
        Assert.NotNull(ParticipantRules.ValidateAnswer(Question(QuestionType.YesNo), answer));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("50")]
    [InlineData(" 12 ")]
    public void Number_accepts_whole_numbers_from_0_to_50(string answer)
    {
        Assert.Null(ParticipantRules.ValidateAnswer(Question(QuestionType.Number), answer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("51")]
    [InlineData("3.5")]
    [InlineData("+3")]
    [InlineData("1e1")]
    [InlineData("seven")]
    [InlineData("99999999999999999999")] // too big for an int
    public void Number_rejects_everything_else(string? answer)
    {
        Assert.NotNull(ParticipantRules.ValidateAnswer(Question(QuestionType.Number), answer));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3")]
    [InlineData("5")]
    public void Scale_accepts_1_to_5(string answer)
    {
        Assert.Null(ParticipantRules.ValidateAnswer(Question(QuestionType.Scale), answer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("6")]
    [InlineData("2.5")]
    [InlineData("high")]
    public void Scale_rejects_everything_else(string? answer)
    {
        Assert.NotNull(ParticipantRules.ValidateAnswer(Question(QuestionType.Scale), answer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A short note.")]
    public void Text_is_optional(string? answer)
    {
        Assert.Null(ParticipantRules.ValidateAnswer(Question(QuestionType.Text), answer));
    }

    [Fact]
    public void Text_allows_1000_characters_but_not_1001()
    {
        var question = Question(QuestionType.Text);

        Assert.Null(ParticipantRules.ValidateAnswer(question, new string('a', 1000)));
        Assert.NotNull(ParticipantRules.ValidateAnswer(question, new string('a', 1001)));
    }
}
