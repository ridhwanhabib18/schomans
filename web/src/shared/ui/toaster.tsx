import { CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { cn } from '@/shared/lib/utils'
import { selectDismissToast, selectToasts, useToastStore, type Toast } from '@/stores/useToastStore'

const AUTO_DISMISS_MS = 7000

const icons = { info: Info, success: CheckCircle2, error: XCircle } as const

function ToastItem({ toast }: { toast: Toast }) {
  const { t } = useTranslation()
  const dismiss = useToastStore(selectDismissToast)
  const navigate = useNavigate()
  const Icon = icons[toast.kind]

  useEffect(() => {
    const timer = setTimeout(() => dismiss(toast.id), AUTO_DISMISS_MS)
    return () => clearTimeout(timer)
  }, [dismiss, toast.id])

  const content = (
    <>
      <Icon aria-hidden="true" className={cn('mt-0.5 size-5 shrink-0', toast.kind === 'error' ? 'text-destructive' : 'text-primary')} />
      <span className="flex min-w-0 flex-col text-left">
        <span className="font-medium">{toast.title}</span>
        {toast.body && <span className="line-clamp-2 text-sm text-muted-foreground">{toast.body}</span>}
      </span>
    </>
  )

  return (
    <li className="pointer-events-auto flex items-start gap-1 rounded-lg border border-border bg-card p-3 shadow-lg">
      {toast.linkUrl ? (
        <button
          type="button"
          className="flex flex-1 items-start gap-3"
          onClick={() => {
            dismiss(toast.id)
            void navigate(toast.linkUrl!)
          }}
        >
          {content}
        </button>
      ) : (
        <div className="flex flex-1 items-start gap-3">{content}</div>
      )}
      <button type="button" aria-label={t('actions.dismiss')} className="rounded p-1 text-muted-foreground hover:bg-muted" onClick={() => dismiss(toast.id)}>
        <X aria-hidden="true" className="size-4" />
      </button>
    </li>
  )
}

/** The toast stack. A polite live region: screen readers announce a new toast without interrupting. */
export function Toaster() {
  const toasts = useToastStore(selectToasts)

  return (
    <ul role="status" aria-live="polite" className="pointer-events-none fixed inset-x-0 bottom-20 z-[60] mx-auto flex w-full max-w-sm flex-col gap-2 px-4 md:bottom-4 md:left-auto md:right-4 md:mx-0">
      {toasts.map((toast) => (
        <ToastItem key={toast.id} toast={toast} />
      ))}
    </ul>
  )
}
