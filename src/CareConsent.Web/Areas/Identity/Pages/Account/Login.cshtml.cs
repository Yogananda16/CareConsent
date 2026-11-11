using System.ComponentModel.DataAnnotations;
using CareConsent.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace CareConsent.Web.Areas.Identity.Pages.Account;

// Replaces the built-in Identity login page so it can show the demo logins.
[AllowAnonymous]
public class LoginModel(SignInManager<IdentityUser> signInManager, IOptions<DemoOptions> demoOptions) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    // Null unless demo mode is on and both demo passwords are configured.
    public DemoOptions? Demo => demoOptions.Value.IsReady ? demoOptions.Value : null;

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return LocalRedirect(returnUrl ?? Url.Content("~/"));
        }

        // The same message for a wrong email and a wrong password.
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
