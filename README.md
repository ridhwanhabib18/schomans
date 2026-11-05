# SchoMans — School Management System

[Bahasa Indonesia](README.id.md)

SchoMans brings attendance, grading, assignments, admissions, announcements, administrative
approvals, and AI assistance into one platform for teachers, students, parents, and school staff.

> Status: all eight phases are built and tested. The deployment files are ready ([deployment guide](docs/en/deployment.md)); the live demo link is added when the demo is deployed.

## Highlights
- Role-based access for 6 roles (Keycloak, OIDC + PKCE)
- Real-time QR attendance with SignalR
- PDF report cards and letters, analytics dashboards
- AI features with human review (Claude API)

## Tech stack
.NET 10 · ASP.NET Core Minimal API · EF Core · SQL Server · Keycloak · SignalR · S3 ·
React 19 · TypeScript · TanStack Query · Zustand · Tailwind · Playwright

## Documentation
- [Product requirements](docs/en/PRD.md)
- [System design](docs/en/system-design.md)
- [Architecture decisions](docs/en/adr/)
- [Technical Q&A](docs/en/technical-qa.md)
- [Deployment guide](docs/en/deployment.md)
- Generated from the code: [API reference](docs/en/generated/api-reference.md), [authorization matrix](docs/en/generated/authorization-matrix.md), [jobs and real-time events](docs/en/generated/jobs-and-realtime.md), [ERD](docs/en/diagrams/erd.mmd)
- [Demo video script](docs/en/demo-video-script.md)
- Test evidence per phase: [`docs/assets/testing/`](docs/assets/testing/)

## Getting started
Requirements: .NET 10 SDK, Node.js 24, Docker (with Compose).

```bash
# 1. Start SQL Server, Keycloak and SeaweedFS (S3 storage for images) (copy the example env file first)
cp infra/.env.example infra/.env
docker compose -f infra/docker-compose.yml --env-file infra/.env up -d

# 2. Run the API (applies migrations and seeds the fictional demo school in Development)
dotnet run --project src/SchoMans.Api

# 3. Run the web app
cd web && npm install && npm run dev
```

Open http://localhost:5173. In development the login page has one-click demo buttons for each role; the
demo accounts are `principal@`, `teacher@`, `student@`, `parent@`, `admin@`, and `applicant@schomans.demo`
(password `Demo123!`, local development only). The API documentation is at http://localhost:5080/scalar,
Keycloak at http://localhost:8080 and the S3 endpoint of SeaweedFS at http://localhost:8333.

| Command | What it does |
| --- | --- |
| `dotnet test` | Unit and integration tests (integration tests start SQL Server and SeaweedFS with Testcontainers) |
| `cd web && npm test` | Frontend unit tests (Vitest) |
| `cd web && npm run test:e2e` | End-to-end tests (Playwright); needs the containers from step 1 |
| `cd web && npm run api:generate` | Regenerates the TypeScript API client (the API must be running) |
| `dotnet run --project tools/SchoMans.DocGen` | Writes the documents derived from the code (`-- --check` fails when they are out of date or a diagram names a route that is gone) |
| `cd web && npm run capture:screenshots` | Captures every page of every role for the gallery below (`CAPTURE_URL` points it at a deployed demo) |
| `cd web && npm run capture:flows` | Records the main flows as GIFs (needs Python and Pillow) |

Background jobs (QR code rotation every minute, closing forgotten attendance sessions, the assignment reminders) run in the
API with Hangfire; admin staff can watch them from the "Background jobs" link of the app, which opens `/hangfire`. Set
`Jobs:Enabled` to `false` to run the API without them.

If Docker is only reachable from WSL, the integration tests can use an already running SQL Server instead of
Testcontainers: set `SCHOMANS_TEST_SQL` to its connection string without a database name, for example
`Server=127.0.0.1,1433;User Id=sa;Password=...;TrustServerCertificate=True;Encrypt=False`, and `SCHOMANS_TEST_S3` to
the endpoint of a running SeaweedFS, for example `http://127.0.0.1:8333`.

## Screenshots

Every picture shows the fictional demo school and is made by `cd web && npm run capture:screenshots` (all pages of all roles, desktop and phone size, in [`docs/assets/screenshots/`](docs/assets/screenshots/)).

### Main flows

