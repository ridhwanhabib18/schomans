# Phase 7 test evidence

Generated on 2026-11-05 from the `feat/phase-7-ai-teaching` branch. Everything ran against real services: SQL Server 2022,
Keycloak 26.8 and SeaweedFS 4.50 in Docker, the real API, and the real web app. The AI provider is the deterministic mock
(the default, see ADR-004). PRD exit criterion of the phase: "Each AI feature's test set passes; chatbot refuses questions outside its sources."

| Suite | Result |
| --- | --- |
| Backend unit tests (Domain 220, Application 350) | 570 passed |
| Backend integration tests (WebApplicationFactory + SQL Server + SeaweedFS) | 721 passed |
| Frontend unit tests (Vitest) | 711 passed (62 files) |
| End-to-end tests (Playwright, 56 tests: 53 of earlier phases and 3 new, one uninterrupted run, 11.6 minutes) | 56 passed |

Backend total: 1291 tests.

## Coverage (PRD target: at least 70%)

Backend line coverage, all test projects merged. Excluded: EF Core migrations, `Program.cs`, and compiler-generated files.

| Assembly | Lines covered | Total lines | Line coverage |
| --- | ---: | ---: | ---: |
| SchoMans.Api | 1373 | 1468 | 93.5% |
| SchoMans.Application | 7674 | 8247 | 93.1% |
| SchoMans.Domain | 2584 | 2922 | 88.4% |
| SchoMans.Infrastructure | 1901 | 2060 | 92.3% |
| **All** | **13532** | **14697** | **92.1%** |

Frontend (Vitest, v8): statements 91.85%, branches 86.53%, functions 88.27%, lines 92.16%. Excluded: the generated API client, test helpers and `main.tsx`.

## What the tests prove
- **AI-05 rubric feedback** (`FeedbackDraft` tests, `FeedbackUi`): the teacher defines a rubric; the AI drafts a score per criterion with a reason and one suggestion, one student at a time or in bulk;
  the draft keeps a copy of the criteria; the teacher accepts, edits or rejects it, and an accepted draft is final; the score always comes from the teacher.
- **AI-06 question generator** (`QuestionPromptTests`, `QuestionBankEndpointTests`, `QuestionBank.test.tsx`): the text of a PDF of up to 30 pages is read on the server and only the text goes to the AI;
  the JSON is validated before it is shown; the teacher edits and saves to a bank per subject; only the author changes or deletes a question, and not once it is in a quiz.
- **Online quizzes** (`QuizTests`, `QuizScoringTests`, `QuizEndpointTests` with 27 cases, `Quiz.test.tsx` with 24 cases, `phase-7.spec.ts`): built from the bank; taken once, with no time limit, until the due date;
  multiple choice is scored at once, short answers wait for the points of the teacher (0 to what the question is worth); the student and the parent see the score, the key and the explanations only after the due date;
  quiz scores are averaged with a Quiz score entered by hand in the grade book.
- **AI-08 chatbot** (`ChatbotTests` with 20 cases, `ChatbotEndpointTests` with 14, `phase-7.spec.ts`): answers only from the 5 best passages of the published news and announcements a user may see;
  every answer links to its source; **a question that is not in the sources gets "I could not find this" with the staff contact, never a guess**; guests are limited to 6 questions per minute per IP; a parent's question counts as one daily AI call.
- **AI-09 drafts** (`DraftPromptTests`, `DocumentDraftEndpointTests`, `GeneralLetterEndpointTests`, `draft-dialog.test.tsx`, `phase-7.spec.ts`): the draft goes into the announcement editor and nothing is saved or sent until the person saves it;
  general letters (invitation, notice) get the letterhead, the number and the signature from the system and a PDF in storage.
- **Roles**: every new endpoint has a success, a validation failure and a forbidden-role test.

