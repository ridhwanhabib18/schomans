import { useTranslation } from 'react-i18next'
import '../i18n'

// The body is stored as one paragraph, a blank line, then three labelled lines (see ProgressSummary on the API side).
const POINTS = [
  { prefix: 'What is going well: ', key: 'going' },
  { prefix: 'What needs attention: ', key: 'attention' },
  { prefix: 'A suggestion for home: ', key: 'home' },
] as const

/** A summary as a family reads it: the paragraph, then the three points with headings. A text the teacher rewrote freely is shown as it is. */
export function SummaryBody({ body }: { body: string }) {
  const { t } = useTranslation('progress')
  const lines = body.split('\n')
  const points = POINTS.map((point) => ({ ...point, text: lines.find((line) => line.startsWith(point.prefix))?.slice(point.prefix.length) }))
  const structured = points.every((point) => point.text)

  if (!structured) {
    return <p className="whitespace-pre-line text-sm leading-relaxed">{body}</p>
  }

  const paragraph = lines.filter((line) => line.trim() !== '' && !POINTS.some((point) => line.startsWith(point.prefix))).join(' ')
  return (
    <div className="flex flex-col gap-3 text-sm leading-relaxed">
      <p>{paragraph}</p>
      <dl className="flex flex-col gap-2">
        {points.map((point) => (
          <div key={point.key}>
            <dt className="font-medium">{t(`mine.${point.key}`)}</dt>
            <dd className="text-muted-foreground">{point.text}</dd>
          </div>
        ))}
      </dl>
    </div>
  )
}
