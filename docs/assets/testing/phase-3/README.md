# Phase 3 test evidence

Generated on 2026-10-28 from the `feat/phase-3-classroom` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API with its Hangfire jobs, and the real web app. PRD exit
criterion of the phase: "Attendance and assignment flows pass end-to-end tests."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 116, Application 168) | 284 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 389 passed |
| Frontend unit tests (Vitest) | 421 passed |
| End-to-end tests (Playwright, Keycloak + API + Hangfire + SeaweedFS + web) | 39 passed (37 desktop at 1440 px, 2 phone at 390 px) |

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 836 | 931 | 89.8% |
| SchoMans.Application | 3288 | 3604 | 91.2% |
| SchoMans.Domain | 1179 | 1266 | 93.1% |
| SchoMans.Infrastructure | 716 | 772 | 92.7% |
| **All** | **6019** | **6573** | **91.6%** |

Frontend (Vitest, v8): statements 91.5%, branches 87.5%, functions 88.8%, lines 91.7%. Excluded: the generated API
client, test helpers and `main.tsx`.

## What the tests prove
- **QR attendance** (`attendance.spec.ts`, `AttendanceEndpointTests`, `AttendanceTimingTests`): the teacher opens a
  lesson; the QR code rotates every minute by the Hangfire job and the new code reaches the teacher's screen live; a
  code older than the 10 second grace is refused (a photo of the screen is useless), the current one checks the student
  in; the teacher sees the check-in without a reload; a second scan changes nothing; the teacher marks a student sick by
  hand; closing marks everybody who did not scan absent; forgotten sessions close by themselves; the optional location
  check refuses a scan from outside the school. The month report and its Excel export, and the student and parent views, work.
- **Assignments** (`assignments.spec.ts`, `AssignmentEndpointTests`): the teacher gives work to a classroom with an
  attachment uploaded straight to SeaweedFS; the student is notified at once, opens the attachment through a short-lived
  link, hands in a file and a note (wrong types are refused before upload), the teacher scores it with feedback, the
  student is notified and the work becomes final; the parent sees the score of their child. Late work is marked late, the
  reminder the day before goes once to students who have not handed in.
- **Timetable** (`timetable.spec.ts`, `ScheduleEndpointTests`): admin staff add and remove lessons; a classroom or a
  teacher cannot be in two lessons at once, back-to-back lessons are fine.
- **Roles**: only teachers open sessions, only students scan, admin staff cannot open assignments, parents see only
  their own children, and a student who types the address of a teacher page gets the "no access" message.

Time-dependent rules (rotation, grace, forgotten sessions, late work, reminders) are tested with a clock the test moves
by hand, so no test waits for real minutes except the single end-to-end test of the rotation by the real job.

## Bugs the tests found during this phase
- The check-in page sent nothing when React mounted its effects twice in development, and ignored a second code in the
  same page: callbacks passed to `mutate` are dropped when the component is unmounted (end-to-end).
- A teacher's review list could show a count that was 30 seconds old, and a student's assignment page stayed old after a
  score notification arrived; both now refresh (end-to-end).
- Picking several attachments at once could pass the limit of five; forms used the browser's own validation messages
  instead of ours (unit tests).
- A `DayOfWeek` stored as text could not be sorted with a cast in SQL; the timetable order is spelled out (integration tests).

## Deviations from the PRD and decisions to confirm
Choices made where the PRD is silent; the first four were decided with the project owner before coding.
1. **The timetable (`Schedule`) has an admin screen** and a demo timetable (Monday to Friday, four lessons a day). The PRD
   data model has the entity but no screen. It also decides which classrooms and subjects a teacher may give assignments for.
2. **Both ways to scan**: the QR code holds the check-in address (`/attend?t=...`) so any camera app works, and the app
   has its own scanner page. One extra package for each direction (qrcode to draw, qr-scanner to read).
3. **Assignment scores stay on the submission** in Phase 3. The PRD says the score flows into F-05 automatically; that
   flow, the weights and the report cards belong to Phase 4. `GradeComponent` exists now because `Assignment` names it.
4. **Handing in again is allowed until the teacher scores the work**; after that it is final. Work after the due date is
   accepted and marked late.
5. **Sessions**: one per lesson per day, opened only on the lesson's weekday. A session left open is closed 15 minutes
   after the lesson ends (and never earlier than 30 minutes after it opened), marking the students who did not scan absent.
6. **The location check** is configurable (`Attendance:Geofence`) and off by default; the PRD calls it optional.
7. **Parents** get read-only pages for the attendance and assignments of their children (`/my-children/...`), as the
   authorization matrix says; admin staff do not see assignments.
8. **Attachments** may be PDF, images or Word, Excel and PowerPoint files, up to 5 files of 10 MB; hand-in files are PDF or
   images up to 10 MB, as the PRD says.
9. **Notifications**: a new assignment, a score, and one reminder per student per assignment at 07:00 WIB the day before the due date.
10. **Deleting is guarded**: a teacher, classroom or subject used by the timetable or by assignments, a lesson with attendance,
    and an assignment with scored work cannot be deleted.
11. **The Hangfire dashboard** (`/hangfire`) is for admin staff only. The browser cannot send a header to a page, so the app
    links with the token once and the API keeps it in an HttpOnly cookie for that path.
12. **Packages added** (all approved): Hangfire (Core, SqlServer, AspNetCore; LGPL) on the backend; `qrcode`, `qr-scanner`
    and `@types/qrcode` on the frontend.
13. The end-to-end tests leave fictional data behind: one-minute lessons with their attendance and one scored assignment
    per run, named "E2E ...". Everything else they create they delete.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report/index.html`, one screenshot per step) and screenshots of the QR
  session screen, the check-in results, the month report, the assignment flow and the timetable

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README).
