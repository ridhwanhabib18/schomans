import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import { parseProblem } from '@/shared/lib/problem'
import { assignmentKeys } from './assignments'

// ---- AI-05: the rubric of an assignment and the AI's draft feedback on the work ----

export interface RubricCriterion {
  name: string
  description: string
  maxPoints: number
}

export interface Rubric {
  assignmentId: string
  criteria: RubricCriterion[]
  totalPoints: number
}

export type FeedbackStatus = 'Draft' | 'Accepted' | 'Rejected'

export interface CriterionScore {
  name: string
  maxPoints: number
  points: number
  reason: string
}

export interface FeedbackDraft {
  submissionId: string
  status: FeedbackStatus
  criteria: CriterionScore[]
  suggestion: string
  /** The points of all criteria on the scale of a score (0 to 100). */
  score: number
}

export interface FeedbackDraftSummary {
  submissionId: string
  status: FeedbackStatus
  score: number
}

export interface DraftedFeedback {
  drafted: number
  alreadyHadDraft: number
  notText: number
  alreadyScored: number
  failed: number
  stoppedBy: string | null
}

export interface CriterionEdit {
  points: number
  reason: string
}

export const feedbackKeys = {
  all: ['essay-feedback'] as const,
  rubric: (assignmentId: string) => [...feedbackKeys.all, 'rubric', assignmentId] as const,
  drafts: (assignmentId: string) => [...feedbackKeys.all, 'drafts', assignmentId] as const,
  draft: (submissionId: string) => [...feedbackKeys.all, 'draft', submissionId] as const,
}

const getRubric = (assignmentId: string) => customInstance<Rubric>({ url: `/api/v1/rubrics/${assignmentId}`, method: 'GET' })

const putRubric = (assignmentId: string, criteria: RubricCriterion[]) =>
  customInstance<Rubric>({ url: `/api/v1/rubrics/${assignmentId}`, method: 'PUT', data: { criteria } })

const listDrafts = (assignmentId: string) =>
  customInstance<FeedbackDraftSummary[]>({ url: `/api/v1/essay-feedback/assignments/${assignmentId}`, method: 'GET' })

const draftAll = (assignmentId: string) =>
  customInstance<DraftedFeedback>({ url: `/api/v1/essay-feedback/assignments/${assignmentId}/draft`, method: 'POST' })

/** The draft of a piece of work, or null when there is none yet (the API answers 404 then). */
const getDraft = async (submissionId: string): Promise<FeedbackDraft | null> => {
  try {
    return await customInstance<FeedbackDraft>({ url: `/api/v1/essay-feedback/submissions/${submissionId}`, method: 'GET' })
  } catch (error) {
    if (parseProblem(error).status === 404) {
      return null
    }
    throw error
  }
}

const draftOne = (submissionId: string) =>
  customInstance<FeedbackDraft>({ url: `/api/v1/essay-feedback/submissions/${submissionId}/draft`, method: 'POST' })

const accept = (submissionId: string, criteria: CriterionEdit[], suggestion: string) =>
  customInstance<FeedbackDraft>({ url: `/api/v1/essay-feedback/submissions/${submissionId}/accept`, method: 'POST', data: { criteria, suggestion } })

const reject = (submissionId: string) =>
  customInstance<FeedbackDraft>({ url: `/api/v1/essay-feedback/submissions/${submissionId}/reject`, method: 'POST' })

export function useRubric(assignmentId: string, enabled = true) {
  return useQuery({ queryKey: feedbackKeys.rubric(assignmentId), queryFn: () => getRubric(assignmentId), enabled })
}

export function useSetRubric(assignmentId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (criteria: RubricCriterion[]) => putRubric(assignmentId, criteria),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: feedbackKeys.all }),
  })
}

export function useFeedbackDrafts(assignmentId: string, enabled = true) {
  return useQuery({ queryKey: feedbackKeys.drafts(assignmentId), queryFn: () => listDrafts(assignmentId), enabled })
}

export function useFeedbackDraft(submissionId: string, enabled = true) {
  return useQuery({ queryKey: feedbackKeys.draft(submissionId), queryFn: () => getDraft(submissionId), enabled })
}

/** A change of a draft can change the score and the status of a piece of work, so both families of queries are read again. */
function useInvalidateFeedback() {
  const queryClient = useQueryClient()
  return async () => {
    await queryClient.invalidateQueries({ queryKey: feedbackKeys.all })
    await queryClient.invalidateQueries({ queryKey: assignmentKeys.all })
  }
}

export function useDraftFeedback() {
  const invalidate = useInvalidateFeedback()
  return useMutation({ mutationFn: (submissionId: string) => draftOne(submissionId), onSuccess: invalidate })
}

export function useDraftAllFeedback(assignmentId: string) {
  const invalidate = useInvalidateFeedback()
  return useMutation({ mutationFn: () => draftAll(assignmentId), onSuccess: invalidate })
}

export function useAcceptFeedback() {
  const invalidate = useInvalidateFeedback()
  return useMutation({
    mutationFn: ({ submissionId, criteria, suggestion }: { submissionId: string; criteria: CriterionEdit[]; suggestion: string }) =>
      accept(submissionId, criteria, suggestion),
    onSuccess: invalidate,
  })
}

export function useRejectFeedback() {
  const invalidate = useInvalidateFeedback()
  return useMutation({ mutationFn: (submissionId: string) => reject(submissionId), onSuccess: invalidate })
}
