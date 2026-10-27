import { AlertCircle, Info } from 'lucide-react'
import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/utils'

interface AlertProps {
  tone: 'error' | 'info'
  message: string
  /** A button next to the message, for example "Reload". */
  action?: ReactNode
}

/** A message above a form or a page. Errors are announced at once (role alert); information is not. */
export function Alert({ tone, message, action }: AlertProps) {
  const Icon = tone === 'error' ? AlertCircle : Info

  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className={cn(
        'flex flex-wrap items-center gap-3 rounded-lg border p-4 text-sm',
        tone === 'error' ? 'border-destructive/40 bg-destructive/5' : 'border-primary/30 bg-primary/5',
      )}
    >
      <Icon aria-hidden="true" className={cn('size-5 shrink-0', tone === 'error' ? 'text-destructive' : 'text-primary')} />
      <p className="flex-1">{message}</p>
      {action}
    </div>
  )
}
