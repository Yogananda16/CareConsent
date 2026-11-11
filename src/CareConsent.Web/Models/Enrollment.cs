using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CareConsent.Web.Models;

public enum EnrollmentStatus
{
    Started,
    Consented,
    Completed
}

// One participant in one study.
public class Enrollment
{
    public int Id { get; set; }

    public int StudyId { get; set; }
    public Study Study { get; set; } = null!;

    // The Identity user ID of the participant.
    [Required]
    public string ParticipantId { get; set; } = "";
    public IdentityUser Participant { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Started;

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ConsentRecord? ConsentRecord { get; set; }
    public List<ScreeningAnswer> Answers { get; set; } = [];
}
