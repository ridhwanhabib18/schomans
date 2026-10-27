import { cloneElement, isValidElement, useId, type ReactElement, type ReactNode } from 'react'
import { cn } from '@/shared/lib/utils'
import { Label } from './form-controls'

interface FormFieldProps {
  label: string
  /** The control (an Input, Select, Textarea). It gets the id, the error state and the description wired up. */
  children: ReactElement<{ id?: string; 'aria-invalid'?: boolean; 'aria-describedby'?: string }>
  error?: string
  hint?: ReactNode
  required?: boolean
  className?: string
}

/** A label, a control, a hint, and an error message, wired together for screen readers (a label on every field, PRD accessibility). */
export function FormField({ label, children, error, hint, required, className }: FormFieldProps) {
  const id = useId()
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(' ') || undefined

  return (
    <div className={cn('flex flex-col gap-1.5', className)}>
      <Label htmlFor={id}>
        {label}
        {required && <span aria-hidden="true" className="text-destructive"> *</span>}
      </Label>
      {isValidElement(children) &&
        cloneElement(children, { id, 'aria-invalid': error ? true : undefined, 'aria-describedby': describedBy })}
      {hint && (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      )}
      {error && (
        <p id={errorId} role="alert" className="text-xs text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
