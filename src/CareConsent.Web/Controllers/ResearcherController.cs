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
}
