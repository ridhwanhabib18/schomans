import { ArrowLeft } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button, buttonVariants } from '@/shared/ui/button'
import { Checkbox, Input, Select, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useTeachingOptions } from '../api/assignments'
import { useQuestions, type Question } from '../api/questions'
import { useCreateQuiz } from '../api/quizzes'
import { assignmentErrorText } from '../errors'
import '../i18n'

const MAX_QUESTIONS = 50
const BANK_PAGE_SIZE = 100

interface Picked {
  question: Question
  points: number
}

/** `/assignments/quizzes/new`: a teacher builds an online quiz from the question bank of a subject. */
export function QuizBuilderPage() {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const options = useTeachingOptions()
  const create = useCreateQuiz()

  const subjects = useMemo(() => {
    const unique = new Map<string, string>()
    for (const option of options.data ?? []) {
      unique.set(option.subjectId, option.subjectName)
    }
    return [...unique].map(([id, name]) => ({ id, name }))
  }, [options.data])

  const [chosen, setChosen] = useState<string>()
  const subjectId = chosen ?? subjects[0]?.id
  const classrooms = (options.data ?? []).filter((o) => o.subjectId === subjectId)
  const [classroomIds, setClassroomIds] = useState<string[]>([])
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [dueAt, setDueAt] = useState('')
  const [picked, setPicked] = useState<Picked[]>([])
  const [search, setSearch] = useState('')
  const debounced = useDebouncedValue(search)
  const bank = useQuestions({ subjectId: subjectId ?? '', search: debounced }, 1, subjectId !== undefined, BANK_PAGE_SIZE)
  const [error, setError] = useState<string>()

  const pickedIds = new Set(picked.map((p) => p.question.id))
  const total = picked.reduce((sum, p) => sum + (Number.isFinite(p.points) ? p.points : 0), 0)
  const dueValid = dueAt !== '' && new Date(dueAt).getTime() > Date.now()
  const ready =
    title.trim() !== '' && dueValid && classroomIds.length > 0 && picked.length >= 1 && picked.length <= MAX_QUESTIONS && picked.every((p) => Number.isInteger(p.points) && p.points >= 1 && p.points <= 100)

  function changeSubject(id: string) {
    setChosen(id)
    setClassroomIds([])
    setPicked([])
    setSearch('')
  }

  function toggle(question: Question) {
    setPicked((current) => (current.some((p) => p.question.id === question.id) ? current.filter((p) => p.question.id !== question.id) : [...current, { question, points: 1 }]))
  }

  function submit() {
    setError(undefined)
    create.mutate(
      {
        title: title.trim(),
        description: description.trim() || null,
        dueAt: new Date(dueAt).toISOString(),
        subjectId: subjectId!,
        classroomIds,
        questions: picked.map((p) => ({ questionId: p.question.id, points: p.points })),
      },
      {
        onSuccess: (quiz) => {
          showToast({ kind: 'success', title: t('quiz.builder.created') })
          void navigate(`/assignments/${quiz.id}`)
        },
        onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link to="/assignments" className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('form.back')}
      </Link>
      <PageHeader title={t('quiz.builder.title')} description={t('quiz.builder.description')} />

      {options.isPending ? (
        <Skeleton className="h-40" />
      ) : options.isError ? (
        <ErrorNotice message={t('detail.error')} onRetry={() => void options.refetch()} />
      ) : subjects.length === 0 ? (
        <Alert tone="info" message={t('form.noOptions')} />
      ) : (
        <form
          noValidate
          className="flex flex-col gap-6"
          onSubmit={(event) => {
            event.preventDefault()
            if (ready && !create.isPending) {
              submit()
            }
          }}
        >
          {error && <Alert tone="error" message={error} />}
          <FormField label={t('quiz.builder.name')} required>
            <Input value={title} maxLength={200} onChange={(event) => setTitle(event.target.value)} />
          </FormField>
          <FormField label={t('quiz.builder.instructions')}>
            <Textarea rows={3} value={description} onChange={(event) => setDescription(event.target.value)} />
          </FormField>
          <div className="flex flex-wrap gap-4">
            <FormField label={t('questions.filters.subject')} required>
              <Select value={subjectId ?? ''} onChange={(event) => changeSubject(event.target.value)}>
                {subjects.map((subject) => (
                  <option key={subject.id} value={subject.id}>
                    {subject.name}
                  </option>
                ))}
              </Select>
            </FormField>
            <FormField label={t('quiz.builder.due')} required hint={t('quiz.builder.dueHint')} error={dueAt !== '' && !dueValid ? t('quiz.builder.dueInPast') : undefined}>
              <Input type="datetime-local" value={dueAt} onChange={(event) => setDueAt(event.target.value)} />
            </FormField>
          </div>

          <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-medium">{t('quiz.builder.classrooms')}</legend>
            <div className="flex flex-wrap gap-4">
              {classrooms.map((option) => (
                <label key={option.classroomId} className="flex items-center gap-2 text-sm">
                  <Checkbox
                    checked={classroomIds.includes(option.classroomId)}
                    onChange={(event) =>
                      setClassroomIds((current) => (event.target.checked ? [...current, option.classroomId] : current.filter((id) => id !== option.classroomId)))
                    }
                  />
                  {option.classroomName}
                </label>
              ))}
            </div>
          </fieldset>

          <section aria-labelledby="bank-title" className="flex flex-col gap-3">
            <h2 id="bank-title" className="font-semibold">
              {t('quiz.builder.bank')}
            </h2>
            <FormField label={t('questions.filters.search')} className="max-w-sm">
              <Input value={search} maxLength={100} onChange={(event) => setSearch(event.target.value)} />
            </FormField>
            {bank.isError ? (
              <ErrorNotice message={t('questions.loadError')} onRetry={() => void bank.refetch()} />
            ) : bank.isPending ? (
              <Skeleton className="h-32" />
            ) : bank.data.items.length === 0 ? (
              <p className="text-sm text-muted-foreground">
                {t('quiz.builder.emptyBank')} <Link to="/assignments/questions" className="text-primary underline-offset-4 hover:underline">{t('quiz.builder.openBank')}</Link>
              </p>
            ) : (
              <>
                <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto rounded-lg border border-border p-2">
                  {bank.data.items.map((question) => (
                    <li key={question.id}>
                      <label className="flex items-start gap-2 rounded-md p-2 text-sm hover:bg-muted">
                        <Checkbox checked={pickedIds.has(question.id)} onChange={() => toggle(question)} disabled={!pickedIds.has(question.id) && picked.length >= MAX_QUESTIONS} className="mt-0.5" />
                        <span className="min-w-0 flex-1">{question.body}</span>
                        <Badge variant="muted">{t(`questions.type.${question.type}`)}</Badge>
                        <Badge variant="muted">{t(`questions.difficulty.${question.difficulty}`)}</Badge>
                      </label>
                    </li>
                  ))}
                </ul>
                {bank.data.totalCount > BANK_PAGE_SIZE && <p className="text-xs text-muted-foreground">{t('quiz.builder.bankMore', { count: BANK_PAGE_SIZE })}</p>}
              </>
            )}
          </section>

          <section aria-labelledby="picked-title" className="flex flex-col gap-3">
            <h2 id="picked-title" className="font-semibold">
              {t('quiz.builder.picked', { count: picked.length, total })}
            </h2>
            {picked.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('quiz.builder.nonePicked')}</p>
            ) : (
              <ol className="flex flex-col gap-2">
                {picked.map((p, index) => (
                  <li key={p.question.id} className="flex flex-wrap items-center gap-3 rounded-lg border border-border p-2 text-sm">
                    <span className="w-6 tabular-nums">{index + 1}.</span>
                    <span className="min-w-0 flex-1">{p.question.body}</span>
                    <label className="flex items-center gap-2">
                      <span className="text-muted-foreground">{t('quiz.builder.pointsLabel')}</span>
                      <Input
                        type="number"
                        min={1}
                        max={100}
                        value={Number.isFinite(p.points) ? p.points : ''}
                        aria-label={t('quiz.builder.pointsFor', { number: index + 1 })}
                        className="w-20"
                        onChange={(event) =>
                          setPicked((current) => current.map((c) => (c.question.id === p.question.id ? { ...c, points: event.target.value === '' ? Number.NaN : Number(event.target.value) } : c)))
                        }
                      />
                    </label>
                    <Button type="button" variant="outline" size="sm" onClick={() => toggle(p.question)} aria-label={t('quiz.builder.remove', { number: index + 1 })}>
                      {tCommon('actions.delete')}
                    </Button>
                  </li>
                ))}
              </ol>
            )}
            <p className="text-xs text-muted-foreground">{t('quiz.builder.lockNote')}</p>
          </section>

          <div>
            <Button type="submit" disabled={!ready || create.isPending}>
              {create.isPending ? tCommon('actions.saving') : t('quiz.builder.create')}
            </Button>
          </div>
        </form>
      )}
    </div>
  )
}
