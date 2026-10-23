# SchoMans technical Q&A

Questions and the reasoning behind SchoMans' technical decisions, for reference and interview preparation. The Indonesian version is in the Tanya jawab teknis tab. License and product status reflect knowledge up to mid-2026; check official sources again before deciding.

## 1. Does any part of the tech stack cost money?

Almost everything is open source and free. Only the Claude API (per token) and server hosting actually cost money.

| Category | Components | Notes |
| --- | --- | --- |
| Paid | Claude API, VPS/Azure, domain (optional), WhatsApp gateway (optional) | Set a budget cap and a mock mode for AI |
| Free with conditions | SQL Server | Developer is for dev/test only; Express allows production with a 10 GB per-database limit |
| Free with conditions | QuestPDF | Free for individuals and companies with revenue < USD 1M/year |
| Free with conditions | Seq, Hangfire, Docker Desktop, GitHub Actions, Cloudflare R2 | Individual license, core edition, personal use, public repo, and free tier are enough for a portfolio |
| Free | Everything else | MIT, Apache 2.0, or BSD |

## 2. What is the best React state management?

There is no single best; what matters is separating two kinds of state. Server state (data from the API) is handled by **TanStack Query**, which takes care of caching, loading, refetching, and invalidation. Client state (login session, form wizards, UI preferences) is handled by a small state manager.

## 3. Zustand or MobX?

**Zustand** was chosen. Since TanStack Query handles most of the state, SchoMans' client state is small, and Zustand is the leanest fit.

| Aspect | Zustand | MobX |
| --- | --- | --- |
| Style | Functional, stores are hooks | OOP, classes with observable properties |
| Boilerplate | Very little | Little, needs `makeAutoObservable` and `observer` |
| Learning curve | Easiest | Requires understanding observables, actions, reactions |
| Bundle size | Very small | Larger |
| Job-market popularity | More common in new React projects | More common in older enterprise projects |

TanStack Query + Zustand is also the most common pattern in React projects today, so recruiters recognize it easily.

## 4. Why was MinIO replaced?

Not because it is paid, but because its free edition is no longer convincing for a new project. During 2025 the community edition's web console was cut back, MinIO stopped publishing ready-made binaries and Docker images, and the community edition went into maintenance mode. Its AGPL v3 license is also often avoided by companies.

## 5. How do we still get S3 experience?

The backend uses AWSSDK.S3 with one `S3FileStorage` implementation behind the `IFileStorage` interface. Locally it uses SeaweedFS in Docker (Apache 2.0); the live demo uses Cloudflare R2 (free tier; usually requires adding a payment method). The practices match production AWS S3: separate private and public buckets, presigned URLs, a key structure, and lifecycle rules.

## 6. Should Keycloak be replaced?

No; Keycloak stays.

| Option | Pros | Cons |
| --- | --- | --- |
| Keycloak (chosen) | Enterprise standard in Indonesia, real OIDC/SSO experience, auth features ready-made | RAM hungry (\~1 GB), realm and reverse-proxy setup is tricky |
| ASP.NET Core Identity | Built into .NET, no extra container, easy to debug | Auth pages built by hand, no SSO |
| Identity + OpenIddict | Native .NET OIDC server | Most complex setup |
| Duende IdentityServer | Mature | Paid for commercial use |
| Auth0, Clerk, Entra External ID | Fast setup | Vendor lock-in, paid above quota |

Reason: CV value for enterprise and banking employers, and the setup cost is paid only once at the start. Mitigation: export the realm to JSON, configure hostname and proxy early, and deploy from Phase 1.

## 7. What is the difference between mediator and MediatR?

**Mediator** is a design pattern: a sender does not call the receiver directly but goes through an intermediary. **MediatR** is a library that implements the pattern (commercially licensed since 2025). **Mediator** (martinothamar) is another library with the same name, MIT licensed.

```csharp
// With the mediator pattern
app.MapPost("/attendance/sessions", (OpenSessionCommand command, IMediator mediator)
    => mediator.Send(command));   // endpoint does not know the handler

// Without a mediator (SchoMans)
app.MapPost("/attendance/sessions", (OpenSessionCommand command, OpenSessionHandler handler)
    => handler.Handle(command));  // endpoint calls the handler directly
```

## 8. Did SchoMans avoid MediatR from the start?

Yes, MediatR was never recommended. However, the first proposal still mentioned the Mediator library as an alternative and included a `Behaviors/` folder, which is a mediator-pipeline concept. Both have been removed, so SchoMans now uses no mediator pattern at all.

