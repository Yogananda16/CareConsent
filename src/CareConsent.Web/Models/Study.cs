using System.ComponentModel.DataAnnotations;

namespace CareConsent.Web.Models;

public class Study
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "";

    [Required, MaxLength(2000)]
    public string Summary { get; set; } = "";

    [Required]
    public string ConsentText { get; set; } = "";

    [Required, MaxLength(20)]
    public string ConsentVersion { get; set; } = "";

    public List<ScreeningQuestion> Questions { get; set; } = [];
    public List<Enrollment> Enrollments { get; set; } = [];
}
