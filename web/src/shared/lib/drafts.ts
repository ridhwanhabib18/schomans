/** The kinds of document the AI can draft for staff (AI-09). */
export type DraftDocumentType = 'Announcement' | 'Invitation' | 'Notice'

/** A draft for an editor. Nothing of it is stored: the staff member edits it and saves or issues it. */
export interface DocumentDraft {
  title: string
  paragraphs: string[]
}

const escapes: Record<string, string> = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }

/** The paragraphs of a draft as the HTML the announcement editor holds. The text is escaped: a draft is never markup. */
export function paragraphsToHtml(paragraphs: readonly string[]): string {
  return paragraphs.map((paragraph) => `<p>${paragraph.replace(/[&<>"']/g, (char) => escapes[char]!)}</p>`).join('')
}
