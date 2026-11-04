import { useMutation } from '@tanstack/react-query'
import { customInstance } from '@/shared/lib/http'

/** Who is asking: a guest of the landing page (public sources only) or a signed-in parent (also the announcements meant for them). */
export type ChatbotAudience = 'public' | 'parent'

export interface ChatbotSource {
  title: string
  /** A page of the web app. */
  url: string
}

export interface ChatbotAnswer {
  /** False when the published sources do not contain the answer: the answer then says so and `contact` tells who to ask. */
  known: boolean
  answer: string
  sources: ChatbotSource[]
  contact: string | null
}

/** Asks the school chatbot (AI-08). It answers only from published sources and links to them. */
export function useAskChatbot(audience: ChatbotAudience) {
  return useMutation({
    mutationFn: (question: string) => customInstance<ChatbotAnswer>({ url: `/api/v1/chatbot/${audience}`, method: 'POST', data: { question } }),
  })
}
