# CareConsent: project brief

## What this is
A small web app for a fictional health research study.
- A participant registers, reads about the study, gives consent, answers a short
  questionnaire, and sees a confirmation.
- A researcher logs in and sees who enrolled, who consented, and who finished.

This is a portfolio project. Its purpose is to show practical C# / ASP.NET Core
skills for an early-career research software engineer role in digital health.
The owner comes from Python (FastAPI, SQLAlchemy, pytest) and is new to .NET.

## Hard rules
- Synthetic data only. Never real patient or personal data.
- Screening questions must be clearly fictional and non-diagnostic.
- Never claim HIPAA compliance, clinical validity, or affiliation with any
  university or hospital, in code, UI text, or docs.
- Never commit secrets. The database password goes in `dotnet user-secrets`
  locally and in environment variables on AWS. The owner types passwords
  himself; do not ask him to paste them into the chat.
- Ask before creating, changing, or deleting anything on AWS. Say what it is
  and that it uses credits before running the command.
- Keep it simple. One MVC project plus one test project. No extra layers,
  no extra frameworks, no AI features.

## How to work
- Do ONE step from the plan at a time, then stop.
- After each step:
  1. Build and run the tests yourself and fix any errors.
  2. Tell the owner how to see it working (URL to open, what to click).
  3. Explain what you built in plain English, in 5 to 10 lines. Compare each
     new .NET idea to its Python equivalent (EF Core vs SQLAlchemy, xUnit vs
     pytest, controller action vs FastAPI route, Razor vs Jinja).
  4. Give one or two sentences he could say about it in an interview.
  5. Suggest a commit message, then wait. The owner commits and pushes with
     GitHub Desktop. Do not move on until he says so.
- Use short, plain words. No long explanations unless asked.

## Environment
- Windows, VS Code, repo at `D:\Yale\CareConsent`.
- PostgreSQL is installed locally. Ask the owner for host, port, and username
  (not the password).
- Check `dotnet --version` and `aws --version` first. If either is missing,
  give the install step and wait.

## Stack
| Layer | Choice |
|---|---|
| Language / framework | C#, ASP.NET Core MVC (current LTS) |
| Pages | Razor Views, HTML, CSS, a little JavaScript |
| Database | PostgreSQL through EF Core (Npgsql provider) |
| Login | ASP.NET Core Identity, roles: Participant, Researcher |
| Tests | xUnit, with WebApplicationFactory for integration tests |
| Hosting | AWS: Elastic Beanstalk (app), RDS PostgreSQL (database), CloudFront (HTTPS) |

## Structure
```
CareConsent.sln
src/CareConsent.Web/      Controllers, Models, ViewModels, Views, Services, Data
tests/CareConsent.Tests/  unit and integration tests
docs/                     architecture and security notes, screenshots
```

## Data model
Besides the Identity user tables:
- Study: title, summary, consent text, consent version
- Enrollment: one participant in one study, status Started / Consented / Completed
- ConsentRecord: agreed, typed name, signed time, consent version
- ScreeningQuestion: fictional question text, type, order
- ScreeningAnswer: one participant's answer to one question

## Plan
0. Check tools (.NET SDK, AWS CLI, C# extension for VS Code).
1. Create the solution and MVC app with Identity login, connected to local
   Postgres. Done when: register and log in work.
2. Add the entities, DbContext, first migration, and seed one fictional study
   with 4 or 5 questions. Done when: tables exist in Postgres.
3. Participant flow: study page, consent, questionnaire, confirmation.
   Server-side validation on every form. Done when: the flow can be completed.
4. Roles and access rules. Only researchers reach researcher pages. Every
   participant query filters by the logged-in user's ID. Done when: a
   participant gets 403 or a redirect on researcher pages and on another
   participant's record.
5. Researcher dashboard: counts by status and a list of enrollments.
6. One JSON API endpoint (study summary stats), researcher-only.
7. Tests. Unit: consent and screening rules (for example, no answers before
   consent). Integration: anonymous user is redirected to login, participant
   is blocked from the dashboard, participant A cannot read participant B's
   record. Done when: `dotnet test` passes.
8. Demo setup: seed one participant and one researcher account, show their
   logins on the login page, add a "Demo with fictional data, do not enter
   real information" banner.
9. Deploy to AWS. Done when: a public https link works with the demo logins.
10. README: what it is, live link, demo logins, screenshots, how to run it
    locally, security notes, and an honest "what I would add next" list
    (audit log, CI pipeline, Docker, SQL Server option). Mention that the
    project was built with AI assistance.

## Security checklist (apply as each step needs it)
- Passwords hashed by Identity, never stored or logged in plain text.
- `[Authorize]` on everything except the home, login, and register pages.
- Anti-forgery tokens on every POST form.
- EF Core queries only, no string-built SQL.
- Friendly error page in production, no stack traces shown to users.
- HTTPS redirect on.
- Collect the minimum: email, consent, answers.
