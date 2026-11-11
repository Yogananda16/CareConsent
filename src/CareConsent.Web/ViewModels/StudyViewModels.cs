using System.ComponentModel.DataAnnotations;
using CareConsent.Web.Models;

namespace CareConsent.Web.ViewModels;

public class StudyPageViewModel
{
    public required Study Study { get; set; }

    // Null when the participant has not started yet.
    public EnrollmentStatus? Status { get; set; }
}

public class ConsentViewModel
{
    // Shown on the page only. Always filled from the database, never from the form.
    public string? StudyTitle { get; set; }
    public string? ConsentText { get; set; }
    public string? ConsentVersion { get; set; }

    [Display(Name = "I have read the text above and I agree to take part.")]
    public bool Agreed { get; set; }

    [Display(Name = "Type your full name to sign")]
    [Required(ErrorMessage = "Type your name to sign.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "Your name must be 2 to 200 characters.")]
    public string? TypedName { get; set; }
}

public class QuestionnaireViewModel
{
    public string? StudyTitle { get; set; }
    public List<AnswerInput> Items { get; set; } = [];
}

public class AnswerInput
{
    public int QuestionId { get; set; }

    // Text and Type are filled from the database, never from the form.
    public string? Text { get; set; }
    public QuestionType Type { get; set; }

    public string? Answer { get; set; }
}

public class ConfirmationViewModel
{
    public int EnrollmentId { get; set; }
    public required string StudyTitle { get; set; }
    public required string TypedName { get; set; }
    public required string ConsentVersion { get; set; }
    public DateTime SignedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<AnsweredQuestion> Answers { get; set; } = [];
}

public record AnsweredQuestion(string Question, string Answer);
