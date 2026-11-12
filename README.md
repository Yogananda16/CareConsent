# CareConsent

**A small research-study web app that shows how participants give informed consent and how researchers securely review study responses.**

Participants register, read a fictional study, sign a consent form, and complete a five-question screening survey. Authorized researchers see enrollment progress, review submitted answers, and retrieve summary statistics. **All study content and demo accounts are fictional.**

**[Live demo](https://careconsent.dev)** · [How it works](#how-it-works) · [Security & privacy](#security-and-privacy) · [Architecture](#system-architecture) · [Testing & CI/CD](#testing-and-cicd) · [Technical docs](#dive-deeper)

> **Portfolio demonstration, not a clinical system.** Do not enter real personal or patient information. This project is not affiliated with a healthcare institution, does not conduct real research, and does not claim HIPAA compliance. See [security limitations](docs/security-and-privacy.md#current-limitations).

## The idea in 30 seconds

In a research study, participants need to understand what they are agreeing to, and researchers need an organized way to track consent and screening responses. **CareConsent demonstrates both sides in one application.**

| Participant | Researcher |
|---|---|
| Creates an account, reviews the study, consents, answers questions, and sees a confirmation. | Uses a provisioned researcher account to see enrollment statuses, review responses, and read study statistics. |

![CareConsent homepage, displaying the fictional-data notice and study entry point](docs/screenshots/homepage.png)

## How it works

```mermaid
flowchart LR
    P[Participant] --> R[Register / log in]
    R --> S[Read study]
    S --> C[Give consent]
    C --> Q[Answer 5 questions]
    Q --> F[Confirmation]

    T[Researcher] --> L[Authorized log in]
    L --> D[Dashboard]
    D --> V[View enrollment and answers]
    D --> A[JSON statistics API]

    classDef participant fill:#e6f4ea,stroke:#4e9870,color:#173a28
    classDef researcher fill:#eaf2ff,stroke:#5b83cb,color:#193657
    class P,R,S,C,Q,F participant
    class T,L,D,V,A researcher
```

**Important distinction:** Public registration always creates a **Participant** account. Researcher access is restricted; there is no self-service researcher registration or approval workflow in the current demo.

## System architecture

One simple ASP.NET Core application handles web pages, authorization, business rules, and an API. PostgreSQL stores the records. AWS hosts the app and its database.

```mermaid
flowchart TB
    U[Participant / Researcher in browser] -->|HTTPS| CF[CloudFront + careconsent.dev]
    CF -->|HTTP to origin*| EB[Elastic Beanstalk / EC2]
    EB --> APP[ASP.NET Core MVC + Razor Views]
    APP --> ID[ASP.NET Core Identity + role checks]
    APP --> EF[EF Core / Npgsql]
    EF -->|TLS requested| DB[(Amazon RDS PostgreSQL)]
    SM[AWS Secrets Manager] -.->|DB connection configuration| EB
    GH[GitHub Actions] -.->|Build, test, deploy| EB
```

*The browser-to-CloudFront link uses HTTPS; **CloudFront-to-Elastic Beanstalk currently uses HTTP**. This is a known demo limitation, not end-to-end TLS. See [AWS architecture](docs/aws-deployment.md) and [security notes](docs/security-and-privacy.md).

[**Explore the full architecture →**](docs/architecture.md)

## What I built

- **Participant journey:** account creation, study information, versioned consent, typed signature, validated questionnaire, and completion receipt.
- **Researcher journey:** role-restricted dashboard with counts for Started / Consented / Completed, participant emails, and per-enrollment response details.
- **Protected JSON API:** `GET /api/study/stats` returns counts only; access is restricted to researchers.
- **Database-backed workflow:** study, enrollment, consent, questions, and answers persisted using EF Core and PostgreSQL.
- **Tested deployment:** automated tests gate the GitHub Actions build before the app is published and sent to AWS Elastic Beanstalk.

## Security and privacy

Security was part of the design, not an afterthought.

| Control | How it works |
|---|---|
| **Authentication** | ASP.NET Core Identity manages registration, sign-in, and password hashing. |
| **Role-based access** | Participant and Researcher authorization checks run on the server. |
| **Record ownership** | Participant record queries filter by the signed-in user ID. |
| **Consent-first workflow** | Questions cannot be submitted before recorded consent; repeated submissions are blocked. |
| **Form protection** | Anti-forgery validation and server-side answer validation. |
| **Minimum researcher data** | Dashboard shows status and email; detail view excludes the typed signature and password fields. |
| **Credential management** | Database connection configuration is supplied through AWS Secrets Manager on deployment. |
| **Data boundaries** | Fictional seeded study; demo banner warns against entering real personal or health information. |

**Not production-ready:** origin TLS is incomplete, the demo uses openly shared accounts, and the deployment role needs least-privilege hardening. No real patient information should be submitted.

[**Security design, access matrix & limitations →**](docs/security-and-privacy.md)

## Tech stack

| Layer | Technologies |
|---|---|
| Web UI | Razor Views, HTML, CSS, Bootstrap |
| Application | C#, .NET 10, ASP.NET Core MVC, REST-style JSON endpoint |
| Authentication / authorization | ASP.NET Core Identity, roles, authorization attributes |
| Data | Entity Framework Core, Npgsql, PostgreSQL |
| Automated testing | xUnit, `WebApplicationFactory`, EF Core InMemory provider |
| Cloud | Name.com domain/DNS, AWS CloudFront, Elastic Beanstalk (EC2), RDS, Secrets Manager, ACM |
| Delivery | GitHub Actions, AWS OIDC role, S3 application bundle |

## Data model

The app uses Identity users plus five main entities:

`Study` · `Enrollment` · `ConsentRecord` · `ScreeningQuestion` · `ScreeningAnswer`

Every participant can have **one enrollment per study**; each enrollment has at most one consent record and one response per question. The seeded **Daily Routines and Wellbeing Study** contains five invented, non-diagnostic questions.

[**See entity relationships and request/data flow →**](docs/database-design.md)

## Testing and CI/CD

**86 automated tests** cover validation rules, participant flows, authentication, authorization, dashboard behavior, researcher details, and the JSON API. Integration tests run the ASP.NET Core app in memory with a test database; they do **not** require a live AWS database.

```mermaid
flowchart LR
    G[Commit + push to main] --> B[GitHub Actions]
    B --> T[Restore / build / test]
    T -->|Tests pass| Z[Publish + package ZIP]
    Z --> O[AWS OIDC authentication]
    O --> E[Elastic Beanstalk deploy]
```

![Successful CareConsent GitHub Actions workflow, including build, tests, and AWS deployment request](docs/screenshots/ci-pipeline.png)

The workflow passed its build/test/deployment-command steps. **A green GitHub Actions run alone does not verify that Elastic Beanstalk completed deployment or that the public app is healthy**; those are checked separately in AWS and the browser. Schema migrations are applied separately, not automatically by CI/CD.

[**Testing strategy and CI/CD details →**](docs/testing-and-cicd.md)

## Application screenshots

| Participant registration | Live application home |
|---|---|
| ![Fictional participant registration form](docs/screenshots/register.png) | ![CareConsent homepage](docs/screenshots/homepage.png) |

**Next screenshots to capture:** study and consent, questionnaire and confirmation, researcher dashboard, and researcher response details. Use fictional data only. See the [screenshot checklist](docs/screenshots/README.md) for suggested filenames and framing.

## Run locally

**Requirements:** .NET 10 SDK, PostgreSQL, and EF Core CLI (`dotnet ef`). The default local connection in `appsettings.json` targets `localhost:5433`, database `careconsent`, user `postgres`; adjust it for your own setup.

```powershell
# From the repository root
# Configure the local database password with .NET user-secrets (do not commit credentials).
dotnet user-secrets set "DbPassword" "<your-local-database-password>" --project src/CareConsent.Web

# Create/update the database schema
dotnet ef database update --project src/CareConsent.Web --startup-project src/CareConsent.Web

# Run the application
dotnet run --project src/CareConsent.Web
```

Then open the local URL printed in the terminal (typically `http://localhost:5180`). To run the automated tests:

```powershell
dotnet test CareConsent.sln
```

The development configuration enables demo seeding only if demo passwords are provided via local user-secrets. See [local setup and deployment notes](docs/aws-deployment.md#local-development).

## Dive deeper

| Document | What's inside |
|---|---|
| [**Architecture**](docs/architecture.md) | MVC request flow, participants vs researchers, component boundaries |
| [**Security & privacy**](docs/security-and-privacy.md) | RBAC matrix, ownership checks, consent validation, risk/limitations |
| [**Database design**](docs/database-design.md) | ER diagram, constraints, EF Core and migrations |
| [**AWS deployment**](docs/aws-deployment.md) | Domain, DNS, certificates, CloudFront, EC2, RDS, Secrets Manager |
| [**Testing & CI/CD**](docs/testing-and-cicd.md) | Test cases, GitHub Actions, AWS OIDC, delivery checks |
| [**Screenshot guide**](docs/screenshots/README.md) | Images to capture, naming, privacy checklist |

## What I'd improve next

Add HTTPS from CloudFront to the application origin and restrict origin access; replace temporary broad deployment permissions with tightly scoped IAM; add researcher access approval and audit logging; improve deployment health checks and monitoring. These are **future improvements**, not implemented features.

---

*CareConsent is a personal portfolio project developed with AI-assisted coding tools and human-directed design, review, testing, and deployment. It is a fictional-data demonstration, not a live research service.*
