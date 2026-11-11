using System.Text.RegularExpressions;
using CareConsent.Web.Data;
using CareConsent.Web.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CareConsent.Tests;

// Starts the real app in memory, like FastAPI's TestClient. The Postgres
// database is swapped for an in-memory one, so tests need no database server
// and never touch real tables.
public class TestAppFactory : WebApplicationFactory<Program>
{
    public const string Password = "Test123!";

    private readonly string _databaseName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Tests create their own users, so the demo accounts are switched off.
        builder.UseSetting("Demo:Enabled", "false");

        builder.ConfigureServices(services =>
        {
            var postgres = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                         || d.ServiceType == typeof(IDbContextOptionsConfiguration<ApplicationDbContext>))
                .ToList();
            foreach (var descriptor in postgres)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        // Creates the tables and the seed rows (roles, study, questions).
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        return host;
    }

    // A client that keeps cookies but does not follow redirects, so tests can
    // see exactly where the app sends the user.
    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public async Task<string> CreateUserAsync(string email, string role)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = email, Email = email };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user.Id;
    }

    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = NewClient();
        var page = await client.GetStringAsync("/Identity/Account/Login");
        var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = Password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = AntiForgeryToken(page)
        }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode); // a failed login re-shows the page with 200
        return client;
    }

    // Adds a finished enrollment straight into the database.
    public Task<int> AddCompletedEnrollmentAsync(string participantId, string typedName) =>
        AddEnrollmentAsync(participantId, EnrollmentStatus.Completed, typedName);

    // Adds an enrollment in the given status straight into the database.
    public async Task<int> AddEnrollmentAsync(string participantId, EnrollmentStatus status, string typedName = "Test Name")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var enrollment = new Enrollment
        {
            StudyId = 1,
            ParticipantId = participantId,
            Status = status,
            StartedAt = DateTime.UtcNow,
            CompletedAt = status == EnrollmentStatus.Completed ? DateTime.UtcNow : null,
            ConsentRecord = status == EnrollmentStatus.Started ? null : new ConsentRecord
            {
                Agreed = true,
                TypedName = typedName,
                SignedAt = DateTime.UtcNow,
                ConsentVersion = "1.0"
            }
        };
        db.Enrollments.Add(enrollment);
        await db.SaveChangesAsync();
        return enrollment.Id;
    }

    // Runs a query against the test database, to check what was stored.
    public async Task<T> QueryAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    // Posts a form the way a browser would, with the anti-forgery token.
    // Every page has a token when logged in (the logout form in the top bar).
    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string url, Dictionary<string, string> form)
    {
        var page = await client.GetStringAsync("/");
        var fields = new Dictionary<string, string>(form) { ["__RequestVerificationToken"] = AntiForgeryToken(page) };
        return await client.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public static string AntiForgeryToken(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
}
