import type { TFunction } from 'i18next'
import { parseProblem } from '@/shared/lib/problem'

/** The text of an API error: the one for its business code when the feature has it, else the API title, else a generic one. */
export function admissionErrorText(error: unknown, t: TFunction<'admissions'>, generic: string): string {
  const problem = parseProblem(error)
  if (problem.code) {
    const text = t(`errors.${problem.code}`, { defaultValue: '' })
    if (text) {
      return text
    }
  }
  return problem.title ?? generic
}
