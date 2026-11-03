// Hand-written until `npm run api:generate` is run against the API with the Phase 6 endpoints; the shapes mirror the DTOs of the API.

export interface AssistantTurn {
  role: 'user' | 'assistant'
  text: string
}

export interface AssistantFigure {
  label: string
  value: string
}

export interface AssistantSource {
  tool: string
  label: string
  /** The page of the app with the same numbers; null when the principal has no such page. */
  url: string | null
  figures: AssistantFigure[]
}

export interface AssistantAnswer {
  answer: string
  sources: AssistantSource[]
}

/** One message of the conversation on screen; the browser keeps them, the server stores none. */
export interface ChatMessage extends AssistantTurn {
  id: number
  sources?: AssistantSource[]
}
