import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useEditSummary } from '../api/progress'
import { SUMMARY_MAX_LENGTH, type ProgressSummary } from '../api/types'
import { progressErrorText } from '../errors'
import '../i18n'

/** The homeroom teacher rewrites a summary. Saving makes it a draft again, so it has to be approved once more. */
export function EditSummaryDialog({ summary, onClose }: { summary: ProgressSummary; onClose: () => void }) {
  const { t } = useTranslation('progress')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const edit = useEditSummary()
  const [body, setBody] = useState(summary.body)
  const [error, setError] = useState<string>()

  function save() {
    setError(undefined)
    edit.mutate(
      { id: summary.id, body: body.trim() },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('edit.saved') })
          onClose()
        },
        onError: (e) => setError(progressErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('edit.title', { name: summary.studentName })}</DialogTitle>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            save()
          }}
        >
          {error && <Alert tone="error" message={error} />}
          <FormField label={t('edit.label')} hint={t('edit.help')}>
            <Textarea rows={10} maxLength={SUMMARY_MAX_LENGTH} value={body} onChange={(event) => setBody(event.target.value)} />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={edit.isPending || body.trim() === ''}>
              {edit.isPending ? tCommon('actions.saving') : t('edit.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
