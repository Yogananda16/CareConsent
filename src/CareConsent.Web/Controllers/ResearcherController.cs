using CareConsent.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CareConsent.Web.Controllers;

// Researcher-only pages. The dashboard content comes in step 5.
[Authorize(Roles = Roles.Researcher)]
public class ResearcherController : Controller
{
    public IActionResult Index() => View();
}
