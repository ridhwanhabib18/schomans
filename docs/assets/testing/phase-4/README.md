# Phase 4 test evidence

Generated on 2026-10-28 from the `feat/phase-4-academics` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API, and the real web app. PRD exit criterion of the phase:
"Report-card PDFs generate; charts show per role."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 140, Application 173) | 313 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 442 passed |
| Frontend unit tests (Vitest) | 459 passed (38 files) |
| End-to-end tests (Playwright, Keycloak + API + SeaweedFS + web) | 41 tests (39 desktop at 1440 px, 2 phone at 390 px); every test passed on the final code, but not in one uninterrupted run, see below |

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 921 | 1016 | 90.6% |
| SchoMans.Application | 3994 | 4346 | 91.9% |
| SchoMans.Domain | 1427 | 1558 | 91.6% |
| SchoMans.Infrastructure | 932 | 997 | 93.5% |
| **All** | **7274** | **7917** | **91.9%** |

Frontend (Vitest, v8): statements 91.5%, branches 87.3%, functions 88.7%, lines 91.7%. Excluded: the generated API client, test helpers and `main.tsx`.

## What the tests prove
- **Grades flow in** (`GradeEndpointTests`): scoring a submission creates the assignment grade of the subject, and
  scoring it again corrects that grade instead of adding one. Teachers enter quiz, midterm and final scores; entering one
  again corrects it. Work scored in Phase 3 is turned into grades by the migration.
- **Final grade and band** (`GradeCalculatorTests`, `GradeEndpointTests`, `GradeBandEndpointTests`): the final grade is
  the weighted average of the component averages; a component without scores is left out and its weight is shared over
  the others. A grade below the minimum passing score of the subject is flagged. The admin replaces the band scale and
  grades get the new labels; a scale without a band at 0 or with duplicates is refused.
- **Weights** (`GradeEndpointTests`): only a teacher of the subject sets its weights; they must cover the four components
  and add up to 100; they change the final grade at once.
- **Publishing locks** (`GradeEndpointTests`, `ReportCardEndpointTests`): only the homeroom teacher publishes or
  unpublishes; once published, entering a score, rescoring an assignment and editing a description are refused with
  409 until the grades are unpublished.
- **Families see published grades only** (`ReportCardEndpointTests`, `AnalyticsEndpointTests`): a student and a parent
  get the grades of the term, with bands and achievement descriptions, only after publication; a classmate or another
  parent's child is refused; their monthly chart shows grades only once they are published.
- **Report-card PDFs generate** (`ReportCardEndpointTests`, `ReportCardPdfRendererTests`): the homeroom teacher generates
  one PDF per student after publishing; the PDFs land in private storage; the student, the homeroom teacher and the
  principal download them through short-lived links; unpublishing withdraws them from the family. `sample-report-card.pdf`
  is a report card made by the real renderer.
- **Charts show per role** (`AnalyticsEndpointTests`, dashboard Vitest tests): the principal gets the attendance trend
  per classroom and the average final grade per subject, a teacher the students below 80% attendance or the minimum
  passing score of their classes, a student or parent the monthly grades and attendance of the child; admin and
  applicants get no analytics.
- **Roles**: every new endpoint has a success, a validation failure and a forbidden-role test.

## Deviations from the PRD and decisions to confirm
Choices made where the PRD is silent; the first six were decided with the project owner before coding.
1. **Grade bands are configurable** by the admin (new table `GradeBand`, default A from 90, B from 80, C from 70, D from
   60, E below 60). The PRD only said the band is calculated; the PRD, the data model and the ERD now describe it.
2. **A component without scores is left out** of the final grade and its weight is shared over the scored components,
   instead of counting as 0.
3. **The teacher of the subject sets its weights** (all four components, adding up to 100). Without own weights a subject
   uses 30/20/20/30 (assignment, quiz, midterm, final).
4. **Published grades are locked** until the homeroom teacher unpublishes them. Weights stay editable because they belong
   to the subject, not to a term.
5. **Achievement descriptions are written by the teacher** and count as accepted when saved; AI drafts that need a review
   come in Phase 6. The letterhead is a fictional school from configuration with a logo file in the repository.
6. **The "requests awaiting approval" card** of the principal's dashboard waits for Phase 5, where approvals (F-08) are built.
7. **`LearningObjective` is not built yet.** It is optional on `Grade` in the data model and only used by the AI features
   of Phase 6.
