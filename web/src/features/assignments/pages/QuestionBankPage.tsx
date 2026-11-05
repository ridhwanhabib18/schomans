import { Plus, Sparkles } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDebouncedValue } from '@/shared/hooks/useDebouncedValue'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { Input, Select } from '@/shared/ui/form-controls'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useTeachingOptions } from '../api/assignments'
import {
  QUESTION_DIFFICULTIES,
  QUESTION_PAGE_SIZE,
  QUESTION_TYPES,
  useDeleteQuestion,
  useQuestions,
  type Question,
  type QuestionDifficulty,
  type QuestionType,
} from '../api/questions'
import { assignmentErrorText } from '../errors'
import { GenerateQuestionsDialog } from '../components/GenerateQuestionsDialog'
import { QuestionDialog } from '../components/QuestionDialog'
import '../i18n'

/** The question bank of the subjects a teacher teaches (AI-06): browse, add and change questions, or draft them from a PDF. */
export function QuestionBankPage() {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const options = useTeachingOptions()
  const subjects = useMemo(() => {
    const unique = new Map<string, string>()
    for (const option of options.data ?? []) {
      unique.set(option.subjectId, option.subjectName)
    }
    return [...unique].map(([id, name]) => ({ id, name }))
  }, [options.data])

  const [chosen, setChosen] = useState<string>()
  const subjectId = chosen ?? subjects[0]?.id
  const [type, setType] = useState<QuestionType>()
  const [difficulty, setDifficulty] = useState<QuestionDifficulty>()
  const [search, setSearch] = useState('')
  const debounced = useDebouncedValue(search)
  const [page, setPage] = useState(1)
  const list = useQuestions({ subjectId: subjectId ?? '', type, difficulty, search: debounced }, page, subjectId !== undefined)

  const [editing, setEditing] = useState<Question | 'new' | null>(null)
  const [generating, setGenerating] = useState(false)
  const [deleting, setDeleting] = useState<Question | null>(null)
  const remove = useDeleteQuestion()

  const filter = (action: () => void) => {
    action()
    setPage(1)
  }

  const columns: Column<Question>[] = [
    { key: 'body', header: t('questions.columns.body'), className: 'min-w-64', render: (q) => <span className="line-clamp-2">{q.body}</span> },
    { key: 'type', header: t('questions.columns.type'), render: (q) => t(`questions.type.${q.type}`) },
    {
      key: 'difficulty',
      header: t('questions.columns.difficulty'),
      render: (q) => <Badge variant={q.difficulty === 'Easy' ? 'success' : q.difficulty === 'Medium' ? 'warning' : 'destructive'}>{t(`questions.difficulty.${q.difficulty}`)}</Badge>,
    },
    { key: 'author', header: t('questions.columns.author'), render: (q) => (q.isMine ? t('questions.you') : q.createdByName) },
    {
      key: 'actions',
      header: t('questions.columns.actions'),
      render: (q) =>
        q.isMine ? (
          <div className="flex gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => setEditing(q)} aria-label={t('questions.editQuestion', { body: q.body })}>
              {tCommon('actions.edit')}
            </Button>
            <Button type="button" variant="outline" size="sm" onClick={() => setDeleting(q)} aria-label={t('questions.deleteQuestion', { body: q.body })}>
              {tCommon('actions.delete')}
            </Button>
          </div>
        ) : null,
    },
  ]

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader
        title={t('questions.title')}
        description={t('questions.description')}
        actions={
          subjectId ? (
            <>
              <Button type="button" variant="outline" onClick={() => setGenerating(true)}>
                <Sparkles aria-hidden="true" />
                {t('questions.generate.open')}
              </Button>
              <Button type="button" onClick={() => setEditing('new')}>
                <Plus aria-hidden="true" />
                {t('questions.add')}
              </Button>
            </>
          ) : undefined
        }
      />

      {options.isError ? (
        <ErrorNotice message={t('questions.loadError')} onRetry={() => void options.refetch()} />
      ) : options.isSuccess && subjects.length === 0 ? (
        <EmptyState title={t('questions.noSubjects.title')} description={t('questions.noSubjects.body')} />
      ) : (
        <>
          <div className="flex flex-wrap gap-4">
            <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
              {t('questions.filters.subject')}
              <Select value={subjectId ?? ''} onChange={(event) => filter(() => setChosen(event.target.value))}>
                {subjects.map((subject) => (
                  <option key={subject.id} value={subject.id}>
                    {subject.name}
                  </option>
                ))}
              </Select>
            </label>
            <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
              {t('questions.editor.type')}
              <Select value={type ?? ''} onChange={(event) => filter(() => setType((event.target.value || undefined) as QuestionType | undefined))}>
                <option value="">{t('questions.filters.all')}</option>
                {QUESTION_TYPES.map((value) => (
                  <option key={value} value={value}>
                    {t(`questions.type.${value}`)}
                  </option>
                ))}
              </Select>
            </label>
            <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
              {t('questions.editor.difficulty')}
              <Select value={difficulty ?? ''} onChange={(event) => filter(() => setDifficulty((event.target.value || undefined) as QuestionDifficulty | undefined))}>
                <option value="">{t('questions.filters.all')}</option>
                {QUESTION_DIFFICULTIES.map((value) => (
                  <option key={value} value={value}>
                    {t(`questions.difficulty.${value}`)}
                  </option>
                ))}
              </Select>
            </label>
            <label className="flex min-w-0 flex-col gap-1.5 text-sm font-medium">
              {t('questions.filters.search')}
              <Input value={search} maxLength={100} onChange={(event) => filter(() => setSearch(event.target.value))} />
            </label>
          </div>

          {list.isError ? (
            <ErrorNotice message={t('questions.loadError')} onRetry={() => void list.refetch()} />
          ) : (
            <>
              <DataTable
                columns={columns}
                rows={list.data?.items}
                rowKey={(q) => q.id}
                isLoading={options.isPending || list.isPending}
                caption={t('questions.caption')}
                empty={<EmptyState title={t('questions.empty.title')} description={t('questions.empty.body')} />}
              />
              {list.data && list.data.totalCount > QUESTION_PAGE_SIZE && <Pagination page={page} pageSize={QUESTION_PAGE_SIZE} totalCount={list.data.totalCount} onPageChange={setPage} />}
            </>
          )}
        </>
      )}

      {subjectId && editing && <QuestionDialog subjectId={subjectId} question={editing === 'new' ? undefined : editing} onClose={() => setEditing(null)} />}
      {subjectId && generating && <GenerateQuestionsDialog subjectId={subjectId} onClose={() => setGenerating(false)} />}
      <ConfirmDialog
        open={deleting !== null}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={t('questions.deleteTitle')}
        description={deleting?.body}
        confirmLabel={tCommon('actions.delete')}
        pending={remove.isPending}
        onConfirm={() =>
          remove.mutate(deleting!.id, {
            onSuccess: () => {
              setDeleting(null)
              showToast({ kind: 'success', title: t('questions.deleted') })
            },
            onError: (e) => {
              setDeleting(null)
              showToast({ kind: 'error', title: assignmentErrorText(e, t, tCommon('errors.generic')) })
            },
          })
        }
      />
    </div>
  )
}
