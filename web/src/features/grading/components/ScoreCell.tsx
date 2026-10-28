import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Input } from '@/shared/ui/form-controls'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useEnterGrade } from '../api/grading'
import type { EnterableComponent } from '../api/types'
import { gradingErrorText } from '../errors'
import '../i18n'

interface ScoreCellProps {
  studentId: string
  studentName: string
  subjectId: string
  termId: string
  component: EnterableComponent
  /** The average the grade book shows now; null while there is no score. */
  value: number | null
  locked: boolean
}

/** One score the teacher types straight into the table: it is saved when the field loses focus and the value changed. */
export function ScoreCell({ studentId, studentName, subjectId, termId, component, value, locked }: ScoreCellProps) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const enter = useEnterGrade()
  const shown = value === null ? '' : String(value)
  const [draft, setDraft] = useState<string | null>(null)
  const [invalid, setInvalid] = useState(false)
  const current = draft ?? shown

  function save() {
    if (draft === null || draft === shown) {
      setDraft(null)
      return
    }

    const score = Number(draft)
    if (draft.trim() === '' || Number.isNaN(score) || score < 0 || score > 100) {
      setInvalid(true)
      showToast({ kind: 'error', title: t('book.invalidScore') })
      return
    }

    setInvalid(false)
    enter.mutate(
      { studentId, subjectId, termId, component, score },
      {
        onSuccess: () => {
          setDraft(null)
          showToast({ kind: 'success', title: t('book.scoreSaved') })
        },
        onError: (error) => showToast({ kind: 'error', title: gradingErrorText(error, t, tCommon('errors.generic')) }),
      },
    )
  }

  return (
    <Input
      type="number"
      inputMode="decimal"
      min={0}
      max={100}
      step="0.01"
      className="h-9 w-24 tabular-nums"
      aria-label={t('book.scoreLabel', { component: t(`components.${component}`), name: studentName })}
      aria-invalid={invalid}
      disabled={locked || enter.isPending}
      value={current}
      onChange={(event) => {
        setInvalid(false)
        setDraft(event.target.value)
      }}
      onBlur={save}
      onKeyDown={(event) => {
        if (event.key === 'Enter') {
          event.currentTarget.blur()
        }
      }}
    />
  )
}
