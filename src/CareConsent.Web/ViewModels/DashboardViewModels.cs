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

public record EnrollmentRow(
    int Id,
    string Email,
    EnrollmentStatus Status,
    DateTime StartedAt,
    DateTime? ConsentedAt,
    DateTime? CompletedAt);
