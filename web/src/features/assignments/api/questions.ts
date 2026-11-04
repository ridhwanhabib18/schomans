import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'

// ---- AI-06: the question bank of a subject and the AI's drafts from a PDF ----

export type QuestionType = 'MultipleChoice' | 'ShortAnswer'
export type QuestionDifficulty = 'Easy' | 'Medium' | 'Hard'

export const QUESTION_TYPES: readonly QuestionType[] = ['MultipleChoice', 'ShortAnswer']
export const QUESTION_DIFFICULTIES: readonly QuestionDifficulty[] = ['Easy', 'Medium', 'Hard']

/** A question as a teacher writes it or as the AI drafts it. */
export interface QuestionInput {
  type: QuestionType
  difficulty: QuestionDifficulty
  body: string
  /** Two to five for multiple choice, none for short answer. */
  options: string[]
  /** For multiple choice the position (from 0) of the right option; for short answer the model answer. */
  answerKey: string
  explanation: string
}

export interface Question extends QuestionInput {
  id: string
  subjectId: string
  createdByName: string
  isMine: boolean
}

export interface GeneratedQuestions {
  questions: QuestionInput[]
  pages: number
  truncated: boolean
}

export interface GenerateRequest {
  subjectId: string
  count: number
  difficulty: QuestionDifficulty
  type: QuestionType
  file: File
}

export interface QuestionFilters {
  subjectId: string
  type?: QuestionType
  difficulty?: QuestionDifficulty
  search?: string
}

interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

export const QUESTION_PAGE_SIZE = 20

export const questionKeys = {
  all: ['question-bank'] as const,
  list: (filters: QuestionFilters, page: number) => [...questionKeys.all, 'list', filters, page] as const,
}

/** The bank of a subject the teacher teaches. */
export function useQuestions(filters: QuestionFilters, page: number, enabled = true) {
  return useQuery({
    queryKey: questionKeys.list(filters, page),
    queryFn: () =>
      customInstance<Paged<Question>>({
        url: '/api/v1/question-bank',
        method: 'GET',
        params: { subjectId: filters.subjectId, type: filters.type, difficulty: filters.difficulty, search: filters.search || undefined, page, pageSize: QUESTION_PAGE_SIZE },
      }),
    enabled,
    placeholderData: keepPreviousData,
  })
}

export function useSaveQuestions() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ subjectId, questions }: { subjectId: string; questions: QuestionInput[] }) =>
      customInstance<Question[]>({ url: '/api/v1/question-bank', method: 'POST', data: { subjectId, questions } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: questionKeys.all }),
  })
}

export function useUpdateQuestion() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, question }: { id: string; question: QuestionInput }) =>
      customInstance<Question>({ url: `/api/v1/question-bank/${id}`, method: 'PUT', data: { id, question } }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: questionKeys.all }),
  })
}

export function useDeleteQuestion() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => customInstance<void>({ url: `/api/v1/question-bank/${id}`, method: 'DELETE' }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: questionKeys.all }),
  })
}

/** Uploads the PDF and asks the AI for drafts. Nothing is saved; the teacher edits the drafts and saves them. */
export function useGenerateQuestions() {
  return useMutation({
    mutationFn: ({ subjectId, count, difficulty, type, file }: GenerateRequest) => {
      const form = new FormData()
      form.append('subjectId', subjectId)
      form.append('count', String(count))
      form.append('difficulty', difficulty)
      form.append('type', type)
      form.append('file', file)
      return customInstance<GeneratedQuestions>({ url: '/api/v1/question-bank/generate', method: 'POST', data: form })
    },
  })
}
