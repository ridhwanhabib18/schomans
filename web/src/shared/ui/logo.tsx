import { cn } from '@/shared/lib/utils'

/** The SchoMans mark (an open book with a sparkle). Decorative: the name next to it carries the meaning. */
export function LogoMark({ className }: { className?: string }) {
  return <img src="/logo-mark.svg" alt="" aria-hidden="true" width={28} height={28} className={cn('size-7 shrink-0', className)} />
}
