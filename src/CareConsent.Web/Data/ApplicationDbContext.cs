using CareConsent.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CareConsent.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
{
    public DbSet<Study> Studies => Set<Study>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<ScreeningQuestion> ScreeningQuestions => Set<ScreeningQuestion>();
    public DbSet<ScreeningAnswer> ScreeningAnswers => Set<ScreeningAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Enrollment>(e =>
        {
            // Store the status as readable text ("Started"), not a number.
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            // A participant can enroll in a study only once.
            e.HasIndex(x => new { x.StudyId, x.ParticipantId }).IsUnique();
            e.HasOne(x => x.Participant).WithMany().HasForeignKey(x => x.ParticipantId);
        });

        // One consent record per enrollment.
        builder.Entity<ConsentRecord>()
            .HasOne(c => c.Enrollment)
            .WithOne(e => e.ConsentRecord)
            .HasForeignKey<ConsentRecord>(c => c.EnrollmentId);

        builder.Entity<ScreeningQuestion>()
            .Property(q => q.Type).HasConversion<string>().HasMaxLength(20);

        builder.Entity<ScreeningAnswer>(a =>
        {
            // One answer per question per enrollment.
            a.HasIndex(x => new { x.EnrollmentId, x.ScreeningQuestionId }).IsUnique();
            a.HasOne(x => x.Question).WithMany().HasForeignKey(x => x.ScreeningQuestionId);
        });

        SeedRoles(builder);
        SeedStudy(builder);
    }

    // Fixed IDs and stamps so the migration does not change on every build.
    private static void SeedRoles(ModelBuilder builder)
    {
        builder.Entity<IdentityRole>().HasData(
            new IdentityRole { Id = "role-participant", Name = Models.Roles.Participant, NormalizedName = "PARTICIPANT", ConcurrencyStamp = "role-participant" },
            new IdentityRole { Id = "role-researcher", Name = Models.Roles.Researcher, NormalizedName = "RESEARCHER", ConcurrencyStamp = "role-researcher" });
    }

    // One fictional study. The questions are made up and are not diagnostic.
    private static void SeedStudy(ModelBuilder builder)
    {
        builder.Entity<Study>().HasData(new Study
        {
            Id = 1,
            Title = "Daily Routines and Wellbeing Study (Fictional)",
            Summary = "A made-up study about everyday habits such as sleep, water, and walking. "
                    + "It exists only to demonstrate this web app. No real research is being done.",
            ConsentText = "This is a fictional study used for a software demo. "
                        + "By agreeing, you confirm that you understand: "
                        + "(1) no real research is taking place, "
                        + "(2) you should not enter any real personal or health information, "
                        + "(3) your answers are stored only as demo data, and "
                        + "(4) you may stop at any time.",
            ConsentVersion = "1.0"
        });

        builder.Entity<ScreeningQuestion>().HasData(
            new ScreeningQuestion { Id = 1, StudyId = 1, Order = 1, Type = QuestionType.YesNo,
                Text = "Are you 18 years of age or older?" },
            new ScreeningQuestion { Id = 2, StudyId = 1, Order = 2, Type = QuestionType.Number,
                Text = "On a typical day, how many glasses of water do you drink?" },
            new ScreeningQuestion { Id = 3, StudyId = 1, Order = 3, Type = QuestionType.Scale,
                Text = "How would you rate your sleep last week, from 1 (poor) to 5 (great)?" },
            new ScreeningQuestion { Id = 4, StudyId = 1, Order = 4, Type = QuestionType.YesNo,
                Text = "Do you usually take a walk during the day?" },
            new ScreeningQuestion { Id = 5, StudyId = 1, Order = 5, Type = QuestionType.Text,
                Text = "Is there anything else you would like to tell the fictional study team?" });
    }
}
