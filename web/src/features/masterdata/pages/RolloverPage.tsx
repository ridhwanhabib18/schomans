import { ArrowLeft, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import type { RolloverResult, TermInput } from '@/api/generated/model'
import { formatDate } from '@/shared/lib/format'
import { parseProblem, type Problem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button, buttonVariants } from '@/shared/ui/button'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useAcademicYears, useActiveClassrooms, useRolloverAcademicYear, useTeacherOptions } from '../api/masterdata'
import {
  emptyYearDraft,
  initialMappings,
  summarize,
  toCommand,
  validateMappings,
  validateYear,
  type MappingDraft,
  type WizardProblem,
  type YearDraft,
} from '../rollover'
import '../i18n'

const STEPS = ['year', 'classrooms', 'review'] as const
const MAX_TERMS = 4

/**
 * A three step wizard: the new year, what happens to each classroom, and a review. Nothing is sent before the last step,
 * and the server applies the whole rollover in one transaction.
 */
export function RolloverPage() {
  const { t } = useTranslation('masterdata')
  const years = useAcademicYears()
  const classrooms = useActiveClassrooms()
  const teachers = useTeacherOptions()
  const rollover = useRolloverAcademicYear()
  const [step, setStep] = useState(0)
  const [year, setYear] = useState<YearDraft>(emptyYearDraft)
  const [edited, setEdited] = useState<MappingDraft[]>()
  const [attempted, setAttempted] = useState(false)
  const [problem, setProblem] = useState<Problem>()
  const [result, setResult] = useState<RolloverResult>()

  const currentYear = years.data?.items.find((item) => item.isActive)
  const sourceClassrooms = useMemo(() => classrooms.data ?? [], [classrooms.data])
  // The mapping starts from the classrooms as soon as they are loaded; the admin's edits replace it.
  const mappings = edited ?? initialMappings(sourceClassrooms)

  const yearProblems = validateYear(year)
  const mappingProblems = validateMappings(mappings, sourceClassrooms)
  const summary = summarize(mappings, sourceClassrooms)
  const stepProblems: WizardProblem[] = step === 0 ? yearProblems : step === 1 ? mappingProblems : []

  if (years.isPending || classrooms.isPending) {
    return <Skeleton className="h-64 w-full" />
  }
  if (years.isError || classrooms.isError) {
    return <ErrorNotice message={t('common.error')} onRetry={() => void (years.isError ? years.refetch() : classrooms.refetch())} />
  }
  if (!currentYear) {
    return (
      <div className="flex flex-col gap-6">
        <PageHeader title={t('rollover.title')} />
        <Alert tone="error" message={t('rollover.map.noCurrent')} />
        <BackLink />
      </div>
    )
  }

  if (result) {
    return (
      <div className="flex flex-col gap-6">
        <PageHeader title={t('rollover.done.title')} description={t('rollover.done.summary', { moved: result.movedStudents, graduated: result.graduatedStudents, created: result.createdClassrooms })} />
        <div className="flex flex-wrap gap-2">
          <Link to="/manage/academic-years" className={buttonVariants()}>
            {t('rollover.done.toYears')}
          </Link>
          <Link to="/manage/classrooms" className={buttonVariants({ variant: 'outline' })}>
            {t('rollover.done.toClassrooms')}
          </Link>
        </div>
      </div>
    )
  }

  function changeMapping(index: number, change: Partial<MappingDraft>) {
    setEdited(mappings.map((mapping, i) => (i === index ? { ...mapping, ...change } : mapping)))
  }

  function changeTerm(index: number, change: Partial<TermInput>) {
    setYear((current) => ({ ...current, terms: current.terms.map((term, i) => (i === index ? { ...term, ...change } : term)) }))
  }

  function next() {
    setAttempted(true)
    if (stepProblems.length > 0) return
    setAttempted(false)
    setStep((current) => current + 1)
  }

  function confirm() {
    setProblem(undefined)
    rollover.mutate(toCommand(year, mappings), { onSuccess: setResult, onError: (error) => setProblem(parseProblem(error)) })
  }

  return (
    <div className="flex flex-col gap-6">
      <BackLink />
      <PageHeader title={t('rollover.title')} description={t('rollover.description')} />

      <nav aria-label={t('rollover.title')}>
        <ol className="flex flex-wrap gap-2 text-sm">
          {STEPS.map((name, index) => (
            <li
              key={name}
              aria-current={index === step ? 'step' : undefined}
              className={index === step ? 'rounded-full bg-primary px-3 py-1 font-medium text-primary-foreground' : 'rounded-full bg-muted px-3 py-1 text-muted-foreground'}
            >
              {index + 1}. {t(`rollover.steps.${name}`)}
            </li>
          ))}
        </ol>
        <p className="sr-only" aria-live="polite">
          {t('rollover.stepLabel', { current: step + 1, total: STEPS.length, name: t(`rollover.steps.${STEPS[step]}`) })}
        </p>
      </nav>

      {attempted && stepProblems.length > 0 && (
        <div role="alert" className="rounded-md border border-destructive/40 bg-destructive/5 p-3 text-sm text-destructive">
          <ul className="list-disc pl-5">
            {stepProblems.map((item) => (
              <li key={item.key + JSON.stringify(item.params)}>{t(`rollover.problems.${item.key}`, item.params)}</li>
            ))}
          </ul>
        </div>
      )}

      {step === 0 && (
        <section className="flex flex-col gap-4" aria-labelledby="rollover-year-title">
          <div>
            <h2 id="rollover-year-title" className="text-lg font-semibold">
              {t('rollover.year.title')}
            </h2>
            <p className="text-sm text-muted-foreground">{t('rollover.year.hint')}</p>
          </div>

          <FormField label={t('years.form.name')} required hint={t('years.form.nameHint')}>
            <Input value={year.name} maxLength={20} onChange={(event) => setYear({ ...year, name: event.target.value })} />
          </FormField>
          <div className="grid gap-4 sm:grid-cols-2">
            <FormField label={t('years.form.start')} required>
              <Input type="date" value={year.startDate} onChange={(event) => setYear({ ...year, startDate: event.target.value })} />
            </FormField>
            <FormField label={t('years.form.end')} required>
              <Input type="date" value={year.endDate} onChange={(event) => setYear({ ...year, endDate: event.target.value })} />
            </FormField>
          </div>

          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-medium">{t('years.form.terms')}</legend>
            <p className="text-sm text-muted-foreground">{t('years.form.termsHint')}</p>
            {year.terms.map((term, index) => (
              <div key={index} className="grid gap-3 rounded-md border p-3 sm:grid-cols-[1fr_auto]">
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
                {year.terms.length > 1 && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="self-end"
                    onClick={() => setYear({ ...year, terms: year.terms.filter((_, i) => i !== index) })}
                  >
                    <Trash2 aria-hidden="true" />
                    <span className="sr-only">{t('years.form.removeTerm')}</span>
                  </Button>
                )}
              </div>
            ))}
            {year.terms.length < MAX_TERMS && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="self-start"
                onClick={() => setYear({ ...year, terms: [...year.terms, { id: null, name: '', startDate: '', endDate: '' }] })}
              >
                <Plus aria-hidden="true" />
                {t('years.form.addTerm')}
              </Button>
            )}
          </fieldset>
        </section>
      )}

      {step === 1 && (
        <section className="flex flex-col gap-4" aria-labelledby="rollover-map-title">
          <div>
            <h2 id="rollover-map-title" className="text-lg font-semibold">
              {t('rollover.map.title')}
            </h2>
            <p className="text-sm text-muted-foreground">{t('rollover.map.description', { year: currentYear.name })}</p>
          </div>

          <div className="overflow-x-auto rounded-lg border">
            <table className="w-full text-left text-sm">
              <caption className="sr-only">{t('rollover.map.caption')}</caption>
              <thead className="bg-muted/50 text-muted-foreground">
                <tr>
                  <th scope="col" className="px-4 py-3 font-medium">{t('rollover.map.columns.classroom')}</th>
                  <th scope="col" className="px-4 py-3 font-medium">{t('rollover.map.columns.students')}</th>
                  <th scope="col" className="px-4 py-3 font-medium">{t('rollover.map.columns.action')}</th>
                  <th scope="col" className="px-4 py-3 font-medium">{t('rollover.map.columns.newClassroom')}</th>
                  <th scope="col" className="px-4 py-3 font-medium">{t('rollover.map.columns.homeroom')}</th>
                </tr>
              </thead>
              <tbody className="divide-y">
                {sourceClassrooms.map((classroom, index) => {
                  const mapping = mappings[index]
                  if (!mapping) return null
                  return (
                    <tr key={classroom.id}>
                      <th scope="row" className="px-4 py-3 font-medium">{classroom.name}</th>
                      <td className="px-4 py-3">{classroom.studentCount}</td>
                      <td className="px-4 py-3">
                        <Select
                          aria-label={`${t('rollover.map.columns.action')}: ${classroom.name}`}
                          value={mapping.graduate ? 'graduate' : 'move'}
                          onChange={(event) => changeMapping(index, { graduate: event.target.value === 'graduate' })}
                        >
                          <option value="move">{t('rollover.map.move')}</option>
                          <option value="graduate">{t('rollover.map.graduate')}</option>
                        </Select>
                      </td>
                      <td className="px-4 py-3">
                        <Input
                          aria-label={`${t('rollover.map.newName')}: ${classroom.name}`}
                          value={mapping.toClassroomName}
                          maxLength={50}
                          disabled={mapping.graduate}
                          onChange={(event) => changeMapping(index, { toClassroomName: event.target.value })}
                        />
                      </td>
                      <td className="px-4 py-3">
                        <Select
                          aria-label={`${t('rollover.map.columns.homeroom')}: ${classroom.name}`}
                          value={mapping.homeroomTeacherId}
                          disabled={mapping.graduate}
                          onChange={(event) => changeMapping(index, { homeroomTeacherId: event.target.value })}
                        >
                          <option value="">{t('rollover.map.homeroomPlaceholder')}</option>
                          {teachers.data?.map((teacher) => (
                            <option key={teacher.id} value={teacher.id}>
                              {teacher.fullName}
                            </option>
                          ))}
                        </Select>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </section>
      )}

      {step === 2 && (
        <section className="flex flex-col gap-4" aria-labelledby="rollover-review-title">
          <h2 id="rollover-review-title" className="text-lg font-semibold">
            {t('rollover.review.title')}
          </h2>
          <p>{t('rollover.review.summary', { ...summary, year: year.name.trim() })}</p>
          <p className="text-sm">
            {t('rollover.review.yearLine', { name: year.name.trim(), start: formatDate(year.startDate), end: formatDate(year.endDate), terms: year.terms.length })}
          </p>
          <ul aria-label={t('rollover.review.listLabel')} className="list-disc pl-5 text-sm">
            {mappings.map((mapping) => {
              const from = sourceClassrooms.find((classroom) => classroom.id === mapping.fromClassroomId)?.name ?? ''
              return (
                <li key={mapping.fromClassroomId}>
                  {mapping.graduate
                    ? t('rollover.review.graduateLine', { from })
                    : t('rollover.review.classroomLine', { from, to: mapping.toClassroomName.trim() })}
                </li>
              )
            })}
          </ul>
          <Alert tone="info" message={t('rollover.review.warning')} />
          {problem && <Alert tone="error" message={t(`rollover.errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? t('common.error') })} />}
        </section>
      )}

      <div className="flex justify-between gap-2">
        <Button variant="outline" onClick={() => setStep((current) => current - 1)} disabled={step === 0 || rollover.isPending}>
          <ArrowLeft aria-hidden="true" />
          {t('rollover.previous')}
        </Button>
        {step < STEPS.length - 1 ? (
          <Button onClick={next}>{t('rollover.next')}</Button>
        ) : (
          <Button onClick={confirm} disabled={rollover.isPending}>
            {rollover.isPending ? t('rollover.working') : t('rollover.confirm')}
          </Button>
        )}
      </div>
    </div>
  )
}

function BackLink() {
  const { t } = useTranslation('masterdata')
  return (
    <Link to="/manage/academic-years" className={buttonVariants({ variant: 'ghost', size: 'sm' }) + ' self-start'}>
      <ArrowLeft aria-hidden="true" />
      {t('rollover.back')}
    </Link>
  )
}
