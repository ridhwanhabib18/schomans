import { Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcceptDescription, useDraftDescription, useWriteDescription } from '../api/grading'
import type { AchievementStatus } from '../api/types'
import { gradingErrorText } from '../errors'
import '../i18n'

interface DescriptionDialogProps {
  studentId: string
  studentName: string
  subjectId: string
  termId: string
  initial: string | null
  /** Draft until accepted; null while there is no text. */
  initialStatus: AchievementStatus | null
  initialIsAiDrafted: boolean
  locked: boolean
  onClose: () => void
}

const MAX_LENGTH = 2000

/**
 * The teacher writes the text that describes what a student achieved in the subject, or asks the AI for a draft and reviews it
 * (accept as it is, edit, or draft again). Only an accepted text goes on the report card.
 */
export function DescriptionDialog({ studentId, studentName, subjectId, termId, initial, initialStatus, initialIsAiDrafted, locked, onClose }: DescriptionDialogProps) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const write = useWriteDescription()
  const draft = useDraftDescription()
  const accept = useAcceptDescription()
  const [body, setBody] = useState(initial ?? '')
  // What the server holds now: a draft can be accepted as it is while the field still shows exactly that text.
  const [saved, setSaved] = useState({ body: initial ?? '', status: initialStatus, isAiDrafted: initialIsAiDrafted })
  const [error, setError] = useState<string>()

  const target = { studentId, subjectId, termId }
  const busy = write.isPending || draft.isPending || accept.isPending
  const fail = (e: unknown) => setError(gradingErrorText(e, t, tCommon('errors.generic')))
  const canAccept = saved.status === 'Draft' && body.trim() === saved.body

  function save() {
    setError(undefined)
    write.mutate(
      { ...target, body: body.trim() },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('book.textDialog.saved') })
          onClose()
        },
        onError: fail,
      },
    )
  }

  function acceptDraft() {
    setError(undefined)
    accept.mutate(target, {
      onSuccess: () => {
        showToast({ kind: 'success', title: t('book.textDialog.accepted') })
        onClose()
      },
      onError: fail,
    })
  }

  function askAi() {
    setError(undefined)
    draft.mutate(target, {
      onSuccess: (result) => {
        setBody(result.body)
        setSaved({ body: result.body, status: result.status, isAiDrafted: result.isAiDrafted })
        showToast({ kind: 'success', title: t('book.textDialog.drafted') })
      },
      onError: fail,
    })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{t('book.textDialog.title', { name: studentName })}</DialogTitle>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            save()
          }}
        >
          {locked && <Alert tone="info" message={t('book.lockedNote')} />}
          {saved.status === 'Draft' && saved.isAiDrafted && <Alert tone="info" message={t('book.textDialog.reviewNote')} />}
          {error && <Alert tone="error" message={error} />}
          <FormField label={t('book.textDialog.label')} hint={t('book.textDialog.help')}>
            <Textarea rows={6} maxLength={MAX_LENGTH} value={body} disabled={locked} onChange={(event) => setBody(event.target.value)} />
          </FormField>
          <DialogFooter className="sm:justify-between">
            <Button type="button" variant="outline" disabled={locked || busy} onClick={askAi}>
              <Sparkles aria-hidden="true" />
              {draft.isPending ? t('book.textDialog.drafting') : saved.body === '' ? t('book.textDialog.draftAi') : t('book.textDialog.redraftAi')}
            </Button>
            <div className="flex flex-wrap justify-end gap-2">
              <Button type="button" variant="outline" onClick={onClose}>
                {tCommon('actions.cancel')}
              </Button>
              {canAccept && (
                <Button type="button" variant="secondary" disabled={locked || busy} onClick={acceptDraft}>
                  {t('book.textDialog.accept')}
                </Button>
              )}
              <Button type="submit" disabled={locked || busy || body.trim() === ''}>
                {write.isPending ? tCommon('actions.saving') : t('book.textDialog.save')}
              </Button>
            </div>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
