import type { FormEvent, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { isConcurrencyConflict, type Problem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import '../i18n'

interface FormDialogProps {
  title: string
  description?: string
  /** The server's answer to the last attempt, if it failed. */
  problem: Problem | undefined
  /** Where the texts for this form's business error codes live, for example `students.errors`. */
  errorPrefix: string
  pending: boolean
  submitLabel: string
  onSubmit: () => void
  onClose: () => void
  /** Called by the "Reload" button of the concurrency message. */
  onReload?: () => void
  children: ReactNode
}

/**
 * A modal form. Rendered only while it is open, so its fields start fresh every time. It shows the business error of
 * the server (a duplicate NISN, a locked field) in the words of the feature, and the "somebody else changed this" message.
 */
export function FormDialog({ title, description, problem, errorPrefix, pending, submitLabel, onSubmit, onClose, onReload, children }: FormDialogProps) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const hasFieldErrors = problem !== undefined && Object.keys(problem.fieldErrors).length > 0

  function submit(event: FormEvent) {
    event.preventDefault()
    onSubmit()
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>

        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          {isConcurrencyConflict(problem) ? (
            <Alert
              tone="error"
              message={tCommon('errors.concurrency')}
              action={
                onReload && (
                  <Button type="button" variant="outline" size="sm" onClick={onReload}>
                    {tCommon('actions.reload')}
                  </Button>
                )
              }
            />
          ) : (
            problem &&
            !hasFieldErrors && (
              <Alert
                tone="error"
                message={t(`${errorPrefix}.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') })}
              />
            )
          )}

          {children}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? tCommon('actions.saving') : submitLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
