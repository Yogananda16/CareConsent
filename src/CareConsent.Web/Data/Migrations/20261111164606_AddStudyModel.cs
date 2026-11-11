using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CareConsent.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStudyModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Studies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ConsentText = table.Column<string>(type: "text", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Studies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Enrollments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyId = table.Column<int>(type: "integer", nullable: false),
                    ParticipantId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enrollments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enrollments_AspNetUsers_ParticipantId",
                        column: x => x.ParticipantId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Enrollments_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StudyId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningQuestions_Studies_StudyId",
                        column: x => x.StudyId,
                        principalTable: "Studies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    Agreed = table.Column<bool>(type: "boolean", nullable: false),
                    TypedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsentRecords_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScreeningAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnrollmentId = table.Column<int>(type: "integer", nullable: false),
                    ScreeningQuestionId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScreeningAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScreeningAnswers_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScreeningAnswers_ScreeningQuestions_ScreeningQuestionId",
                        column: x => x.ScreeningQuestionId,
                        principalTable: "ScreeningQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Studies",
                columns: new[] { "Id", "ConsentText", "ConsentVersion", "Summary", "Title" },
                values: new object[] { 1, "This is a fictional study used for a software demo. By agreeing, you confirm that you understand: (1) no real research is taking place, (2) you should not enter any real personal or health information, (3) your answers are stored only as demo data, and (4) you may stop at any time.", "1.0", "A made-up study about everyday habits such as sleep, water, and walking. It exists only to demonstrate this web app. No real research is being done.", "Daily Routines and Wellbeing Study (Fictional)" });

            migrationBuilder.InsertData(
                table: "ScreeningQuestions",
                columns: new[] { "Id", "Order", "StudyId", "Text", "Type" },
                values: new object[,]
                {
                    { 1, 1, 1, "Are you 18 years of age or older?", "YesNo" },
                    { 2, 2, 1, "On a typical day, how many glasses of water do you drink?", "Number" },
                    { 3, 3, 1, "How would you rate your sleep last week, from 1 (poor) to 5 (great)?", "Scale" },
                    { 4, 4, 1, "Do you usually take a walk during the day?", "YesNo" },
                    { 5, 5, 1, "Is there anything else you would like to tell the fictional study team?", "Text" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_EnrollmentId",
                table: "ConsentRecords",
                column: "EnrollmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_ParticipantId",
                table: "Enrollments",
                column: "ParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_StudyId_ParticipantId",
                table: "Enrollments",
                columns: new[] { "StudyId", "ParticipantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswers_EnrollmentId_ScreeningQuestionId",
                table: "ScreeningAnswers",
                columns: new[] { "EnrollmentId", "ScreeningQuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningAnswers_ScreeningQuestionId",
                table: "ScreeningAnswers",
                column: "ScreeningQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScreeningQuestions_StudyId",
                table: "ScreeningQuestions",
                column: "StudyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords");

            migrationBuilder.DropTable(
                name: "ScreeningAnswers");

            migrationBuilder.DropTable(
                name: "Enrollments");

            migrationBuilder.DropTable(
                name: "ScreeningQuestions");

            migrationBuilder.DropTable(
                name: "Studies");
        }
    }
}
