import { Bar, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import { formatMonth } from '@/shared/lib/time'
import type { ChildAnalytics } from '../api/analyticsTypes'
import '../i18n'
import { ChartCard, DataTableForReaders } from './ChartCard'
import { AXIS_TICK, GRID_STROKE, INITIAL_SIZE, seriesColor, TOOLTIP_STYLE } from './chartTheme'

const shortMonth = (month: string) => formatMonth(month).replace(/ 20(\d\d)$/, ' $1')

/** Parent or student: the average grade (bars) and the attendance rate (line) of a child per month. */
export function ChildProgressChart({ child }: { child: ChildAnalytics }) {
  const { t } = useTranslation('dashboard')
  const hasData = child.months.some((m) => m.averageGrade !== null || m.attendanceRate !== null)
  const gradeLabel = t('analytics.child.grade')
  const attendanceLabel = t('analytics.child.attendance')
  const data = child.months.map((m) => ({ month: shortMonth(m.month), [gradeLabel]: m.averageGrade, [attendanceLabel]: m.attendanceRate }))

  return (
    <ChartCard
      title={t('analytics.child.title', { name: child.studentName })}
      description={t('analytics.child.description')}
      summary={t('analytics.child.summary', { name: child.studentName })}
      table={
        <DataTableForReaders
          caption={t('analytics.child.caption', { name: child.studentName })}
          headers={[t('analytics.child.month'), gradeLabel, attendanceLabel]}
          rows={child.months.map((m) => [formatMonth(m.month), m.averageGrade ?? '—', m.attendanceRate ?? '—'])}
        />
      }
    >
      {hasData ? (
        <ResponsiveContainer width="100%" height="100%" initialDimension={INITIAL_SIZE}>
          <ComposedChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid stroke={GRID_STROKE} vertical={false} />
            <XAxis dataKey="month" tick={AXIS_TICK} />
            <YAxis domain={[0, 100]} tick={AXIS_TICK} width={40} />
            <Tooltip contentStyle={TOOLTIP_STYLE} cursor={{ fill: 'var(--muted)' }} />
            <Legend />
            <Bar dataKey={gradeLabel} fill={seriesColor(0)} radius={[4, 4, 0, 0]} isAnimationActive={false} />
            <Line type="monotone" dataKey={attendanceLabel} stroke={seriesColor(1)} strokeWidth={2} dot={{ r: 3 }} connectNulls isAnimationActive={false} />
          </ComposedChart>
        </ResponsiveContainer>
      ) : (
        <p className="flex h-full items-center justify-center text-sm text-muted-foreground">{t('analytics.child.empty')}</p>
      )}
    </ChartCard>
  )
}
