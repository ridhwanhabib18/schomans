import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { useTranslation } from 'react-i18next'
import { formatMonth } from '@/shared/lib/time'
import type { ClassroomAttendanceTrend } from '../api/analyticsTypes'
import '../i18n'
import { ChartCard, DataTableForReaders } from './ChartCard'
import { AXIS_TICK, GRID_STROKE, INITIAL_SIZE, seriesColor, TOOLTIP_STYLE } from './chartTheme'

const shortMonth = (month: string) => formatMonth(month).replace(/ 20(\d\d)$/, ' $1')

/** Principal: the monthly attendance rate of every class as one line each. */
export function AttendanceTrendChart({ trend }: { trend: ClassroomAttendanceTrend[] }) {
  const { t } = useTranslation('dashboard')
  const months = trend[0]?.months.map((m) => m.month) ?? []
  const hasData = trend.some((classroom) => classroom.months.some((m) => m.rate !== null))

  const data = months.map((month, index) => ({
    month: shortMonth(month),
    ...Object.fromEntries(trend.map((classroom) => [classroom.classroomName, classroom.months[index]?.rate ?? null])),
  }))

  return (
    <ChartCard
      title={t('analytics.attendanceTrend.title')}
      description={t('analytics.attendanceTrend.description')}
      summary={t('analytics.attendanceTrend.summary')}
      table={
        <DataTableForReaders
          caption={t('analytics.attendanceTrend.caption')}
          headers={[t('analytics.attendanceTrend.month'), ...trend.map((classroom) => classroom.classroomName)]}
          rows={months.map((month, index) => [formatMonth(month), ...trend.map((classroom) => classroom.months[index]?.rate ?? '—')])}
        />
      }
    >
      {hasData ? (
        <ResponsiveContainer width="100%" height="100%" initialDimension={INITIAL_SIZE}>
          <LineChart data={data} margin={{ top: 8, right: 16, bottom: 0, left: 0 }}>
            <CartesianGrid stroke={GRID_STROKE} vertical={false} />
            <XAxis dataKey="month" tick={AXIS_TICK} />
            <YAxis domain={[0, 100]} unit="%" tick={AXIS_TICK} width={48} />
            <Tooltip contentStyle={TOOLTIP_STYLE} formatter={(value) => (value === null ? '—' : `${value}%`)} />
            <Legend />
            {trend.map((classroom, index) => (
              <Line
                key={classroom.classroomId}
                type="monotone"
                dataKey={classroom.classroomName}
                stroke={seriesColor(index)}
                strokeWidth={2}
                dot={{ r: 3 }}
                connectNulls
                isAnimationActive={false}
              />
            ))}
          </LineChart>
        </ResponsiveContainer>
      ) : (
        <p className="flex h-full items-center justify-center text-sm text-muted-foreground">{t('analytics.attendanceTrend.empty')}</p>
      )}
    </ChartCard>
  )
}
