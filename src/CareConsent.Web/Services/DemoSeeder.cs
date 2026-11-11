using CareConsent.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CareConsent.Web.Services;

// Settings from the "Demo" section of the configuration.
// The passwords are never in the repo: they come from `dotnet user-secrets`
// locally and from environment variables on AWS.
public class DemoOptions
{
    public bool Enabled { get; set; }

    // ".example" is a reserved domain, so these can never be real addresses.
    public string ParticipantEmail { get; set; } = "participant@careconsent.example";
    public string ResearcherEmail { get; set; } = "researcher@careconsent.example";

    public string? ParticipantPassword { get; set; }
    public string? ResearcherPassword { get; set; }

    public bool IsReady =>
        Enabled && !string.IsNullOrEmpty(ParticipantPassword) && !string.IsNullOrEmpty(ResearcherPassword);
}

// Creates the two fictional demo accounts at startup. Safe to run on every
// start: it only adds what is missing and never touches other accounts.
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DemoOptions>>().Value;
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DemoSeeder");

        if (!options.Enabled)
        {
            return;
        }
        if (!options.IsReady)
        {
            logger.LogWarning("Demo accounts were not created. Set Demo:ParticipantPassword and Demo:ResearcherPassword.");
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await EnsureUserAsync(users, logger, options.ParticipantEmail, options.ParticipantPassword!, Roles.Participant);
        await EnsureUserAsync(users, logger, options.ResearcherEmail, options.ResearcherPassword!, Roles.Researcher);
    }

    private static async Task EnsureUserAsync(
        UserManager<IdentityUser> users, ILogger logger, string email, string password, string role)
    {
        var user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            if (!await SucceededAsync(users.CreateAsync(user, password), logger, email))
            {
                return;
            }
        }
        else if (!await users.CheckPasswordAsync(user, password))
        {
            // The configured demo password changed, so update the stored hash.
            var token = await users.GeneratePasswordResetTokenAsync(user);
            if (!await SucceededAsync(users.ResetPasswordAsync(user, token, password), logger, email))
            {
                return;
            }
        }

        if (!await users.IsInRoleAsync(user, role))
        {
            await SucceededAsync(users.AddToRoleAsync(user, role), logger, email);
        }
    }

    // Logs the reason for a failure. The password itself is never logged.
    private static async Task<bool> SucceededAsync(Task<IdentityResult> action, ILogger logger, string email)
    {
        var result = await action;
        if (!result.Succeeded)
        {
            logger.LogError("Demo account {Email} was not set up: {Errors}",
                email, string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        return result.Succeeded;
    }
}
