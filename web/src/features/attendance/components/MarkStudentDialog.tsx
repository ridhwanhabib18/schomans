import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { AttendanceStatus, SessionStudentDto } from '@/api/generated/model'
import { parseProblem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useMarkAttendance } from '../api/attendance'
import '../i18n'

const STATUSES = ['Present', 'Excused', 'Sick', 'Absent'] as const satisfies readonly NonNullable<AttendanceStatus>[]

/** The teacher sets one student's status by hand: no camera, a letter from home. */
export function MarkStudentDialog({ sessionId, student, onClose }: { sessionId: string; student: SessionStudentDto; onClose: () => void }) {
  const { t } = useTranslation('attendance')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const mark = useMarkAttendance(sessionId)
  const [status, setStatus] = useState<NonNullable<AttendanceStatus>>(student.status ?? 'Present')
  const [note, setNote] = useState(student.note ?? '')
  const [error, setError] = useState<string>()

  function save() {
    setError(undefined)
    mark.mutate(
      { studentId: student.studentId, status, note: note.trim() || null },
      {
        onSuccess: () => {
          showToast({ kind: 'success', title: t('mark.saved') })
          onClose()
        },
        onError: (e) => {
          const problem = parseProblem(e)
          setError(t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }))
        },
      },
    )
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{t('mark.title', { name: student.fullName })}</DialogTitle>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            save()
          }}
        >
          {error && <Alert tone="error" message={error} />}
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-1 text-sm font-medium">{t('mark.status')}</legend>
            <div className="grid grid-cols-2 gap-2">
              {STATUSES.map((value) => (
                <label key={value} className="flex items-center gap-2 rounded-md border border-border p-2 text-sm has-[:checked]:border-primary has-[:checked]:bg-primary/5">
                  <input type="radio" name="status" value={value} checked={status === value} onChange={() => setStatus(value)} className="accent-[var(--primary)]" />
                  {t(`status.${value}`)}
                </label>
              ))}
            </div>
          </fieldset>
          <FormField label={t('mark.note')} hint={t('mark.noteHint')}>
            <Textarea value={note} rows={2} maxLength={300} onChange={(event) => setNote(event.target.value)} />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={mark.isPending}>
              {mark.isPending ? tCommon('actions.saving') : t('mark.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
