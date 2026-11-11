using System.Globalization;
using CareConsent.Web.Models;

namespace CareConsent.Web.Services;

// The consent and screening rules, kept apart from the controller so they
// are easy to unit test.
public static class ParticipantRules
{
    // Consent can be given once, right after starting.
    public static bool CanConsent(Enrollment enrollment) =>
        enrollment.Status == EnrollmentStatus.Started;

    // No answers before consent, and no second set of answers after finishing.
    public static bool CanAnswer(Enrollment enrollment) =>
        enrollment.Status == EnrollmentStatus.Consented
        && enrollment.ConsentRecord is { Agreed: true };

    // Returns an error message, or null when the answer is valid.
    public static string? ValidateAnswer(ScreeningQuestion question, string? rawAnswer)
    {
        var value = rawAnswer?.Trim() ?? "";

        switch (question.Type)
        {
            case QuestionType.YesNo:
                return value is "Yes" or "No" ? null : "Choose Yes or No.";

            case QuestionType.Number:
                return IsWholeNumberBetween(value, 0, 50) ? null : "Enter a whole number from 0 to 50.";

            case QuestionType.Scale:
                return IsWholeNumberBetween(value, 1, 5) ? null : "Choose a number from 1 to 5.";

            default: // Text is optional.
                return value.Length <= 1000 ? null : "Keep your answer under 1000 characters.";
        }
    }

    private static bool IsWholeNumberBetween(string value, int min, int max) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
        && number >= min && number <= max;
}
