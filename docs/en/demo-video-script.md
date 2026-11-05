# Demo video script (about 2 minutes)

A storyboard for the portfolio video. It uses only the fictional demo school; record it against the deployed demo (or the local stack), at 1440 × 900 for the desktop scenes and in the phone view of the browser for the two phone scenes. The steps are the ones the end-to-end tests walk through, so every click below is known to work. Keep the narration short: say what the person in the scene wants, not how the code works.

Before recording: sign out everywhere, use a clean browser profile (no bookmarks bar, no extensions), set the browser zoom to 100%, and close the notification bell toasts of earlier tests. Sign in with the one-click demo buttons; the names on screen are fictional.

| Time | Scene | What to show | Narration (English) |
| --- | --- | --- | --- |
| 0:00 | Landing page | The public landing page, the news list, then ask the chatbot "When does registration open?" and show the answer with its source link | "SchoMans puts a school's daily work in one place. Visitors can already ask the school a question, and the answer comes only from what the school has published." |
| 0:15 | QR attendance | **Teacher** opens today's lesson and shows the QR code. **Student** (phone view) opens the check-in address and sees "You are checked in". Back on the teacher screen the count goes up by itself. | "A teacher opens a lesson and shows a code that changes every minute. A student checks in with the phone, and the teacher sees it at once, without refreshing." |
| 0:40 | Assignments and a quiz | **Teacher** writes an assignment and gives it. **Student** gets a toast, hands in a typed answer. **Teacher** scores it with feedback. Then show the quiz builder with questions from the question bank. | "Work goes out to the class, students hand it in, and the score reaches the student and the parent. Quizzes are built from a bank of questions that the AI can draft from a PDF, and a teacher reads every question first." |
| 1:00 | Approvals | **Parent** asks for a leave. **Teacher** approves, then the **Principal** approves. The parent sees "Approved". | "A parent asks for a leave. The homeroom teacher and then the principal decide, and the parent is told. Nothing is lost between three people." |
| 1:20 | Admissions | **Admin staff** open the applications and one applicant's documents; show what the AI read from a photo next to the document, with a mismatch between documents flagged. | "Applications come in with their documents. The AI reads a photo and flags what does not match, but a person decides." |
| 1:35 | AI with a human in the loop | **Admin staff**: "Draft with AI" on a new announcement, the draft lands in the editor (not published). **Principal**: ask the assistant "Which requests are waiting for approval?" and show the sources. | "Drafts and answers always come back to a person. The principal's assistant only reads data, and shows where every number comes from." |
| 1:50 | Close | The dashboard of the principal, then the GitHub page with the gallery | "It is tested end to end, documented in two languages, and the code is public." |

Notes for the recorder
- The AI provider of the demo is the mock, so the answers are short and fixed. Say "the AI" and do not claim a model; if you ran a real model for the video, say which one.
- Do not show the Keycloak admin console, the jobs dashboard, or any environment file.
- The GIFs in `docs/assets/flows/` show the same flows as still steps if the video is not available.
