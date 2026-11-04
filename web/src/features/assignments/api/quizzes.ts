import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import { assignmentKeys } from './assignments'
import type { QuestionDifficulty, QuestionType } from './questions'

// ---- AI-06: online quizzes built from the question bank ----

export interface QuizQuestion {
  questionId: string
  order: number
  type: QuestionType
  difficulty: QuestionDifficulty
  body: string
  options: string[]
  points: number
  /** Null while the quiz is open for a student: the key and the explanation come after the due date. */
  answerKey: string | null
  explanation: string | null
  /** For multiple choice the position of the chosen option; for short answer the text. */
  myAnswer: string | null
  pointsAwarded: number | null
}

export type QuizAttemptStatus = 'Submitted' | 'WaitingForTeacher' | 'Scored'

export interface QuizAttempt {
  submissionId: string
  studentId: string
  studentName: string
  submittedAt: string
  status: QuizAttemptStatus
  score: number | null
  shortAnswersToGrade: number
}

export interface Quiz {
  assignmentId: string
  title: string
  description: string
  dueAt: string
  isOpen: boolean
  resultsVisible: boolean
  canEdit: boolean
  totalPoints: number
  questions: QuizQuestion[]
  attempt: QuizAttempt | null
}

export interface CreateQuizRequest {
  title: string
  description: string | null
  dueAt: string
  subjectId: string
  classroomIds: string[]
  questions: { questionId: string; points: number }[]
}

export const quizKeys = {
  all: ['quizzes'] as const,
  detail: (id: string, studentId?: string) => [...quizKeys.all, id, studentId ?? 'me'] as const,
}

/** A quiz as the viewer may see it; a teacher or a parent names the student whose attempt to open. */
export function useQuiz(assignmentId: string, studentId?: string, enabled = true) {
  return useQuery({
    queryKey: quizKeys.detail(assignmentId, studentId),
    queryFn: () => customInstance<Quiz>({ url: `/api/v1/quizzes/${assignmentId}`, method: 'GET', params: { studentId } }),
    enabled,
  })
}

function useInvalidateQuizzes() {
  const queryClient = useQueryClient()
  return async () => {
    await Promise.all([queryClient.invalidateQueries({ queryKey: quizKeys.all }), queryClient.invalidateQueries({ queryKey: assignmentKeys.all })])
  }
}

export function useCreateQuiz() {
  const invalidate = useInvalidateQuizzes()
  return useMutation({
    mutationFn: (request: CreateQuizRequest) => customInstance<{ id: string }>({ url: '/api/v1/quizzes', method: 'POST', data: request }),
    onSuccess: invalidate,
  })
}

/** The student takes the quiz: once, before the due date. */
export function useSubmitQuiz(assignmentId: string) {
  const invalidate = useInvalidateQuizzes()
  return useMutation({
    mutationFn: (answers: { questionId: string; answer: string | null }[]) =>
      customInstance<Quiz>({ url: `/api/v1/quizzes/${assignmentId}/submit`, method: 'POST', data: { assignmentId, answers } }),
    onSuccess: invalidate,
  })
}

/** The teacher gives points to the short answers of one attempt. */
export function useGradeQuiz(assignmentId: string) {
  const invalidate = useInvalidateQuizzes()
  return useMutation({
    mutationFn: ({ studentId, points }: { studentId: string; points: { questionId: string; points: number }[] }) =>
      customInstance<Quiz>({ url: `/api/v1/quizzes/${assignmentId}/grade`, method: 'POST', data: { assignmentId, studentId, points } }),
    onSuccess: invalidate,
  })
}
