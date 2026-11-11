using CareConsent.Web.Data;
using CareConsent.Web.Models;
using CareConsent.Web.Services;
using CareConsent.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareConsent.Web.Controllers;

// The participant flow: study page -> consent -> questionnaire -> confirmation.
// Every query filters by the logged-in user's ID, so a participant only ever
// sees their own enrollment.
[Authorize]
public class StudyController(ApplicationDbContext db, UserManager<IdentityUser> userManager) : Controller
{
    public async Task<IActionResult> Index()
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        return View(new StudyPageViewModel { Study = study, Status = enrollment?.Status });
    }

    [HttpPost]
    public async Task<IActionResult> Start()
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);

        if (enrollment is null)
        {
            enrollment = new Enrollment
            {
                StudyId = study.Id,
                ParticipantId = userManager.GetUserId(User)!,
                Status = EnrollmentStatus.Started,
                StartedAt = DateTime.UtcNow
            };
            db.Enrollments.Add(enrollment);
            await db.SaveChangesAsync();
        }

        return RedirectToStep(enrollment);
    }

    public async Task<IActionResult> Consent()
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        if (enrollment is null || !ParticipantRules.CanConsent(enrollment))
        {
            return RedirectToStep(enrollment);
        }

        return View(WithStudyText(new ConsentViewModel(), study));
    }

    [HttpPost]
    public async Task<IActionResult> Consent(ConsentViewModel model)
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        if (enrollment is null || !ParticipantRules.CanConsent(enrollment))
        {
            return RedirectToStep(enrollment);
        }

        if (!model.Agreed)
        {
            ModelState.AddModelError(nameof(model.Agreed), "You must tick the box to agree before you can continue.");
        }

        if (!ModelState.IsValid)
        {
            return View(WithStudyText(model, study));
        }

        db.ConsentRecords.Add(new ConsentRecord
        {
            EnrollmentId = enrollment.Id,
            Agreed = true,
            TypedName = model.TypedName!.Trim(),
            SignedAt = DateTime.UtcNow,
            ConsentVersion = study.ConsentVersion
        });
        enrollment.Status = EnrollmentStatus.Consented;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Questionnaire));
    }

    public async Task<IActionResult> Questionnaire()
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        if (enrollment is null || !ParticipantRules.CanAnswer(enrollment))
        {
            return RedirectToStep(enrollment);
        }

        var questions = await GetQuestionsAsync(study.Id);
        return View(new QuestionnaireViewModel
        {
            StudyTitle = study.Title,
            Items = questions.Select(q => ToInput(q, null)).ToList()
        });
    }

    [HttpPost]
    public async Task<IActionResult> Questionnaire(QuestionnaireViewModel model)
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        if (enrollment is null || !ParticipantRules.CanAnswer(enrollment))
        {
            return RedirectToStep(enrollment);
        }

        // Trust only the question IDs and answers from the form. The question
        // text and type always come from the database.
        var questions = await GetQuestionsAsync(study.Id);
        var posted = model.Items
            .GroupBy(i => i.QuestionId)
            .ToDictionary(g => g.Key, g => g.First().Answer);
        var items = questions
            .Select(q => ToInput(q, posted.GetValueOrDefault(q.Id)?.Trim()))
            .ToList();

        for (var i = 0; i < questions.Count; i++)
        {
            var error = ParticipantRules.ValidateAnswer(questions[i], items[i].Answer);
            if (error is not null)
            {
                ModelState.AddModelError($"Items[{i}].Answer", error);
            }
        }

        if (!ModelState.IsValid)
        {
            return View(new QuestionnaireViewModel { StudyTitle = study.Title, Items = items });
        }

        foreach (var item in items.Where(i => !string.IsNullOrEmpty(i.Answer)))
        {
            db.ScreeningAnswers.Add(new ScreeningAnswer
            {
                EnrollmentId = enrollment.Id,
                ScreeningQuestionId = item.QuestionId,
                Value = item.Answer!
            });
        }
        enrollment.Status = EnrollmentStatus.Completed;
        enrollment.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Confirmation));
    }

    public async Task<IActionResult> Confirmation()
    {
        var study = await GetStudyAsync();
        var enrollment = await GetEnrollmentAsync(study.Id);
        if (enrollment is not { Status: EnrollmentStatus.Completed, ConsentRecord: not null })
        {
            return RedirectToStep(enrollment);
        }

        var answers = await db.ScreeningAnswers
            .Where(a => a.EnrollmentId == enrollment.Id)
            .OrderBy(a => a.Question.Order)
            .Select(a => new AnsweredQuestion(a.Question.Text, a.Value))
            .ToListAsync();

        return View(new ConfirmationViewModel
        {
            StudyTitle = study.Title,
            TypedName = enrollment.ConsentRecord.TypedName,
            ConsentVersion = enrollment.ConsentRecord.ConsentVersion,
            SignedAt = enrollment.ConsentRecord.SignedAt,
            CompletedAt = enrollment.CompletedAt,
            Answers = answers
        });
    }

    // There is one seeded study in this demo.
    private Task<Study> GetStudyAsync() =>
        db.Studies.OrderBy(s => s.Id).FirstAsync();

    private Task<Enrollment?> GetEnrollmentAsync(int studyId)
    {
        var userId = userManager.GetUserId(User);
        return db.Enrollments
            .Include(e => e.ConsentRecord)
            .FirstOrDefaultAsync(e => e.StudyId == studyId && e.ParticipantId == userId);
    }

    private Task<List<ScreeningQuestion>> GetQuestionsAsync(int studyId) =>
        db.ScreeningQuestions
            .Where(q => q.StudyId == studyId)
            .OrderBy(q => q.Order)
            .ToListAsync();

    // Sends the participant to the step they are allowed to be on.
    private IActionResult RedirectToStep(Enrollment? enrollment) => enrollment?.Status switch
    {
        null => RedirectToAction(nameof(Index)),
        EnrollmentStatus.Started => RedirectToAction(nameof(Consent)),
        EnrollmentStatus.Consented => RedirectToAction(nameof(Questionnaire)),
        _ => RedirectToAction(nameof(Confirmation))
    };

    private static ConsentViewModel WithStudyText(ConsentViewModel model, Study study)
    {
        model.StudyTitle = study.Title;
        model.ConsentText = study.ConsentText;
        model.ConsentVersion = study.ConsentVersion;
        return model;
    }

    private static AnswerInput ToInput(ScreeningQuestion question, string? answer) => new()
    {
        QuestionId = question.Id,
        Text = question.Text,
        Type = question.Type,
        Answer = answer
    };
}
