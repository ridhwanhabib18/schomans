import { useMutation, useQuery } from '@tanstack/react-query'
import { getMyAttendance } from '@/api/generated/schomans'
import { openInNewTab } from '@/shared/lib/download'
import { customInstance } from '@/shared/lib/http'
import { parseProblem } from '@/shared/lib/problem'
import { currentMonth } from '@/shared/lib/time'

// Hand-written until `npm run api:generate` is run against the API with the Phase 4 endpoints.

export type GradeComponent = 'Assignment' | 'Quiz' | 'Midterm' | 'Final'

export interface ComponentScore {
  component: GradeComponent
  average: number | null
  count: number
}

export interface SubjectGrade {
  subjectId: string
  subjectName: string
  minimumPassingScore: number
  components: ComponentScore[]
  finalGrade: number | null
  band: string | null
  isPassing: boolean | null
  description: string | null
}

export interface StudentGrades {
  studentId: string
  termId: string
  publishedAt: string
  hasReportCard: boolean
  subjects: SubjectGrade[]
}

interface ReportCardUrl {
  url: string
  expiresAt: string
}

export const reportCardKeys = {
  all: ['report-cards'] as const,
  ownStudent: () => [...reportCardKeys.all, 'own-student'] as const,
  grades: (studentId: string, termId: string) => [...reportCardKeys.all, 'grades', studentId, termId] as const,
}

const getStudentGrades = (studentId: string, termId: string) =>
  customInstance<StudentGrades>({ url: '/api/v1/report-cards/grades', method: 'GET', params: { studentId, termId } })

const getReportCardUrl = (studentId: string, termId: string) =>
  customInstance<ReportCardUrl>({ url: '/api/v1/report-cards/url', method: 'GET', params: { studentId, termId } })

/**
 * The student record id of the signed-in student. The API has no endpoint for it, but the attendance of the student
 * names it, so that answer is used.
 */
export function useOwnStudentId(enabled = true) {
  return useQuery({
    queryKey: reportCardKeys.ownStudent(),
    queryFn: async () => (await getMyAttendance({ Month: currentMonth() })).studentId,
    enabled,
    staleTime: 30 * 60_000,
  })
}

/** The published grades of a term. A 404 with `grades.not_published` is an answer, not a failure: see {@link isNotPublished}. */
export function useStudentGrades(studentId: string, termId: string) {
  return useQuery({
    queryKey: reportCardKeys.grades(studentId, termId),
    queryFn: () => getStudentGrades(studentId, termId),
    enabled: studentId !== '' && termId !== '',
    retry: false,
  })
}

export function isNotPublished(error: unknown): boolean {
  return parseProblem(error).code === 'grades.not_published'
}

/** Opens the PDF report card through a short-lived address. */
export function useOpenReportCard() {
  return useMutation({
    mutationFn: async ({ studentId, termId }: { studentId: string; termId: string }) => {
      const { url } = await getReportCardUrl(studentId, termId)
      openInNewTab(url)
    },
  })
}
