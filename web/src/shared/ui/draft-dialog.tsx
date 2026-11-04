import { Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDraftDocument } from '@/shared/hooks/useDraftDocument'
import type { DocumentDraft, DraftDocumentType } from '@/shared/lib/drafts'
import { parseProblem } from '@/shared/lib/problem'
import { Alert } from './alert'
import { Button } from './button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from './dialog'
import { Input, Textarea } from './form-controls'
import { FormField } from './form-field'

export const DRAFT_AUDIENCE_MAX = 100
export const DRAFT_NOTES_MAX = 1500

interface DraftDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  type: DraftDocumentType
  /** True when the draft will replace text the person already wrote: the dialog says so before they ask. */
  replaces?: boolean
  /** Called with the draft. The caller puts it in its editor; nothing is saved here. */
  onDraft: (draft: DocumentDraft) => void
}

/** AI-09: the person gives the audience and short notes, the AI drafts the title and the text. Nothing is published or sent. */
export function DraftDialog({ open, onOpenChange, type, replaces, onDraft }: DraftDialogProps) {
  const { t } = useTranslation()
  const draft = useDraftDocument()
  const [audience, setAudience] = useState('')
  const [notes, setNotes] = useState('')
  const [error, setError] = useState<string>()

  const ready = audience.trim() !== '' && notes.trim() !== ''

  function ask() {
    setError(undefined)
    draft.mutate(
      { type, audience: audience.trim(), notes: notes.trim() },
      {
        onSuccess: (result) => {
          onDraft(result)
          onOpenChange(false)
        },
        onError: (e) => {
          const problem = parseProblem(e)
          const text = problem.code ? t(`draft.errors.${problem.code}`, { defaultValue: '' }) : ''
          setError(text || problem.title || t('errors.generic'))
        },
      },
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{t(`draft.title.${type}`)}</DialogTitle>
          <DialogDescription>{t('draft.intro')}</DialogDescription>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            // The dialog is a portal, but React still bubbles its events to the form that renders it (an editor): that form must not submit too.
            event.stopPropagation()
            if (ready && !draft.isPending) {
              ask()
            }
          }}
        >
          {error && <Alert tone="error" message={error} />}
          {replaces && <Alert tone="info" message={t('draft.replaces')} />}
          <FormField label={t('draft.audience')} required hint={t('draft.audienceHint')}>
            <Input value={audience} maxLength={DRAFT_AUDIENCE_MAX} onChange={(event) => setAudience(event.target.value)} />
          </FormField>
          <FormField label={t('draft.notes')} required hint={t('draft.notesHint')}>
            <Textarea rows={5} value={notes} maxLength={DRAFT_NOTES_MAX} onChange={(event) => setNotes(event.target.value)} />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              {t('actions.cancel')}
            </Button>
            <Button type="submit" disabled={!ready || draft.isPending}>
              <Sparkles aria-hidden="true" />
              {draft.isPending ? t('draft.writing') : t('draft.write')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
