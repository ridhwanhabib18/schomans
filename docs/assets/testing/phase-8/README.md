# Phase 8 test evidence

Generated on 2026-11-05 from the `feat/phase-8-portfolio-release` branch. Everything ran against real services on a development machine: SQL Server 2022, Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API and the real web app; the AI provider is the deterministic mock (ADR-004). PRD exit criterion of the phase: "Live link + public repo ready to share."
**That criterion is not met yet: nothing is deployed** (no VPS or domain). What is ready is everything needed to deploy (`docs/en/deployment.md`), tested locally as far as a development machine allows.

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 220, Application 350) | 570 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 724 passed |
| Frontend unit tests (Vitest) | 711 passed (62 files) |
| End-to-end tests (Playwright, 56 tests, one uninterrupted run, 12.6 minutes) | 56 passed |
| Capture scripts against the local stack (7 page captures, 5 flows) | 12 passed |
| Documentation check (`SchoMans.DocGen --check`) | up to date, no diagram names a route that is gone |

Backend total: 1294 tests (3 new: `ForwardedHeadersTests`).

## Coverage (PRD target: at least 70%)
Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files. `tools/SchoMans.DocGen` is not covered by tests of its own; it is exercised by the documentation check.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1438 | 1533 | 93.8% |
| SchoMans.Application | 7674 | 8247 | 93.1% |
| SchoMans.Domain | 2584 | 2922 | 88.4% |
| SchoMans.Infrastructure | 1901 | 2060 | 92.3% |
| **All** | **13597** | **14762** | **92.1%** |

Frontend (Vitest, v8): statements 90.67%, branches 86.53%, functions 85.94%, lines 91.5%. Excluded: the generated API client, test helpers and `main.tsx`.

## Lighthouse on the public pages (PRD target: 90)
Lighthouse 12, mobile profile (simulated slow 4G, 4× slower CPU), headless Chromium, the production build served by `vite preview` with the API on the same machine (so no compression or HTTP/2 from a real proxy). Reports: `lighthouse/*.report.html`.

| Page | Performance | Accessibility | Best practices | SEO | First paint | Largest paint | Blocking time |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Landing page `/` | 46 → **69** | 99 | 93 | 91 → **100** | 3.7 → 2.4 s | 5.6 → 2.4 s | 990 → 1110 ms |
| News list `/news` | 57 → **75** | 98 | 93 | 91 → **100** | 3.7 → 2.3 s | 4.5 → 2.9 s | 770 → 680 ms |
| An article | 55 → **78** | 99 | 93 | 91 → **100** | 3.7 → 2.3 s | 4.6 → 3.1 s | 920 → 550 ms |

(The first figure is before the changes of this phase, the second after.) **Performance is below the 90 target on all three pages.** Two changes were made: the routes of the signed-in area are loaded when they open (the app was one 1 MB bundle), and a `robots.txt` was added (the server answered with the app, which Lighthouse counted as 18 errors). What is left is the JavaScript of the single-page app itself (main bundle about 490 KB, blocking time of 0.5 to 1.1 s under a 4× slower CPU), and the landing page still shifts its layout a little (0.108) when the news arrives. Reaching 90 would take pre-rendering the public pages, loading SignalR and the sign-in library only after sign-in, and reserving space for the news list; those are not done.

## What the tests and checks prove
- **Forwarded headers** (`ForwardedHeadersTests`): behind a trusted proxy every visitor has their own limit (6 chatbot questions a minute); a connection that is not a trusted proxy cannot change its address with a forged `X-Forwarded-For`; with the setting off, the header is ignored. The same was checked against the real stack through Caddy: 6 answers, then 429, whatever the forged address.
- **Deployment files**: both images build (API 641 MB, web 94 MB); the production compose stack starts healthy on `localhost` and `auth.localhost`; Caddy serves the app with deep links, long cache for hashed files and no cache for `sw.js`; the API answers through Caddy with Keycloak tokens; a demo sign-in through the real Keycloak and every page of the teacher role work; `/hangfire` and the Keycloak admin console are not reachable from outside. (Images were built with the proxy certificate of this machine added to a throwaway Dockerfile, which is not in the repository.)
- **Documentation generated from the code**: 181 operations in the API reference and the authorization matrix, the ERD and one diagram per domain, the recurring jobs (taken from the real registration) and the SignalR events. The check fails when a generated file is out of date or when a hand-written document names an API route that does not exist; a made-up route in a diagram was detected during this phase.
- **Gallery** (`docs/assets/screenshots/`, 166 pictures, and `docs/assets/flows/`, 5 GIFs): captured from a fresh database with the fictional demo data, with the demo admin staff user named M Ridhwan Habib A.