<p><img src="docs/assets/flows/qr-attendance.gif" alt="QR attendance: the teacher shows the code, the student checks in, the teacher sees it live" width="720"><br><em>QR attendance: the teacher shows the code, the student checks in, the teacher sees it live</em></p>

<p><img src="docs/assets/flows/assignments.gif" alt="Assignments: given, handed in, scored, followed by the student and the parent" width="720"><br><em>Assignments: given, handed in, scored, followed by the student and the parent</em></p>

<p><img src="docs/assets/flows/approvals.gif" alt="Approvals: a parent asks for a leave, the teacher and the principal decide" width="720"><br><em>Approvals: a parent asks for a leave, the teacher and the principal decide</em></p>

<p><img src="docs/assets/flows/admissions.gif" alt="Admissions: the applicant and the staff" width="720"><br><em>Admissions: the applicant and the staff</em></p>

<p><img src="docs/assets/flows/ai-features.gif" alt="AI features: assistant, draft in the editor, question bank, school chatbot" width="720"><br><em>AI features: assistant, draft in the editor, question bank, school chatbot</em></p>

### Guest

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/guest/home-desktop.png" alt="Landing page with the school chatbot" width="360"><br><sub>Landing page with the school chatbot</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/guest/news-desktop.png" alt="Public news" width="360"><br><sub>Public news</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/guest/home-mobile.png" alt="Landing page on a phone" width="180"><br><sub>Landing page on a phone</sub></td></tr>
</table>

### Principal

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/principal/dashboard-desktop.png" alt="Dashboard" width="360"><br><sub>Dashboard</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/principal/assistant-desktop.png" alt="AI assistant that answers from the data, with its sources" width="360"><br><sub>AI assistant that answers from the data, with its sources</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/principal/requests-inbox-desktop.png" alt="Approvals inbox" width="360"><br><sub>Approvals inbox</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/principal/manage-audit-log-desktop.png" alt="Audit log" width="360"><br><sub>Audit log</sub></td></tr>
</table>

### Admin staff

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-students-desktop.png" alt="Students" width="360"><br><sub>Students</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-admissions-desktop.png" alt="Admissions" width="360"><br><sub>Admissions</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-letters-desktop.png" alt="General letters with letterhead, number and signature" width="360"><br><sub>General letters with letterhead, number and signature</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/admin-staff/manage-announcements-desktop.png" alt="Announcements" width="360"><br><sub>Announcements</sub></td></tr>
</table>

### Teacher

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/attendance-desktop.png" alt="Today's lessons and QR attendance" width="360"><br><sub>Today's lessons and QR attendance</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/assignments-questions-desktop.png" alt="Question bank" width="360"><br><sub>Question bank</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/grading-desktop.png" alt="Grade book" width="360"><br><sub>Grade book</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/progress-summaries-desktop.png" alt="Progress summaries for review" width="360"><br><sub>Progress summaries for review</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/teacher/dashboard-mobile.png" alt="Dashboard on a phone" width="180"><br><sub>Dashboard on a phone</sub></td></tr>
</table>

### Student

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/dashboard-desktop.png" alt="Dashboard" width="360"><br><sub>Dashboard</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/student/assignments-desktop.png" alt="Assignments" width="360"><br><sub>Assignments</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/grades-me-desktop.png" alt="My grades" width="360"><br><sub>My grades</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/student/attendance-scan-desktop.png" alt="Scan the lesson code" width="360"><br><sub>Scan the lesson code</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/student/dashboard-mobile.png" alt="Dashboard on a phone" width="180"><br><sub>Dashboard on a phone</sub></td></tr>
</table>

### Parent

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/parent/dashboard-desktop.png" alt="Dashboard with both children" width="360"><br><sub>Dashboard with both children</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/parent/my-children-assignments-desktop.png" alt="Assignments of the children" width="360"><br><sub>Assignments of the children</sub></td></tr>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/parent/my-children-summaries-desktop.png" alt="Progress summaries sent after review" width="360"><br><sub>Progress summaries sent after review</sub></td><td align="center" valign="top"><img src="docs/assets/screenshots/parent/requests-desktop.png" alt="Requests for leave and letters" width="360"><br><sub>Requests for leave and letters</sub></td></tr>
</table>

### Applicant

<table>
<tr><td align="center" valign="top"><img src="docs/assets/screenshots/applicant/admissions-desktop.png" alt="Application with documents and status" width="360"><br><sub>Application with documents and status</sub></td></tr>
</table>
