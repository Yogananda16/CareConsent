using CareConsent.Web.Data;
using CareConsent.Web.Models;
using CareConsent.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareConsent.Web.Controllers;

// Researcher-only pages.
[Authorize(Roles = Roles.Researcher)]
public class ResearcherController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var study = await db.Studies.OrderBy(s => s.Id).FirstAsync();

        // One query that counts the enrollments per status.
        var counts = await db.Enrollments
            .Where(e => e.StudyId == study.Id)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        // Only the columns the dashboard needs. No typed names, no answers,
        // and nothing from the login tables except the email.
        var rows = await db.Enrollments
            .Where(e => e.StudyId == study.Id)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => new EnrollmentRow(
                e.Id,
                e.Participant.Email ?? "",
                e.Status,
                e.StartedAt,
                e.ConsentRecord != null ? (DateTime?)e.ConsentRecord.SignedAt : null,
                e.CompletedAt))
            .ToListAsync();

        return View(new DashboardViewModel
        {
            StudyTitle = study.Title,
            Started = counts.GetValueOrDefault(EnrollmentStatus.Started),
            Consented = counts.GetValueOrDefault(EnrollmentStatus.Consented),
            Completed = counts.GetValueOrDefault(EnrollmentStatus.Completed),
            Enrollments = rows
        });
    }

    // One enrollment with its consent status and answers, for example
    // /Researcher/Details/12. An ID that does not exist (or is not a number)
    // gets a plain 404.
    public async Task<IActionResult> Details(int id)
    {
        // Only the columns the page shows. The typed signature and the login
        // columns (password hash, tokens) are never loaded.
        var details = await db.Enrollments
            .Where(e => e.Id == id)
            .Select(e => new EnrollmentDetailsViewModel
            {
                Id = e.Id,
                StudyId = e.StudyId,
                StudyTitle = e.Study.Title,
                Email = e.Participant.Email ?? "",
                Status = e.Status,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                ConsentGiven = e.ConsentRecord != null && e.ConsentRecord.Agreed,
                ConsentedAt = e.ConsentRecord != null ? (DateTime?)e.ConsentRecord.SignedAt : null,
                ConsentVersion = e.ConsentRecord != null ? e.ConsentRecord.ConsentVersion : null
            })
            .FirstOrDefaultAsync();

        if (details is null)
        {
            return NotFound();
        }

        var answers = await db.ScreeningAnswers
            .Where(a => a.EnrollmentId == id)
            .ToDictionaryAsync(a => a.ScreeningQuestionId, a => a.Value);

        var questions = await db.ScreeningQuestions
            .Where(q => q.StudyId == details.StudyId)
            .OrderBy(q => q.Order)
            .Select(q => new { q.Id, q.Text })
            .ToListAsync();

        // Every question is listed, with null where there is no answer.
        details.Answers = questions
            .Select(q => new QuestionAnswer(q.Text, answers.GetValueOrDefault(q.Id)))
            .ToList();

        return View(details);
    }
}
