import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useWriteDescription } from '../api/grading'
import { gradingErrorText } from '../errors'
import '../i18n'

interface DescriptionDialogProps {
  studentId: string
  studentName: string
  subjectId: string
  termId: string
  initial: string | null
  locked: boolean
  onClose: () => void
}

const MAX_LENGTH = 2000

/** The teacher writes the text that describes what a student achieved in the subject; it goes on the report card. */
export function DescriptionDialog({ studentId, studentName, subjectId, termId, initial, locked, onClose }: DescriptionDialogProps) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const write = useWriteDescription()
  const [body, setBody] = useState(initial ?? '')
  const [error, setError] = useState<string>()

  function save() {
    setError(undefined)
    write.mutate(
      { studentId, subjectId, termId, body: body.trim() },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('book.textDialog.saved') })
          onClose()
        },
        onError: (e) => setError(gradingErrorText(e, t, tCommon('errors.generic'))),
      },
    )
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
          {error && <Alert tone="error" message={error} />}
          <FormField label={t('book.textDialog.label')} hint={t('book.textDialog.help')}>
            <Textarea rows={5} maxLength={MAX_LENGTH} value={body} disabled={locked} onChange={(event) => setBody(event.target.value)} />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={locked || write.isPending || body.trim() === ''}>
              {write.isPending ? tCommon('actions.saving') : t('book.textDialog.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
