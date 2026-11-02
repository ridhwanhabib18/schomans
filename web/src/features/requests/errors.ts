import type { TFunction } from 'i18next'
import { parseProblem } from '@/shared/lib/problem'

/**
 * The text of an API error: the one for its business code when the feature has it, else the first validation message,
 * else the API title, else a generic one.
 */
export function requestErrorText(error: unknown, t: TFunction<'requests'>, generic: string): string {
  const problem = parseProblem(error)
  if (problem.code) {
    const text = t(`errors.${problem.code}`, { defaultValue: '' })
    if (text) {
      return text
    }
  }

  const firstValidation = Object.values(problem.fieldErrors).flat()[0]
  return firstValidation ?? problem.title ?? generic
}