## Deviations from the PRD and decisions to confirm
Decided with the project owner before coding unless noted.
1. **No SQL full-text search** (AI-08): the SQL Server image has no full-text component, so passages are scored per keyword with `LIKE`, and the scores are summed in the application. A keyword can also match text in the HTML of an article.
2. **New package**: PdfPig (Apache-2.0), approved before use. Its package id is `PdfPig` (0.1.16, author UglyToad); the PRD table said `UglyToad.PdfPig`, which only has third-party builds. The PRD table is corrected.
3. **Data model beyond the PRD**: `Rubric`, `FeedbackDraft`, `GeneralLetter`, `Question`, `AssignmentQuestion`, and `Submission.AnswersJson` / `ShortPointsJson`. The PRD, ERD and system design are updated.
4. **AI-05** handles typed work only (an uploaded file is not read); an accepted draft is final; a draft keeps a copy of the criteria.
5. **Question bank**: per subject, shared by the teachers of the subject, only the author edits or deletes; a question used in a quiz cannot be changed or deleted (the PRD schema refers to `QuestionId`).
6. **Quizzes**: taken once, no time limit, closed at the due date (decided, not in the PRD text); every question counts the same by default; quiz scores are averaged with a hand-entered Quiz score, and the PRD sentence is corrected.
7. **General letters** need codes for the number: `UND` (invitation) and `PEM` (notice), which the PRD does not define.
8. **Chatbot limits**: a parent's question counts toward the 10 daily AI calls; guests only toward the monthly budget, plus 6 questions per minute per IP.
9. **Architecture wording**: `system-design.md` called the project a "modular monolith". The module boundaries are not enforced (one `AppDbContext`, a few cross-feature calls), so the document now says monolith with Clean Architecture and vertical slices, and the technical Q&A explains it.
10. **Claude and Ollama clients** are tested against scripted servers only; the test sets run against the mock (ADR-004).

## Found by the end-to-end run
The quiz scoring dialog did not close and showed no toast after the points were saved, because its form was remounted when the score changed. Fixed (the key is the submission only) in the same phase; the Vitest case for the toast cannot see it because its mock does not change the score.

## Known issues
- `AchievementDescriptionEndpointTests.A_bulk_run_...` (AI-02) failed once in an earlier phase and passed in every later run, including this one. The cause is not found.
- The Vitest wait is widened (5 s per wait, 15 s per test) and Playwright waits 15 s per expectation, because 60 jsdom files in parallel and real services on a busy machine exceeded the defaults. No test was removed or skipped.
- The generated API client is still hand-written for the endpoints of Phase 4 and later (`assignmentDto.type` was added by hand).
- There is no screen for the AI allowance of a user (`AiGateway.GetAllowanceAsync` exists, no endpoint).

## Data the end-to-end tests leave behind
The question bank keeps the two questions of the quiz test ("E2E" is not in their text; they contain a timestamp), because a question that was in a quiz cannot be deleted. Announcements and quizzes of the tests are deleted afterwards.
The draft test uses one of the 10 daily AI calls of the demo admin staff user.

## Contents
- `backend/`: TRX results of the three test projects, the Cobertura coverage reports, and `coverage-summary.md`
- `frontend/`: `vitest-junit.xml` and the HTML coverage report (`coverage-report/index.html`)
- `e2e/`: the Playwright HTML report (`playwright-report-run-1/index.html`), the text log of the run (`logs/`), and screenshots of the new flows (`screenshots/`)

## How these were produced
```bash
docker compose -f infra/docker-compose.yml up -d
dotnet test --collect:"XPlat Code Coverage" --logger trx                      # backend
cd web && npm run test:coverage                                              # frontend
cd web && npm run test:e2e                                                   # end to end
```
Where Docker is only reachable from WSL, set `SCHOMANS_TEST_SQL` and `SCHOMANS_TEST_S3` for the integration tests (see the README). Stop a running API before `dotnet test`: it locks the DLLs the integration project copies.
