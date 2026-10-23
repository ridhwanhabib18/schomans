import type { ComponentProps } from 'react'
import { cn } from '@/shared/lib/utils'

/** Placeholder block shown while data loads. */
function Skeleton({ className, ...props }: ComponentProps<'div'>) {
  return <div aria-hidden="true" className={cn('animate-pulse rounded-md bg-muted', className)} {...props} />
}

export { Skeleton }
