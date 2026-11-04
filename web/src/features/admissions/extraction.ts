import type { ApplicantForm, ApplicantExtractions, DocumentType } from './api/admissions'

/** The AI reads photos only; a PDF has to be uploaded again as a photo. */
export function canReadWithAi(fileName: string): boolean {
  return /\.(jpe?g|png|webp)$/i.test(fileName)
}

/** The form fields a document can fill in. The rest of what the AI reads (NIK, parents, grades) is for staff to compare. */
export const SUGGESTED_FIELDS = ['fullName', 'dateOfBirth', 'placeOfBirth', 'address', 'guardianName'] as const
export type SuggestedField = (typeof SUGGESTED_FIELDS)[number]

export interface Suggestion {
  field: SuggestedField
  value: string
  source: DocumentType
}

const ORDER: DocumentType[] = ['BirthCertificate', 'FamilyCard', 'ReportCard']

const same = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase()

/** What the documents say that the form does not say yet: one suggestion per field, from the first document that has it. */
export function suggestionsFor(form: ApplicantForm, extractions: ApplicantExtractions): Suggestion[] {
  const documents = [...extractions.documents].sort((a, b) => ORDER.indexOf(a.type) - ORDER.indexOf(b.type))
  const result: Suggestion[] = []

  for (const field of SUGGESTED_FIELDS) {
    for (const document of documents) {
      const value = document.fields[field]
      if (!value) {
        continue
      }

      const current = form[field] ?? ''
      if (!same(value, current)) {
        result.push({ field, value, source: document.type })
      }
      break
    }
  }

  return result
}
