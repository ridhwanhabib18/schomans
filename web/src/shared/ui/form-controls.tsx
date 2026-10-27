import type { ComponentProps } from 'react'
import { cn } from '@/shared/lib/utils'

const controlClass =
  'w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground disabled:cursor-not-allowed disabled:opacity-60 aria-[invalid=true]:border-destructive'

function Input({ className, ...props }: ComponentProps<'input'>) {
  return <input className={cn(controlClass, 'h-10', className)} {...props} />
}

function Textarea({ className, ...props }: ComponentProps<'textarea'>) {
  return <textarea className={cn(controlClass, 'min-h-24', className)} {...props} />
}

/** A native select: accessible and keyboard friendly out of the box, and styled like the other controls. */
function Select({ className, children, ...props }: ComponentProps<'select'>) {
  return (
    <select className={cn(controlClass, 'h-10', className)} {...props}>
      {children}
    </select>
  )
}

function Checkbox({ className, ...props }: Omit<ComponentProps<'input'>, 'type'>) {
  return <input type="checkbox" className={cn('size-4 rounded border-border accent-[var(--primary)]', className)} {...props} />
}

function Label({ className, ...props }: ComponentProps<'label'>) {
  return <label className={cn('text-sm font-medium', className)} {...props} />
}

export { Checkbox, Input, Label, Select, Textarea }
