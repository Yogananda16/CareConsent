using System.ComponentModel.DataAnnotations;

namespace CareConsent.Web.Models;

public class ConsentRecord
{
    public int Id { get; set; }

    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public bool Agreed { get; set; }

    [Required, MaxLength(200)]
    public string TypedName { get; set; } = "";

    public DateTime SignedAt { get; set; }

    // The version of the consent text the participant saw.
    [Required, MaxLength(20)]
    public string ConsentVersion { get; set; } = "";
}
