# Phase 2 test evidence

Generated on 2026-10-28 from the `feat/phase-2-core-data` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API and the real web app. PRD exit criterion of the phase: "Excel
import works; announcements appear in real time."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 74, Application 119) | 193 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 321 passed |
| Frontend unit tests (Vitest) | 299 passed |
| End-to-end tests (Playwright, Keycloak + API + SeaweedFS + web) | 31 passed (29 desktop at 1440 px, 2 phone at 390 px) |

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 621 | 672 | 92.4% |
| SchoMans.Application | 1844 | 2005 | 92.0% |
| SchoMans.Domain | 712 | 760 | 93.7% |
| SchoMans.Infrastructure | 548 | 569 | 96.3% |
| **All** | **3725** | **4006** | **93.0%** |

Frontend (Vitest, v8): statements 89.7%, branches 86.7%, functions 87.3%, lines 89.5%. Excluded: the generated API
client, test helpers and `main.tsx`.

## What the end-to-end tests prove
- **Excel import works** (`masterdata.spec.ts`): the admin builds an .xlsx file, imports it through the screen, sees
  "2 of 3 rows were imported" and the failed row with its reason, and the good rows appear in the student list.
- **Announcements appear in real time** (`announcements.spec.ts`): three browsers at once. The admin publishes an
  announcement for teachers; the teacher's open page shows a toast and a badge without a reload, a student outside the
  audience gets nothing, and opening it from the bell clears the badge.
- **News with images** (`news.spec.ts`): the editor, the cover image uploaded straight to SeaweedFS through a presigned URL
  (with CORS), publishing, and a guest reading the article with its own title and meta tags. A draft is not readable by a guest.
- **Public content**: an announcement marked public shows on the landing page for a guest; a private one does not.
- **Roles**: a teacher and the principal see lists without any button to change them, a student has no management
  pages, and a teacher who types the address of a page for another role gets the "no access" message.

The tests clean up what they create, so the demo data stays as it was. The academic year rollover is deliberately **not**
run end to end (it would change the shared demo data for good); its wizard is checked up to the validation step, and
the whole flow is covered by the unit and integration tests.

## Bugs the tests found during this phase
- The toast stack used navigation outside the router and crashed the page on the first toast (end-to-end).
- Saving or publishing changes a record's row version, which rebuilds the form; callbacks passed to `mutate` were
  dropped, so the "published" toast and the redirect never happened (end-to-end, now also a unit test).
- A test of the presigned upload URL assumed the SDK says exactly 300 seconds; it rounds to 299 to 301 depending on timing.

## Deviations from the PRD and decisions to confirm
These are choices made where the PRD is silent. None changes a stated requirement.
1. **NISN is exactly 10 digits and NIP exactly 18 digits** (official formats); the PRD names the fields but not the format.
2. **Accounts of imported students** are not created in Phase 2. A student gets an account when they first sign in with
   the email on record; sending the invitation email waits for Phase 6 (decision of the project owner).
3. **A student has a status** (`Active`, `Graduated`) and the academic year has **terms**; both are needed by the rollover
   and by grading in a later phase.
4. **Academic year rollover** works from a mapping the admin defines per classroom (move up to a named classroom with a
   homeroom teacher, or graduate), applied in one transaction (decision of the project owner).
5. **Announcement audience**: no roles and no classrooms means every school role (applicants are not included); roles
   and classrooms combine as "these roles within these classrooms"; a classroom reaches its students, their parents and
   its homeroom teacher. A published announcement keeps its audience fixed because the notifications are already out.
6. **Announcements can be public** (`IsPublic`), readable by guests and used later by the landing page chatbot.
7. **News and announcement text** is written in a rich text editor (Tiptap) and stored as HTML that the server
   sanitizes with an allow-list; the browser never receives unsanitized HTML.
8. **Rate limits** on the public endpoints and on upload URLs, and a **sitemap** for published news (search engines).
9. **Numbers in JSON are strict numbers** so the OpenAPI document and the generated client agree.
10. **Packages added** (all approved): ClosedXML, AWSSDK.S3, HtmlSanitizer on the backend; Tiptap, `@microsoft/signalr`
    and two Radix dialog packages on the frontend. The SignalR test client for the integration tests is hand written
    instead of adding a package.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report/index.html`, one screenshot per step), `playwright-junit.xml`,
  and screenshots of the news editor, the public article, the landing page, the real-time toast, master data and the import result

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README).
