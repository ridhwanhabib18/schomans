import { cn } from '@/shared/lib/utils'

/**
 * Shows the HTML of a news article or announcement. The API sanitizes the HTML before it is stored (an allow-list:
 * formatting and safe links only), so it is rendered as it comes. Never pass text that did not come from the API here.
 */
export function RichTextContent({ html, className }: { html: string; className?: string }) {
  return <div className={cn('rich-text', className)} dangerouslySetInnerHTML={{ __html: html }} />
}