## 9. For a portfolio, is it better to use a mediator or not?

No mediator, with the reasoning written in ADR-001. What impresses is not the choice itself but the ability to explain it. An answer like "I know MediatR, but for this project I chose direct handler calls because of A and B" usually lands better with senior interviewers than simply following a template. If the target employer clearly uses MediatR, the MIT-licensed Mediator library is worth considering because the code is nearly identical.

## 10. Why Minimal API instead of Controllers?

Minimal API fits the per-feature layout and direct handler calls; a controller would only be a pass-through layer. Dependencies are injected per endpoint, endpoint filters suit validation without a mediator, and the built-in OpenAPI is used to generate the orval client. Details are in ADR-002.

## 11. How does Minimal API differ from Controllers?

| Aspect | Controller | Minimal API |
| --- | --- | --- |
| Shape | Class deriving from `ControllerBase` | Functions mapped to routes |
| Configuration | Attributes: `[Route]`, `[HttpPost]`, `[Authorize]` | Chained methods: `MapPost`, `MapGroup`, `RequireAuthorization` |
| Dependency injection | Constructor, for every action | Per endpoint |
| Filters | MVC action filters | Endpoint filters |
| Automatic validation | `[ApiController]` + DataAnnotations | Built-in support since .NET 10; SchoMans uses FluentValidation via an endpoint filter |
| MVC features | Complete | Leaner |
| Performance | Slightly heavier | Lighter, supports Native AOT |
| Grouping | Automatic per class | Requires discipline |

Both run on the same ASP.NET Core: middleware, authentication, DI, and routing.

## 12. What are Minimal API's weaknesses and how are they handled?

| Weakness | Mitigation (PRD convention) |
| --- | --- |
| Easy to get messy | One file per feature + `MapGroup`, no endpoints in `Program.cs` |
| Lambdas are hard to read and debug | Named static methods, no business logic in endpoints |
| Parameter binding can surprise | Explicit parameter sources, `[AsParameters]` for queries |
| `IFormFile` uploads hit antiforgery | `.DisableAntiforgery()` because the API uses JWT |
| Fewer MVC features | Libraries must support Minimal API |
| Long endpoint metadata | Group-level configuration, extension methods such as `.WithValidation<T>()` |
| Endpoints are hard to unit test | Unit tests on handlers, integration tests per endpoint |
| Team familiarity | Reasoning written in ADR-002 |

## 13. Which parts are the heaviest to build?

| Rank | Part | Why it is heavy | How to lighten it |
| --- | --- | --- | --- |
| 1 | Keycloak + .NET + React auth foundation | Many settings must line up, errors are often vague | Do it first, export the realm to JSON, test access per role |
| 2 | Grading & report cards | Kurikulum Merdeka domain rules and PDF layout | Decide the school level and report-card format first, use a real report card as reference |
| 3 | Real-time QR attendance | Rotating QR, SignalR, phone-browser camera, simultaneous scans | Proven scanner library, test on real phones, unique constraint on session + student |
| 4 | Admissions & approvals | Essentially state machines with many statuses | Draw the status diagram first, transitions as entity methods |
| 5 | AI feature quality | Prompt iteration and test sets | Build the test set before writing the prompt |

Often underestimated: deployment (SQL Server and Keycloak are RAM hungry; deploy from Phase 1) and realistic demo seed data.

## 14. Why was the student AI tutor (AI-07) removed?

It is the hardest feature to get right: never giving direct answers, refusing off-topic questions, and handling sensitive situations with child users. It needs strict guardrails and continuous testing, so it was taken out of scope and listed under non-goals.

## 15. Which AI features are used?

Eight features: parent progress summaries (AI-01, must), report-card achievement descriptions (AI-02), admission document extraction (AI-03), a tool-use principal assistant (AI-04), rubric-based essay feedback (AI-05), a question generator (AI-06), a school info chatbot (AI-08), and announcement and letter drafts (AI-09). All follow the same principles: minimal data, drafts reviewed by a human, validated JSON output, budget caps, and a mock mode.

## 16. Why is the code and app fully English but the documentation bilingual?

English code is the industry standard and consistent with framework names. An English app lets recruiters anywhere try the demo, and the i18n structure allows Indonesian to be added later without code changes. Bilingual documentation serves two audiences: English for international reviewers, Indonesian for local readers and interview preparation.
