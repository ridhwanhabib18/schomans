import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listTeachingOptions } from '@/api/generated/schomans'
import { customInstance } from '@/shared/lib/http'
import type {
  EnterGradeCommand,
  GeneratedReportCards,
  GradeBand,
  GradeBandInput,
  GradeBook,
  GradeBookParams,
  GradeWeightItem,
  GradeWeights,
  PublicationCommand,
  WriteDescriptionCommand,
} from './types'

export const gradingKeys = {
  all: ['grading'] as const,
  teachingOptions: () => [...gradingKeys.all, 'teaching-options'] as const,
  book: (params: GradeBookParams) => [...gradingKeys.all, 'book', params] as const,
  weights: (subjectId: string) => [...gradingKeys.all, 'weights', subjectId] as const,
  bands: () => [...gradingKeys.all, 'bands'] as const,
}

// ---- requests (the Phase 4 endpoints) ----

const getGradeBook = (params: GradeBookParams) => customInstance<GradeBook>({ url: '/api/v1/grades/book', method: 'GET', params })

const enterGrade = (command: EnterGradeCommand) => customInstance<unknown>({ url: '/api/v1/grades', method: 'PUT', data: command })

const publishGrades = (command: PublicationCommand) => customInstance<unknown>({ url: '/api/v1/grades/publications', method: 'POST', data: command })

const unpublishGrades = (params: PublicationCommand) => customInstance<void>({ url: '/api/v1/grades/publications', method: 'DELETE', params })

const writeDescription = (command: WriteDescriptionCommand) =>
  customInstance<unknown>({ url: '/api/v1/report-cards/descriptions', method: 'PUT', data: command })

const generateReportCards = (command: PublicationCommand) =>
  customInstance<GeneratedReportCards>({ url: '/api/v1/report-cards/generate', method: 'POST', data: command })

const getGradeWeights = (subjectId: string) => customInstance<GradeWeights>({ url: `/api/v1/grade-weights/${subjectId}`, method: 'GET' })

const setGradeWeights = (subjectId: string, weights: GradeWeightItem[]) =>
  customInstance<GradeWeights>({ url: `/api/v1/grade-weights/${subjectId}`, method: 'PUT', data: { subjectId, weights } })

const listGradeBands = () => customInstance<GradeBand[]>({ url: '/api/v1/grade-bands', method: 'GET' })

const replaceGradeBands = (bands: GradeBandInput[]) => customInstance<GradeBand[]>({ url: '/api/v1/grade-bands', method: 'PUT', data: { bands } })

// ---- hooks ----

/** Any change to scores, weights or publication moves several screens, so one coarse invalidation keeps them all right. */
function useInvalidateGrading() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: gradingKeys.all })
}

/** The classroom and subject pairs the signed-in teacher teaches (from the timetable). */
export function useGradingOptions() {
  return useQuery({ queryKey: gradingKeys.teachingOptions(), queryFn: () => listTeachingOptions(), staleTime: 5 * 60_000 })
}

export function useGradeBook(params: GradeBookParams | null) {
  return useQuery({
    queryKey: gradingKeys.book(params ?? { classroomId: '', subjectId: '', termId: '' }),
    queryFn: () => getGradeBook(params!),
    enabled: params !== null,
    // Scored assignments flow in while the teacher is elsewhere.
    staleTime: 0,
  })
}

export function useGradeWeights(subjectId: string | null) {
  return useQuery({ queryKey: gradingKeys.weights(subjectId ?? ''), queryFn: () => getGradeWeights(subjectId!), enabled: subjectId !== null })
}

export function useSetGradeWeights() {
  const invalidate = useInvalidateGrading()
  return useMutation({
    mutationFn: ({ subjectId, weights }: { subjectId: string; weights: GradeWeightItem[] }) => setGradeWeights(subjectId, weights),
    onSuccess: invalidate,
  })
}

export function useEnterGrade() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (command: EnterGradeCommand) => enterGrade(command), onSuccess: invalidate })
}

export function useWriteDescription() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (command: WriteDescriptionCommand) => writeDescription(command), onSuccess: invalidate })
}

export function usePublishGrades() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (command: PublicationCommand) => publishGrades(command), onSuccess: invalidate })
}

export function useUnpublishGrades() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (command: PublicationCommand) => unpublishGrades(command), onSuccess: invalidate })
}

export function useGenerateReportCards() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (command: PublicationCommand) => generateReportCards(command), onSuccess: invalidate })
}

export function useGradeBands() {
  return useQuery({ queryKey: gradingKeys.bands(), queryFn: () => listGradeBands() })
}

export function useReplaceGradeBands() {
  const invalidate = useInvalidateGrading()
  return useMutation({ mutationFn: (bands: GradeBandInput[]) => replaceGradeBands(bands), onSuccess: invalidate })
}
