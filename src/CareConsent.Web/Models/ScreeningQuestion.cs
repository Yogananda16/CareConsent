using System.ComponentModel.DataAnnotations;

namespace CareConsent.Web.Models;

public enum QuestionType
{
    YesNo,
    Number,
    Scale,   // 1 to 5
    Text
}

public class ScreeningQuestion
{
    public int Id { get; set; }

    public int StudyId { get; set; }
    public Study Study { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Text { get; set; } = "";

    public QuestionType Type { get; set; }

    // Position of the question on the page, lowest first.
    public int Order { get; set; }
}
