# Phase 5 test evidence

Generated on 2026-11-02 from the `feat/phase-5-school` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API, and the real web app. PRD exit criterion of the phase:
"Admissions and approval flows pass end to end."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 165, Application 188) | 353 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS + Keycloak) | 501 passed |
| Frontend unit tests (Vitest) | 551 passed (49 files) |
| End-to-end tests (Playwright, 49 tests) | Full run: 47 passed, 2 failed on a timeout; both passed after the fix, see below |

Backend total: 854 tests.

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1083 | 1178 | 91.9% |
| SchoMans.Application | 5063 | 5498 | 92.1% |
| SchoMans.Domain | 1958 | 2144 | 91.3% |
| SchoMans.Infrastructure | 1166 | 1237 | 94.3% |
| **All** | **9270** | **10057** | **92.2%** |

Frontend (Vitest, v8): statements 91.9%, branches 87.4%, functions 88.7%, lines 92.1%. Excluded: the generated API client, test helpers and `main.tsx`.

## What the tests prove
- **Admission periods** (`AdmissionEndpointTests`, `AdmissionTests`): only the admin staff manage periods; the dates must be in
  order; two periods are never open at once.
- **Applying** (`AdmissionEndpointTests`): an applicant fills the form, uploads the three documents through presigned
  links and hands the application in only when the form and the documents are complete; after that the form is locked
  except for rejected documents, which can be replaced.
- **Hidden decisions** (`AdmissionEndpointTests`, results job): the staff verify documents and decide; the applicant
  keeps seeing "in verification" until the announcement moment, for everybody at once; the Hangfire job then notifies
  them. A rejected document and its note are always visible, because the applicant must act on them.
- **Enrollment** (`AdmissionEndpointTests`, `KeycloakIdentityProvisionerTests`): an accepted applicant confirms the place before the
  re-registration deadline; the admin staff enroll them with a classroom and a NISN; the account becomes a Student in the
  database and the Keycloak role changes through the Admin API. A Keycloak failure gives 502 and changes nothing. The
  end-to-end test checks the new student's real login and deletes the user afterwards.
- **Approval chains** (`ApprovalTests`, `RequestEndpointTests`, `ApprovalChainEndpointTests`): the default chains work step by step;
  the homeroom teacher is found from the classroom of the student; only the person whose turn it is decides; a rejection needs a
  note; the requester can withdraw a pending request; a chain cannot be edited while requests of its kind wait.
- **Letters** (`LetterPdfRendererTests`, `RequestEndpointTests`): approving the last step of a certificate letter numbers it
  (`001/SK/SCHOMANS/X/2026`), draws the PDF with a QR code, stores it privately, and only people with access to the request
  get a signed link. `/verify/<code>` is public and answers "valid" or "not found". `sample-letter.pdf` is a letter made by the real renderer.
- **Dashboard and landing page**: the principal's "requests awaiting approval" card counts the real queue; the landing page
  shows the admissions banner, the static school profile and the awards from the news category Award.
- **Roles**: every new endpoint has a success, a validation failure and a forbidden-role test.

