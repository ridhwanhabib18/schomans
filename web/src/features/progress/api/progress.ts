import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { listStudents } from '@/api/generated/schomans'
import { customInstance } from '@/shared/lib/http'
import type { MyClassroom, Paged, ProgressSummary, SentSummaries, TeacherNote } from './types'

// Hand-written until `npm run api:generate` is run against the API with the Phase 6 endpoints.

export const progressKeys = {
  all: ['progress'] as const,
  classrooms: () => [...progressKeys.all, 'classrooms'] as const,
  classroomSummaries: (classroomId: string, month: string) => [...progressKeys.all, 'classroom', classroomId, month] as const,
  mine: (studentId: string) => [...progressKeys.all, 'mine', studentId] as const,
  notes: (studentId: string) => [...progressKeys.all, 'notes', studentId] as const,
  students: (classroomId: string) => [...progressKeys.all, 'students', classroomId] as const,
}

// ---- requests ----

const listMyClassrooms = () => customInstance<MyClassroom[]>({ url: '/api/v1/teacher-notes/classrooms', method: 'GET' })

const listClassroomSummaries = (classroomId: string, month: string) =>
  customInstance<ProgressSummary[]>({ url: '/api/v1/progress-summaries', method: 'GET', params: { classroomId, month } })

const regenerateSummary = (studentId: string, month: string) =>
  customInstance<ProgressSummary>({ url: '/api/v1/progress-summaries/regenerate', method: 'POST', data: { studentId, month } })

const editSummary = (id: string, body: string) =>
  customInstance<ProgressSummary>({ url: `/api/v1/progress-summaries/${id}`, method: 'PUT', data: { id, body } })

const approveSummary = (id: string) => customInstance<ProgressSummary>({ url: `/api/v1/progress-summaries/${id}/approve`, method: 'POST' })

const sendSummaries = (classroomId: string, month: string) =>
  customInstance<SentSummaries>({ url: '/api/v1/progress-summaries/send', method: 'POST', data: { classroomId, month } })

const listMySummaries = (studentId: string) =>
  customInstance<Paged<ProgressSummary>>({ url: '/api/v1/progress-summaries/mine', method: 'GET', params: { studentId, page: 1, pageSize: 100 } })

const listNotes = (studentId: string) =>
  customInstance<Paged<TeacherNote>>({ url: '/api/v1/teacher-notes', method: 'GET', params: { studentId, page: 1, pageSize: 100 } })

const createNote = (studentId: string, body: string, date?: string) =>
  customInstance<TeacherNote>({ url: '/api/v1/teacher-notes', method: 'POST', data: { studentId, body, date } })

const deleteNote = (id: string) => customInstance<void>({ url: `/api/v1/teacher-notes/${id}`, method: 'DELETE' })

// ---- hooks ----

/** The classrooms the signed-in teacher works in; `isHomeroom` marks the ones whose summaries they review. */
export function useMyClassrooms() {
  return useQuery({ queryKey: progressKeys.classrooms(), queryFn: listMyClassrooms, staleTime: 5 * 60_000 })
}

/** The active students of a classroom the teacher works in, for the note and draft pickers. */
export function useClassroomStudents(classroomId: string | null) {
  return useQuery({
    queryKey: progressKeys.students(classroomId ?? ''),
    queryFn: async () => (await listStudents({ ClassroomId: classroomId!, Status: 'Active', Page: 1, PageSize: 100, Sort: 'name' })).items,
    enabled: classroomId !== null,
  })
}

export function useClassroomSummaries(classroomId: string | null, month: string) {
  return useQuery({
    queryKey: progressKeys.classroomSummaries(classroomId ?? '', month),
    queryFn: () => listClassroomSummaries(classroomId!, month),
    enabled: classroomId !== null,
  })
}

/** Every change to a summary can move the list of the classroom and the parents' history, so one invalidation covers them. */
function useInvalidateProgress() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: progressKeys.all })
}

export function useRegenerateSummary() {
  const invalidate = useInvalidateProgress()
  return useMutation({
    mutationFn: ({ studentId, month }: { studentId: string; month: string }) => regenerateSummary(studentId, month),
    onSuccess: invalidate,
  })
}

export function useEditSummary() {
  const invalidate = useInvalidateProgress()
  return useMutation({ mutationFn: ({ id, body }: { id: string; body: string }) => editSummary(id, body), onSuccess: invalidate })
}

export function useApproveSummary() {
  const invalidate = useInvalidateProgress()
  return useMutation({ mutationFn: (id: string) => approveSummary(id), onSuccess: invalidate })
}

export function useSendSummaries() {
  const invalidate = useInvalidateProgress()
  return useMutation({ mutationFn: ({ classroomId, month }: { classroomId: string; month: string }) => sendSummaries(classroomId, month), onSuccess: invalidate })
}

/** The sent summaries about one child, newest month first (the parent's history). */
export function useMySummaries(studentId: string) {
  return useQuery({ queryKey: progressKeys.mine(studentId), queryFn: () => listMySummaries(studentId) })
}

export function useTeacherNotes(studentId: string | null) {
  return useQuery({ queryKey: progressKeys.notes(studentId ?? ''), queryFn: () => listNotes(studentId!), enabled: studentId !== null })
}

export function useCreateNote() {
  const invalidate = useInvalidateProgress()
  return useMutation({ mutationFn: ({ studentId, body, date }: { studentId: string; body: string; date?: string }) => createNote(studentId, body, date), onSuccess: invalidate })
}

export function useDeleteNote() {
  const invalidate = useInvalidateProgress()
  return useMutation({ mutationFn: (id: string) => deleteNote(id), onSuccess: invalidate })
}
