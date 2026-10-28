import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { currentMonth } from '@/shared/lib/time'
import { ChildPicker } from '@/shared/ui/child-picker'
import { MonthPicker } from '@/shared/ui/month-picker'
import { PageHeader } from '@/shared/ui/page-parts'
import { useMyAttendance, useStudentAttendance } from '../api/attendance'
import { StudentAttendanceView } from '../components/StudentAttendanceView'
import '../i18n'

/** A student's own attendance, month by month. */
export function MyAttendancePage() {
  const { t } = useTranslation('attendance')
  const [month, setMonth] = useState(currentMonth)
  const { data, isPending, isError, refetch } = useMyAttendance(month)

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('mine.title')} description={t('mine.description')} actions={<MonthPicker month={month} onChange={setMonth} />} />
      <StudentAttendanceView data={data} isPending={isPending} isError={isError} onRetry={() => void refetch()} />
    </div>
  )
}

function ChildMonth({ childId, month }: { childId: string; month: string }) {
  const { data, isPending, isError, refetch } = useStudentAttendance(childId, month)
  return <StudentAttendanceView data={data} isPending={isPending} isError={isError} onRetry={() => void refetch()} />
}

/** A parent follows the attendance of a child (PRD US-03). */
export function ChildAttendancePage() {
  const { t } = useTranslation('attendance')
  const [month, setMonth] = useState(currentMonth)

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('child.title')} description={t('child.description')} actions={<MonthPicker month={month} onChange={setMonth} />} />
      <ChildPicker>{(childId) => <ChildMonth childId={childId} month={month} />}</ChildPicker>
    </div>
  )
}
