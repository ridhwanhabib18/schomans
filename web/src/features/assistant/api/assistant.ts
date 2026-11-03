import { useMutation } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'
import type { AssistantAnswer, AssistantTurn } from './types'

const ask = (question: string, history: AssistantTurn[]) =>
  customInstance<AssistantAnswer>({ url: '/api/v1/assistant/ask', method: 'POST', data: { question, history } })

/** Asks the principal assistant; the earlier turns are sent along because the server keeps no conversation. */
export function useAskAssistant() {
  return useMutation({ mutationFn: ({ question, history }: { question: string; history: AssistantTurn[] }) => ask(question, history) })
}
