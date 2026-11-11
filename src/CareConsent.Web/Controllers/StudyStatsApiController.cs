using CareConsent.Web.Data;
using CareConsent.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareConsent.Web.Controllers;

// The JSON this endpoint returns. Counts only: no emails, names, or answers.
public record StudyStatsResponse(
    int StudyId,
    string StudyTitle,
    int Total,
    int Started,
    int Consented,
    int Completed);

// A JSON API for researchers. [ApiController] makes this an API endpoint, so
// a caller who is not allowed gets a 401 or 403 status code, not a redirect
// to an HTML page.
[ApiController]
[Route("api/study")]
[Authorize(Roles = Roles.Researcher)]
public class StudyStatsApiController(ApplicationDbContext db) : ControllerBase
{
    // GET /api/study/stats
    [HttpGet("stats")]
    public async Task<ActionResult<StudyStatsResponse>> GetStats()
    {
        var study = await db.Studies
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Title })
            .FirstAsync();

        var counts = await db.Enrollments
            .Where(e => e.StudyId == study.Id)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        var started = counts.GetValueOrDefault(EnrollmentStatus.Started);
        var consented = counts.GetValueOrDefault(EnrollmentStatus.Consented);
        var completed = counts.GetValueOrDefault(EnrollmentStatus.Completed);

        return new StudyStatsResponse(
            study.Id,
            study.Title,
            Total: started + consented + completed,
            Started: started,
            Consented: consented,
            Completed: completed);
    }
}