## Found by testing in this phase
1. Behind a proxy, the rate limits per client address would have put every visitor in one bucket (fixed: `ForwardedHeaders`).
2. The API refused the plain-HTTP key address of the internal Docker network (fixed: `Keycloak__RequireHttpsMetadata` in the production compose file).
3. Keycloak's realm import failed on SQL Server: a client description was longer than 255 characters (the development H2 database does not enforce it). Fixed; Keycloak had restarted 15 times before.
4. A project that references the API alone resolved `Newtonsoft.Json` to a version with a known vulnerability (fixed with a version pin to the 13.0.4 the API already uses).
5. No `robots.txt` (fixed).
6. Two end-to-end defects of the tests, not of the product: a second navigation made right after another one is cancelled while the page of a lazy route is still loading (the attendance spec now waits for the page), and `getByLabel('Feedback')` matched two elements once the AI feedback region had loaded (made exact).

## Deviations from the PRD and decisions to confirm
1. **Not deployed.** No VPS or domain exists, so there is no live link, and the gallery comes from the local stack. `CAPTURE_URL=<demo url> npm run capture:screenshots` repeats it against the demo. The deployment guide says what was and was not tested.
2. **Flow GIFs** are built from step pictures with a small Python script (Pillow), because ffmpeg is not installed here and is not a project dependency. The admissions GIF has three steps: a fresh database has no applicant yet.
3. **The 2-minute demo video** is a script to record (`docs/en/demo-video-script.md`), not a video.
4. **API reference**: generated from the registered endpoints (method, route, name, policy, request and response types), not from an exported `openapi.json`; field-level schemas are in the OpenAPI document of the running API.
5. **Not built from the stack table**: Seq (logs go to the console) and automatic backups.
6. **Lighthouse performance** is below 90 (see above).
7. **The demo seed has no grades or attendance history**, so the dashboards of a fresh database show "no data" charts. Adding a history to the seed is a follow-up; it is not in the PRD.
8. **Demo name**: the demo admin staff user, previously Sari Utami, is M Ridhwan Habib A (seed, Keycloak realm and the tests that name it).
9. **Dependency pin**: `Newtonsoft.Json` 13.0.4 in `Directory.Packages.props`. No package was added.
10. **System design tab sync** does not apply: the design document is the Markdown file in the repository.

## Known issues
- The end-to-end suite is sensitive to a starved machine. With about 200 MB of free memory one run took 18 minutes and a real-time test timed out; the same spec passes alone, and the final run of 56 is uninterrupted. Likewise, one Vitest run could not start three workers (`Timeout waiting for worker`) and ran 688 of 711 tests; the run stored here ran all 711.
- `AchievementDescriptionEndpointTests.A_bulk_run_...` (AI-02) failed once in an earlier phase and has passed since; the cause is not found.
- The generated API client is still hand-written for the endpoints of Phase 4 and later.
- There is no screen for the AI allowance of a user (`AiGateway.GetAllowanceAsync` exists, no endpoint).

## Data the tests leave behind
The end-to-end tests leave rows in the development database (as in earlier phases). The capture scripts use their own database, `SchoMansCapture`, which can be dropped at any time. The quiz end-to-end test leaves two questions in the question bank.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report-run-1/index.html`) and the text log of the run (`logs/`)
- `lighthouse/`: the HTML and JSON reports of the three public pages

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx        # backend (stop a running API first: it locks the DLLs)
cd web && npm run test:coverage                                 # frontend
cd web && npm run test:e2e                                      # end to end
dotnet run --project tools/SchoMans.DocGen -- --check           # documents
cd web && npm run capture:screenshots && npm run capture:flows  # gallery
npx lighthouse@12 http://localhost:4173/ --output=json --output=html   # after npm run build and vite preview
```
