import * as AlertDialog from '@radix-ui/react-alert-dialog'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/shared/lib/utils'
import { buttonVariants } from './button'

interface ConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: ReactNode
  confirmLabel: string
  /** Style the confirm button as destructive (deleting something). */
  destructive?: boolean
  pending?: boolean
  onConfirm: () => void
}

/** Asks before something that cannot be undone easily. An alert dialog: Escape and the cancel button back out, focus starts on cancel. */
export function ConfirmDialog({ open, onOpenChange, title, description, confirmLabel, destructive, pending, onConfirm }: ConfirmDialogProps) {
  const { t } = useTranslation()

  return (
    <AlertDialog.Root open={open} onOpenChange={onOpenChange}>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
        <AlertDialog.Content className="fixed left-1/2 top-1/2 z-50 flex w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-xl border border-border bg-card p-6 text-card-foreground shadow-lg">
          <AlertDialog.Title className="text-lg font-semibold">{title}</AlertDialog.Title>
          <AlertDialog.Description className="text-sm text-muted-foreground">{description}</AlertDialog.Description>
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <AlertDialog.Cancel className={buttonVariants({ variant: 'outline' })}>{t('actions.cancel')}</AlertDialog.Cancel>
            <AlertDialog.Action
              disabled={pending}
              className={cn(buttonVariants(), destructive && 'bg-destructive text-destructive-foreground')}
              onClick={(event) => {
                // Keep the dialog open until the request finishes, so errors stay visible next to the question.
                event.preventDefault()
                onConfirm()
              }}
            >
              {confirmLabel}
            </AlertDialog.Action>
          </div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  )
}
