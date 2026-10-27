import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { AcademicYearDto, TermInput } from '@/api/generated/model'
import { fieldError, parseProblem, type Problem } from '@/shared/lib/problem'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateAcademicYear, useUpdateAcademicYear } from '../api/masterdata'
import { FormDialog } from './FormDialog'
import '../i18n'

const MAX_TERMS = 4

const emptyTerm = (): TermInput => ({ id: null, name: '', startDate: '', endDate: '' })

export function AcademicYearFormDialog({ year, onClose }: { year?: AcademicYearDto; onClose: () => void }) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateAcademicYear()
  const update = useUpdateAcademicYear()
  const [name, setName] = useState(year?.name ?? '')
  const [startDate, setStartDate] = useState(year?.startDate ?? '')
  const [endDate, setEndDate] = useState(year?.endDate ?? '')
  const [terms, setTerms] = useState<TermInput[]>(
    year ? year.terms.map((term) => ({ id: term.id, name: term.name, startDate: term.startDate, endDate: term.endDate })) : [emptyTerm()],
  )
  const [problem, setProblem] = useState<Problem>()

  function changeTerm(index: number, change: Partial<TermInput>) {
    setTerms((current) => current.map((term, i) => (i === index ? { ...term, ...change } : term)))
  }

  function submit() {
    setProblem(undefined)
    const done = () => {
      showToast({ kind: 'success', title: t('common.saved') })
      onClose()
    }

    if (year) {
      update.mutate(
        { id: year.id, command: { id: year.id, name, startDate, endDate, terms, rowVersion: year.rowVersion } },
        { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) },
      )
    } else {
      create.mutate({ name, startDate, endDate, terms }, { onSuccess: done, onError: (e) => setProblem(parseProblem(e)) })
    }
  }

  return (
    <FormDialog
      title={year ? t('years.form.editTitle') : t('years.form.newTitle')}
      description={year ? undefined : t('years.form.newYearNote')}
      problem={problem}
      errorPrefix="years.errors"
      pending={create.isPending || update.isPending}
      submitLabel={tCommon('actions.save')}
      onSubmit={submit}
      onClose={onClose}
      onReload={onClose}
    >
      <FormField label={t('years.form.name')} required hint={t('years.form.nameHint')} error={fieldError(problem, 'name')}>
        <Input value={name} maxLength={20} onChange={(event) => setName(event.target.value)} />
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label={t('years.form.start')} required error={fieldError(problem, 'startDate')}>
          <Input type="date" value={startDate} onChange={(event) => setStartDate(event.target.value)} />
        </FormField>
        <FormField label={t('years.form.end')} required error={fieldError(problem, 'endDate')}>
          <Input type="date" value={endDate} onChange={(event) => setEndDate(event.target.value)} />
        </FormField>
      </div>

      <fieldset className="flex flex-col gap-3">
        <legend className="text-sm font-medium">{t('years.form.terms')}</legend>
        <p className="text-sm text-muted-foreground">{t('years.form.termsHint')}</p>

        {terms.map((term, index) => (
          <div key={term.id ?? `new-${index}`} className="grid gap-3 rounded-md border p-3 sm:grid-cols-[1fr_auto]">
            <div className="grid gap-3 sm:grid-cols-3">
              <FormField label={t('years.form.termName')} required>
                <Input value={term.name} maxLength={50} onChange={(event) => changeTerm(index, { name: event.target.value })} />
              </FormField>
              <FormField label={t('years.form.termStart')} required>
                <Input type="date" value={term.startDate} onChange={(event) => changeTerm(index, { startDate: event.target.value })} />
              </FormField>
              <FormField label={t('years.form.termEnd')} required>
                <Input type="date" value={term.endDate} onChange={(event) => changeTerm(index, { endDate: event.target.value })} />
              </FormField>
            </div>
            {/* A saved term may be referenced by grades, so only terms that are not saved yet can be removed. */}
            {term.id === null && terms.length > 1 && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                className="self-end"
                onClick={() => setTerms((current) => current.filter((_, i) => i !== index))}
              >
                <Trash2 aria-hidden="true" />
                <span className="sr-only">{t('years.form.removeTerm')}</span>
              </Button>
            )}
          </div>
        ))}

        {fieldError(problem, 'terms') && (
          <p role="alert" className="text-sm text-destructive">
            {fieldError(problem, 'terms')}
          </p>
        )}

        {terms.length < MAX_TERMS && (
          <Button type="button" variant="outline" size="sm" className="self-start" onClick={() => setTerms((current) => [...current, emptyTerm()])}>
            <Plus aria-hidden="true" />
            {t('years.form.addTerm')}
          </Button>
        )}
      </fieldset>
    </FormDialog>
  )
}
