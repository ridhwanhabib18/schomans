import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import type { SubjectAverage } from '../api/analyticsTypes'
import '../i18n'
import { ChartCard, DataTableForReaders } from './ChartCard'
import { AXIS_TICK, GRID_STROKE, INITIAL_SIZE, seriesColor, TOOLTIP_STYLE } from './chartTheme'

/** Principal: the average final grade of the current term per subject. */
export function SubjectAverageChart({ averages }: { averages: SubjectAverage[] }) {
  const { t } = useTranslation('dashboard')

  return (
    <ChartCard
      title={t('analytics.subjectAverages.title')}
      description={t('analytics.subjectAverages.description')}
      summary={t('analytics.subjectAverages.summary')}
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
          <BarChart data={averages} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid stroke={GRID_STROKE} vertical={false} />
            <XAxis dataKey="subjectName" tick={AXIS_TICK} interval={0} />
            <YAxis domain={[0, 100]} tick={AXIS_TICK} width={40} />
            <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'var(--muted)' }} />
            <Bar dataKey="average" name={t('analytics.subjectAverages.average')} fill={seriesColor(0)} radius={[4, 4, 0, 0]} isAnimationActive={false} />
          </BarChart>
        </ResponsiveContainer>
      ) : (
        <p className="flex h-full items-center justify-center text-sm text-muted-foreground">{t('analytics.subjectAverages.empty')}</p>
      )}
    </ChartCard>
  )
}
