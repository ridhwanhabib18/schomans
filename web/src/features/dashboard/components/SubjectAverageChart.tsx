import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import type { SubjectAverage } from '../api/analyticsTypes'
import '../i18n'
import { ChartCard, DataTableForReaders } from './ChartCard'
import { AXIS_TICK, GRID_STROKE, INITIAL_SIZE, seriesColor, TOOLTIP_STYLE } from './chartTheme'

/** Subject names run along the left axis, so even long ones stay apart; a name that does not fit is cut (the tooltip has it whole). */
const SUBJECT_AXIS_WIDTH = 116
const MAX_LABEL = 17
const shorten = (name: string) => (name.length > MAX_LABEL ? `${name.slice(0, MAX_LABEL - 1)}…` : name)

/** Principal: the average final grade of the current term per subject. */
export function SubjectAverageChart({ averages }: { averages: SubjectAverage[] }) {
  const { t } = useTranslation('dashboard')

  return (
    <ChartCard
      title={t('analytics.subjectAverages.title')}
      description={t('analytics.subjectAverages.description')}
      summary={t('analytics.subjectAverages.summary')}
      height={Math.max(288, averages.length * 36 + 56)}
      table={
        <DataTableForReaders
          caption={t('analytics.subjectAverages.caption')}
          headers={[t('analytics.subjectAverages.subject'), t('analytics.subjectAverages.average'), t('analytics.subjectAverages.students')]}
          rows={averages.map((s) => [s.subjectName, s.average, s.students])}
        />
      }
    >
      {averages.length > 0 ? (
        <ResponsiveContainer width="100%" height="100%" initialDimension={INITIAL_SIZE}>
          <BarChart data={averages} layout="vertical" margin={{ top: 8, right: 24, bottom: 0, left: 0 }}>
            <CartesianGrid stroke={GRID_STROKE} horizontal={false} />
            <XAxis type="number" domain={[0, 100]} tick={AXIS_TICK} />
            <YAxis type="category" dataKey="subjectName" tick={AXIS_TICK} interval={0} width={SUBJECT_AXIS_WIDTH} tickFormatter={shorten} />
            <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'var(--muted)' }} />
            <Bar dataKey="average" name={t('analytics.subjectAverages.average')} fill={seriesColor(0)} radius={[0, 4, 4, 0]} isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      ) : (
        <p className="flex h-full items-center justify-center text-sm text-muted-foreground">{t('analytics.subjectAverages.empty')}</p>
      )}
    </ChartCard>
  )
}
