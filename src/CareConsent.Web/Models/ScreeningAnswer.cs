using System.ComponentModel.DataAnnotations;

namespace CareConsent.Web.Models;

// One participant's answer to one question.
public class ScreeningAnswer
{
    public int Id { get; set; }

    public int EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = null!;

    public int ScreeningQuestionId { get; set; }
    public ScreeningQuestion Question { get; set; } = null!;

    [Required, MaxLength(1000)]
    public string Value { get; set; } = "";
}