## Deviations from the PRD and decisions to confirm
Choices made where the PRD is silent; the first seven were decided with the project owner before coding.
1. **The admin decides manually**; there is no automatic score. The decision stays hidden until `ResultsAnnouncedAt`.
2. **An accepted applicant confirms the place** (`ReregistrationConfirmedAt`) before the deadline, then the admin enrolls them with classroom and NISN.
3. **Default chains**: student leave HomeroomTeacher then Principal, teacher leave Principal, certificate letter Admin then Principal. The admin edits them while no request of the kind is pending.
4. **Only certificate letters become PDFs**, numbered `001/SK/SCHOMANS/X/2026` (sequence per year, Roman-numeral month), with a QR code to a public verify page.
5. **Landing page**: a static fictional profile, awards from the news category Award, and an admissions banner.
6. **The role change Applicant to Student** goes through the Keycloak Admin API with a service account (ADR-003).
7. **Package added (approved)**: QRCoder 1.7.0 (MIT) for the QR code.
8. **Fields added to the data model** beyond the PRD sketch: `AdmissionPeriod.Name` and `ResultsNotifiedAt`, `Applicant.SubmittedAt`, `DecidedAt`, `ReregistrationConfirmedAt`, `ApplicantDocument.FileName`, `Request.DecidedAt`. The PRD, ERD and system design were updated.
9. **`HomeroomTeacher` is a chain role, not a Keycloak role**: it is resolved to the homeroom teacher of the student's classroom.
10. **A new error type `Unavailable`** maps to HTTP 502 for a failing identity provider.
11. **New policies**: `Applicant`, `RequestUsers` (every role except Applicant) and `Approvers` (Admin, Principal, Teacher).
12. **The AI-03 document extraction** (`ExtractedData`) stays empty until Phase 6.
13. **The results job runs every 5 minutes** (Hangfire), not at the exact second of the announcement.
14. **Request and applicant data is always re-read** (`staleTime: 0`) because other people change it.
15. **The inbox lists show no reason column**; the reason is on the request page. A column is a cheap follow-up.
16. **Demo data** has one open admission period so the flow can be shown at once.

## Service account caveat
The `schomans-admin-api` client has `manage-users` and `view-realm` only, but a service account with `manage-users` can in
principle grant itself more. For the demo this is accepted; production would add a Keycloak fine-grained permission (see ADR-003).

## How the end-to-end tests ran (read this before trusting the number)
1. **Full run** (`logs/run-1-full.log`): 47 passed, 2 failed (20.8 minutes). Both failures were Phase 1 specs
   (`demo-login` anonymous redirect, `keycloak-login`): the silent sign-in of the web app takes about 4.7 seconds while the Vite
   dev server loads the app twice, and their assertions waited 5 seconds.
2. **Rerun of the two** (`logs/run-2-the-two-failures-again.log`): the same two failed again, which confirmed the cause.
3. **After the fix** (`logs/run-3-after-the-timeout-fix.log`): the assertion timeout of the two files is 20 seconds; 15 of 15 tests of those files passed.

Not done: one uninterrupted 49-test run on the final code. Run `cd web && npm run test:e2e` on an idle machine for that.
The only change after the full run besides the timeouts is a `networkidle` wait before the landing-banner screenshot (it showed
skeletons); it changes no assertion.

## Bugs the tests and the screenshots found during this phase
- EF Core treated a document with a preset Guid, added to a tracked applicant, as Modified and failed with a concurrency error (500).
  The keys of the new child tables are now `ValueGeneratedNever()` (migration `FixChildKeyGeneration`).
- The submit button stayed locked: the derived `IsComplete` of the form was stored in the JSON and went stale. It is now `[JsonIgnore]`
  and the web form compares the seven fields.
- Concurrent uploads hid a document in the web form; the list is now invalidated after every step (unit test added).
- A request page showed a 30-second-old state after a decision; requests and applicants are read again every time.
- The retired-period tests used overlapping windows (409) and failed 12 of 24 admission tests; each now gets its own window.
- The results job counted applicants left by earlier runs; the test runs it once to catch up.
- The demo admin loses its session on a reload, so the end-to-end specs navigate inside the app.
- The approval-chain editor was locked in the dev database by pending end-to-end requests; they were cancelled through the API.

## Data the end-to-end tests leave behind
Applicant rows, requests and letters with an "E2E ..." reason stay in the development database. The student and the Keycloak
user created by the enrollment spec are deleted by its `afterAll`.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report of the first full run (`playwright-report-run-1/index.html`), the text logs of the three runs (`logs/`), and screenshots (`screenshots/`)
- `sample-letter.pdf`: a certificate letter rendered by the real QuestPDF renderer, with its QR code

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README).
