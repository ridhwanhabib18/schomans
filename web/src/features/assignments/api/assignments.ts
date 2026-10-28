import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAssignment,
  createAssignmentAttachmentUploadUrl,
  createSubmissionUploadUrl,
  deleteAssignment,
  getAssignment,
  getAssignmentAttachmentUrl,
  getSubmission,
  getSubmissionFileUrl,
  gradeSubmission,
  listAssignments,
  listChildAssignments,
  listMyAssignments,
  listSubmissions,
  listTeachingOptions,
  submitAssignment,
  updateAssignment,
} from '@/api/generated/schomans'
import type {
  CreateAssignmentCommand,
  GradeSubmissionCommand,
  ListAssignmentsParams,
  NewAttachment,
  SubmitAssignmentCommand,
  UpdateAssignmentCommand,
} from '@/api/generated/model'
import { openInNewTab } from '@/shared/lib/download'
import { putToPresignedUrl } from '@/shared/lib/upload'

export const assignmentKeys = {
  all: ['assignments'] as const,
  teacherList: (params: ListAssignmentsParams) => [...assignmentKeys.all, 'list', params] as const,
  teachingOptions: () => [...assignmentKeys.all, 'teaching-options'] as const,
  detail: (id: string, studentId?: string) => [...assignmentKeys.all, 'detail', id, studentId ?? null] as const,
  mine: (page: number) => [...assignmentKeys.all, 'mine', page] as const,
  child: (studentId: string, page: number) => [...assignmentKeys.all, 'child', studentId, page] as const,
  submissions: (id: string) => [...assignmentKeys.all, 'submissions', id] as const,
  submission: (id: string) => [...assignmentKeys.all, 'submission', id] as const,
}

/** Any change can move a list, a count or a status somewhere else, so a coarse invalidation keeps every screen right. */
function useInvalidateAssignments() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: assignmentKeys.all })
}

// ---- teachers ----

export function useTeacherAssignments(params: ListAssignmentsParams) {
  return useQuery({ queryKey: assignmentKeys.teacherList(params), queryFn: () => listAssignments(params), placeholderData: keepPreviousData })
}

export function useTeachingOptions(enabled = true) {
  return useQuery({ queryKey: assignmentKeys.teachingOptions(), queryFn: () => listTeachingOptions(), enabled })
}

export function useCreateAssignment() {
  const invalidate = useInvalidateAssignments()
  return useMutation({ mutationFn: (command: CreateAssignmentCommand) => createAssignment(command), onSuccess: invalidate })
}

export function useUpdateAssignment() {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateAssignmentCommand }) => updateAssignment(id, command),
    onSuccess: invalidate,
  })
}

export function useDeleteAssignment() {
  const invalidate = useInvalidateAssignments()
  return useMutation({ mutationFn: (id: string) => deleteAssignment(id), onSuccess: invalidate })
}

/** Uploads one attachment straight to storage; resolves with what to save on the assignment. */
export function useUploadAttachment() {
  return useMutation({
    mutationFn: async (file: File): Promise<NewAttachment> => {
      const issued = await createAssignmentAttachmentUploadUrl({ contentType: file.type, sizeBytes: file.size })
      await putToPresignedUrl(issued.url, file, issued.contentType)
      return { key: issued.key, fileName: file.name }
    },
  })
}

export function useSubmissions(assignmentId: string, enabled = true) {
  return useQuery({ queryKey: assignmentKeys.submissions(assignmentId), queryFn: () => listSubmissions(assignmentId), enabled })
}

export function useSubmission(submissionId: string | null) {
  return useQuery({
    queryKey: assignmentKeys.submission(submissionId ?? ''),
    queryFn: () => getSubmission(submissionId!),
    enabled: submissionId !== null,
  })
}

export function useGradeSubmission() {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: (command: GradeSubmissionCommand) => gradeSubmission(command.submissionId, command),
    onSuccess: invalidate,
  })
}

// ---- everybody who may see an assignment ----

export function useAssignment(id: string, studentId?: string, enabled = true) {
  return useQuery({
    queryKey: assignmentKeys.detail(id, studentId),
    queryFn: () => getAssignment(id, studentId ? { studentId } : undefined),
    enabled: enabled && id !== '',
  })
}

export function useOpenAttachment(assignmentId: string, studentId?: string) {
  return useMutation({
    mutationFn: async (attachmentId: string) =>
      openInNewTab((await getAssignmentAttachmentUrl(assignmentId, attachmentId, studentId ? { studentId } : undefined)).url),
  })
}

export function useOpenSubmissionFile() {
  return useMutation({ mutationFn: async (submissionId: string) => openInNewTab((await getSubmissionFileUrl(submissionId)).url) })
}

// ---- students and parents ----

export function useMyAssignments(page: number) {
  return useQuery({ queryKey: assignmentKeys.mine(page), queryFn: () => listMyAssignments({ Page: page, PageSize: 20 }), placeholderData: keepPreviousData })
}

export function useChildAssignments(studentId: string, page: number) {
  return useQuery({
    queryKey: assignmentKeys.child(studentId, page),
    queryFn: () => listChildAssignments(studentId, { Page: page, PageSize: 20 }),
    placeholderData: keepPreviousData,
  })
}

export function useUploadSubmissionFile(assignmentId: string) {
  return useMutation({
    mutationFn: async (file: File) => {
      const issued = await createSubmissionUploadUrl(assignmentId, { assignmentId, contentType: file.type, sizeBytes: file.size })
      await putToPresignedUrl(issued.url, file, issued.contentType)
      return { key: issued.key, fileName: file.name }
    },
  })
}

export function useSubmitAssignment() {
  const invalidate = useInvalidateAssignments()
  return useMutation({
    mutationFn: (command: SubmitAssignmentCommand) => submitAssignment(command.assignmentId, command),
    onSuccess: invalidate,
  })
}
