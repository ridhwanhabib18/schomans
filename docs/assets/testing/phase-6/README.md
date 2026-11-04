# Phase 6 test evidence

Generated on 2026-11-04 from the `feat/phase-6-ai-extras` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API, and the real web app. The AI provider is the deterministic mock
(the default, see ADR-004). PRD exit criterion of the phase: "Parent summaries sent after review; each AI feature's test set passes."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 196, Application 266) | 462 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 629 passed |
| Frontend unit tests (Vitest) | 604 passed (54 files) |
| End-to-end tests (Playwright, 53 tests, one uninterrupted run, 12.1 minutes) | 53 passed |

Backend total: 1091 tests.

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1098 | 1166 | 94.2% |
| SchoMans.Application | 5645 | 5937 | 95.1% |
| SchoMans.Domain | 1745 | 1948 | 89.6% |
| SchoMans.Infrastructure | 1394 | 1520 | 91.7% |
| **All** | **9882** | **10571** | **93.5%** |

Frontend (Vitest, v8): statements 91.6%, branches 86.7%, functions 88.3%, lines 91.9%. Excluded: the generated API client, test helpers and `main.tsx`.

## What the tests prove
- **AI-01 parent summaries**: drafted per student from facts only, reviewed by the homeroom teacher, and sent to the parents only after approval.
- **AI-02 achievement descriptions** (`AchievementDescriptionEndpointTests`, `ObjectivesDialog`/grade book tests): scores can be linked to a
  learning objective; one draft per student or one for the whole classroom; the texts of two students are never identical; a published grade book refuses drafts.
- **AI-03 admission documents** (`DocumentExtractionEndpointTests`, `ExtractionPromptTests` with 30 cases, `ExtractionUi`): photos only, the result is
  stored on the document, mismatches between documents are flagged for staff, and nothing is written into the form without the applicant's confirmation.
- **AI-04 principal assistant** (`AssistantEndpointTests`, 26 cases): only registered read-only tools can be called, with the permission check; student names
  never reach the provider; answers carry their sources and report links; too many tool steps are refused.
- **AI gateway** (`AiGatewayTests`): 10 calls per user per day, USD 5 per month, every call logged; a bulk run or a multi-step conversation costs one daily call.
- **F-11 audit log** (`AuditEndpointTests`, `AuditLogTests`): grades, attendance records, requests and approval steps are recorded in the same save as the
  change, with the user, the old and the new values; a save that changes nothing leaves no trace; only admin staff and the principal can read the trail.
- **F-11 email** (`EmailQueueTests`, `EmailMessageTests`): a decided request, an announced admission result and a published announcement queue an email next to the
  in-app notification; the sender job sends with a full link, retries a failing mail and gives it up after 5 attempts; without a mail server the queue waits.
- **PWA** (`pwa.test.ts`, `ai-extras.spec.ts`): the manifest and its icons are served, and the service worker is registered in production builds only.
- **Roles**: every new endpoint has a success, a validation failure and a forbidden-role test.

## Deviations from the PRD and decisions to confirm
Decided with the project owner before coding unless noted.
1. **AI-02**: a score can optionally link to a learning objective (new `LearningObjective` table and `Grade.LearningObjectiveId`). Grades linked to an objective still count in the final grade.
2. **AI-03**: photos only, PDFs are refused with a clear message. The national ID number, the parents' names and the grades are shown as information only; the application form is unchanged. Photos are sent to the provider as they are, so student names cannot be replaced by IDs; with Ollama they stay on the machine.
3. **AI-04**: eight tools (more than the PRD examples), multi-turn with the history kept by the browser, **no SignalR streaming**: the whole answer arrives at once.
4. **Data model beyond the PRD**: `TeacherNote`, `LearningObjective`, `AiUsageLog` (`Provider`, `Succeeded`, `IsBulk`), `AchievementDescription.IsAiDrafted`, `ApplicantDocument.ExtractedData`/`ExtractedAt`, and for F-11 `AuditLog.EntityId` (the moment is `CreatedAt`) and the new `EmailMessage` queue. The PRD, ERD and system design are updated.
5. **Audit log scope**: only grades, attendance records, requests and approval steps (what the PRD names). Report cards and admission decisions are not audited.
6. **Email events**: request decided (to the requester), admission result announced, announcement published. WhatsApp is not built. **The monthly progress summaries are sent as in-app notifications only, not by email; this is open for confirmation** because the exit criterion says "sent after review".
7. **Email goes through a queue** (`EmailMessage` plus a Hangfire job every minute) instead of sending inside the request, so a mail server that is down never fails a request. The built-in `System.Net.Mail` client is used; no package was added.
8. **PWA**: a hand-written manifest and service worker that cache the app shell and hashed build files only; the API and Keycloak are never cached. No package was added.
9. **Mailpit** (`axllent/mailpit`, MIT) was added to `infra/docker-compose.yml` as the local mail catcher.
10. **Claude and Ollama clients** are tested against scripted servers only; the test sets run against the mock. Running them against a real model is a manual step whenever a prompt changes (ADR-004).
11. **Free provider**: local Ollama; the default stays Mock; Claude Haiku 5.5 is used when an API key is set.

## Known issues
- `AchievementDescriptionEndpointTests.A_bulk_run_drafts_every_student_with_scores_and_the_texts_are_not_the_same` failed once in an earlier run (2 drafted, 1 failed) and passed in every later run, including the runs of this evidence. The cause is not found; the behaviour is safe, because a failed student is reported and can be drafted again.
- The Vitest test `AssignmentsHomePage > shows a teacher the assignments they gave` can time out when the machine is busy; it passes alone and passed in the evidence run.
- The generated API client is still hand-written for the endpoints of Phase 4 and later.
- There is no screen for the AI allowance of a user (`AiGateway.GetAllowanceAsync` exists, no endpoint).

## Data the end-to-end tests leave behind
Requests with an "E2E ..." reason stay in the development database (the audit spec withdraws its request afterwards). The principal assistant spec uses one of the 10 daily AI calls of the demo principal.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report-run-1/index.html`), the text log of the run (`logs/`), and screenshots of the new screens (`screenshots/`)

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README).