8. **Report cards need published grades**: generating before publication is refused, and generating again replaces the
   PDFs. Assignment scores enter the grade book automatically; quiz, midterm and final scores are entered by the teacher
   (online quizzes come in Phase 7).
9. **The teacher's attention list** uses attendance in the active term (present / all records below 80%) and the final
   grade against the minimum passing score. A homeroom teacher sees every subject of the class; a subject teacher only
   their own subjects.
10. **Demo data** has unpublished quiz and midterm scores for every student of the active term, so the grade book and the
    principal's charts show data; the homeroom teacher publishes during the demo.
11. **Packages added** (both approved): QuestPDF (Community License) on the backend, Recharts (MIT) on the frontend.
12. **Admin staff** see no analytics; the PRD names dashboards only for the principal, teachers and parents.
13. **Students and parents get their own term list** (`GET /report-cards/terms`, own student only) instead of access to the
    academic years, so the authorization matrix does not change.
14. **The publish and generate buttons show for every teacher**; the API refuses the ones who are not the homeroom teacher of the
    class (403 `grades.not_homeroom`, shown in the dialog). The principal may read the grade book through the API but has no screen for it yet.
15. **The teacher's attention list shows the 10 most urgent students** and the total, with a "Show all" button.
16. **The generated API client** (`npm run api:generate`) was regenerated; the Phase 4 hooks are hand-written beside it and use the
    same endpoints.

## How the end-to-end tests ran (read this before trusting the number)
The machine that ran them stalled now and then (a laptop that went to sleep took WSL, and with it SQL Server, Keycloak and
SeaweedFS, down for one run; other runs saw single requests of 8 to 15 seconds), and the older specs wait 5 seconds for
a page. So the result is told as it happened, not as one clean green run:
1. **Full run** (`logs/run-1-full.log`): 31 passed, 7 failed, 3 not run (24.8 minutes). Failed: `announcements` (2), the QR
   attendance story, the Keycloak self-registration, `master data` (2) and the new `grading` story.
2. **Rerun of those ten** (`logs/run-2-failed-and-skipped.log`): 9 passed. The six older specs passed unchanged, so they
   were timeouts under load; only `grading` failed again.
3. **`grading` after the fixes below** (`logs/run-3-grading-after-spec-fix.log`): both stories passed. Later reruns of
   `grading` and the layout specs (desktop and phone) after the visual fixes also passed.

Not done: one uninterrupted 41-test run on the final code. Run `cd web && npm run test:e2e` on an idle machine for that.

## Bugs the tests and the screenshots found during this phase
- Students and parents got a 403 on the grades page: it filled its term picker from the academic years, which only staff may
  list. A new endpoint `GET /report-cards/terms` answers for the own student only (end-to-end test; the unit tests had mocked it).
- Generating the report cards of a class of 30 took more than 8 seconds because every PDF was drawn and uploaded in turn;
  drawing and uploading now run four at a time (about 2.7 seconds) (end-to-end).
- The parent dashboard was 71 px wider than a 390 px phone: a screen-reader table inside the chart card kept its own width
  (end-to-end layout test).
- The demo grades collided with the first grading spec (a typed score equal to the stored one saves nothing), the PDF popup
  of headless Chromium became a download and made the spec wait forever, and the chained requests of the student page
  need more than 5 seconds after the API starts (the spec now checks the signed storage request and waits longer).
- Found only by looking at the screenshots: the grade book showed a raw translation error under its title (two `description`
  keys in one JSON object), the subject names of the principal's bar chart overlapped, and the teacher's attention list
  was 4300 px tall (it shows the first 10 and a total, with a "Show all" button). A unit test now checks that every
  translation key of the new pages resolves to text.
- The Phase 1 `demo-login` specs still expected an empty dashboard for every role; the four roles with a dashboard now check
  for it.
- The unit test run of the frontend once failed to start a worker under load and silently skipped one file; it is rerun on one
  worker, which is the run reported above.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report of the first full run (`playwright-report-run-1/index.html`), the text logs of the three runs (`logs/`), and screenshots of the grade book, the student and parent grade pages and the dashboards of each role (`screenshots/`)
- `sample-report-card.pdf`: a report card rendered by the real QuestPDF renderer

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README).
