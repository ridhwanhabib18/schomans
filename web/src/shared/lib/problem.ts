import axios from 'axios'

/** What the UI needs from an API error: the stable code, a message, and the messages per form field. */
export interface Problem {
  status?: number
  /** The stable `code` of a business error, for example `students.nisn_taken`. */
  code?: string
  title?: string
  /** Validation messages by field name, as the API sent them (any casing). */
  fieldErrors: Record<string, string[]>
}

interface ProblemBody {
  code?: unknown
  title?: unknown
  errors?: unknown
}

export function parseProblem(error: unknown): Problem {
  if (!axios.isAxiosError(error)) {
    return { fieldErrors: {} }
  }

  const body = (error.response?.data ?? {}) as ProblemBody
  const fieldErrors: Record<string, string[]> = {}
  if (body.errors && typeof body.errors === 'object') {
    for (const [field, messages] of Object.entries(body.errors as Record<string, unknown>)) {
      if (Array.isArray(messages)) {
        fieldErrors[field] = messages.filter((m): m is string => typeof m === 'string')
      }
    }
  }

  return {
    status: error.response?.status,
    code: typeof body.code === 'string' ? body.code : undefined,
    title: typeof body.title === 'string' ? body.title : undefined,
    fieldErrors,
  }
}

/** The first message for a form field. The API names fields in PascalCase (`FullName`), forms use camelCase, so the match ignores case. */
export function fieldError(problem: Problem | undefined, field: string): string | undefined {
  if (!problem) {
    return undefined
  }

  const wanted = field.toLowerCase()
  const key = Object.keys(problem.fieldErrors).find((name) => name.toLowerCase() === wanted)
  return key ? problem.fieldErrors[key]?.[0] : undefined
}

export const CONCURRENCY_CONFLICT = 'concurrency.conflict'

/** True when somebody else changed the record since it was opened (HTTP 409 with the concurrency code). */
export function isConcurrencyConflict(problem: Problem | undefined): boolean {
  return problem?.code === CONCURRENCY_CONFLICT
}
