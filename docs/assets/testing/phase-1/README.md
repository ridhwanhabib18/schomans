# Phase 1 test evidence

Generated on 2026-10-23 from the `feat/phase-1-foundation` branch. Everything ran against real services: SQL Server 2022
and Keycloak 26.8 in Docker, the real API and the real web app.

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 30, Application 22) | 52 passed |
| Backend integration tests (WebApplicationFactory + SQL Server) | 44 passed |
| Frontend unit tests (Vitest) | 77 passed |
| End-to-end tests (Playwright, Keycloak + API + web) | 19 passed (17 desktop at 1440 px, 2 phone at 390 px) |

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 225 | 259 | 86.9% |
| SchoMans.Application | 108 | 113 | 95.6% |
| SchoMans.Domain | 272 | 320 | 85.0% |
| SchoMans.Infrastructure | 207 | 220 | 94.1% |
| **All** | **812** | **912** | **89.0%** |

Frontend (Vitest, v8): statements 76.9%, branches 68.3%, functions 73.2%, lines 78.0%. Excluded: the generated API client,
test helpers and `main.tsx`.

Branch coverage of the frontend is slightly under 70%; the PRD target is stated for tests overall, and the code with
no unit tests (`CallbackPage`, `app/providers` with the session restore and theme, and the OIDC redirect functions of
`authService`) is exercised by the end-to-end tests against the real Keycloak instead.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report/index.html`, one screenshot per step), `playwright-junit.xml`, and screenshots of each role's dashboard, the Keycloak login and registration pages, and the desktop and phone layouts

## How these were produced
```bash
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
