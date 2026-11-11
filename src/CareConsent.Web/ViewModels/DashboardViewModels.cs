using CareConsent.Web.Models;

namespace CareConsent.Web.ViewModels;

public class DashboardViewModel
{
    public required string StudyTitle { get; set; }

    // Each count is the number of enrollments currently in that status.
    public int Started { get; set; }
    public int Consented { get; set; }
    public int Completed { get; set; }
    public int Total => Started + Consented + Completed;

    public List<EnrollmentRow> Enrollments { get; set; } = [];
}

// What a researcher sees about one enrollment. It has no typed signature
// and nothing from the login tables except the email.
public class EnrollmentDetailsViewModel
{
    public int Id { get; set; }
    public int StudyId { get; set; }
    public string StudyTitle { get; set; } = "";
    public string Email { get; set; } = "";
    public EnrollmentStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? ConsentedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool ConsentGiven { get; set; }
    public string? ConsentVersion { get; set; }
    public List<QuestionAnswer> Answers { get; set; } = [];
}

// Answer is null when the participant gave no answer to the question.
public record QuestionAnswer(string Question, string? Answer);

public record EnrollmentRow(
    int Id,
    string Email,
    EnrollmentStatus Status,
    DateTime StartedAt,
    DateTime? ConsentedAt,
    DateTime? CompletedAt);
