import { Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import type { DocumentDraft } from '@/shared/lib/drafts'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { DataTable, Pagination, type Column } from '@/shared/ui/data-table'
import { DraftDialog } from '@/shared/ui/draft-dialog'
import { Input, Select, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { EmptyState, ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { PAGE_SIZE, useGeneralLetters, useIssueGeneralLetter, useOpenGeneralLetter, type GeneralLetter, type GeneralLetterKind } from '../api/letters'
import '../i18n'

export const MAX_PARAGRAPHS = 8
export const MAX_PARAGRAPH_LENGTH = 800
export const MAX_BODY_LENGTH = 4000
const KINDS: readonly GeneralLetterKind[] = ['Invitation', 'Notice']

/** The text of the editor as paragraphs: blocks separated by a blank line. */
export const toParagraphs = (body: string): string[] =>
  body
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph !== '')

/** Why the text cannot be issued yet, as a key of the `form` texts; null when it can. The API checks the same limits. */
export function bodyProblem(paragraphs: readonly string[]): 'tooManyParagraphs' | 'paragraphTooLong' | 'tooLong' | null {
  if (paragraphs.length > MAX_PARAGRAPHS) {
    return 'tooManyParagraphs'
  }
  if (paragraphs.some((paragraph) => paragraph.length > MAX_PARAGRAPH_LENGTH)) {
    return 'paragraphTooLong'
  }
  return paragraphs.join('\n\n').length > MAX_BODY_LENGTH ? 'tooLong' : null
}

function NewLetter() {
  const { t } = useTranslation('letters')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const issue = useIssueGeneralLetter()
  const [kind, setKind] = useState<GeneralLetterKind>('Invitation')
  const [recipient, setRecipient] = useState('')
  const [subject, setSubject] = useState('')
  const [body, setBody] = useState('')
  const [drafting, setDrafting] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string>()

  const paragraphs = toParagraphs(body)
  const problem = bodyProblem(paragraphs)
  const ready = recipient.trim() !== '' && subject.trim() !== '' && paragraphs.length > 0 && problem === null

  // The AI draft lands in the form like typed text: the person reads it, changes it, and only then issues the letter.
  function applyDraft(draft: DocumentDraft) {
    setSubject(draft.title)
    setBody(draft.paragraphs.join('\n\n'))
  }

  function send() {
    setError(undefined)
    issue.mutate(
      { kind, recipient: recipient.trim(), subject: subject.trim(), paragraphs },
      {
        onSuccess: (letter) => {
          setConfirming(false)
          showToast({ kind: 'success', title: t('form.issued', { number: letter.letterNumber }) })
          setRecipient('')
          setSubject('')
          setBody('')
        },
        onError: (e) => {
          setConfirming(false)
          const code = parseProblem(e).code
          setError((code && t(`errors.${code}`, { defaultValue: '' })) || parseProblem(e).title || tCommon('errors.generic'))
        },
      },
    )
  }

  return (
    <section aria-labelledby="new-letter-title" className="flex max-w-3xl flex-col gap-4 rounded-lg border border-border p-4">
      <h2 id="new-letter-title" className="font-semibold">
        {t('form.title')}
      </h2>
      {error && <Alert tone="error" message={error} />}
      <form
        noValidate
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault()
          if (ready) {
            setConfirming(true)
          }
        }}
      >
        <FormField label={t('form.kind')} className="sm:max-w-xs">
          <Select value={kind} onChange={(event) => setKind(event.target.value as GeneralLetterKind)}>
            {KINDS.map((value) => (
              <option key={value} value={value}>
                {t(`kind.${value}`)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t('form.recipient')} required hint={t('form.recipientHint')}>
          <Input value={recipient} maxLength={150} onChange={(event) => setRecipient(event.target.value)} />
        </FormField>
        <div>
          <Button type="button" variant="outline" size="sm" onClick={() => setDrafting(true)}>
            <Sparkles aria-hidden="true" />
            {t('form.draftWithAi')}
          </Button>
        </div>
        <FormField label={t('form.subject')} required>
          <Input value={subject} maxLength={150} onChange={(event) => setSubject(event.target.value)} />
        </FormField>
        <FormField
          label={t('form.body')}
          required
          hint={t('form.bodyHint')}
          error={problem ? t(`form.${problem}`, { max: problem === 'tooManyParagraphs' ? MAX_PARAGRAPHS : MAX_PARAGRAPH_LENGTH }) : undefined}
        >
          <Textarea rows={10} value={body} onChange={(event) => setBody(event.target.value)} />
        </FormField>
        <div>
          <Button type="submit" disabled={!ready || issue.isPending}>
            {issue.isPending ? t('form.issuing') : t('form.issue')}
          </Button>
        </div>
      </form>

      <DraftDialog open={drafting} onOpenChange={setDrafting} type={kind} replaces={subject.trim() !== '' || body.trim() !== ''} onDraft={applyDraft} />
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t('form.confirmTitle')}
        description={t('form.confirmBody')}
        confirmLabel={t('form.issue')}
        pending={issue.isPending}
        onConfirm={send}
      />
    </section>
  )
}

function IssuedLetters() {
  const { t } = useTranslation('letters')
  const [page, setPage] = useState(1)
  const { data, isPending, isError, refetch } = useGeneralLetters(page)
  const open = useOpenGeneralLetter()

  const columns: Column<GeneralLetter>[] = [
    { key: 'number', header: t('list.columns.number'), className: 'whitespace-nowrap tabular-nums', render: (l) => l.letterNumber },
    { key: 'kind', header: t('list.columns.kind'), render: (l) => t(`kind.${l.kind}`) },
    { key: 'recipient', header: t('list.columns.recipient'), render: (l) => l.recipient },
    { key: 'subject', header: t('list.columns.subject'), render: (l) => l.subject },
    { key: 'signedBy', header: t('list.columns.signedBy'), render: (l) => l.signedByName },
    { key: 'issued', header: t('list.columns.issued'), className: 'whitespace-nowrap tabular-nums', render: (l) => formatDateTime(l.issuedAt) },
    {
      key: 'actions',
      header: t('list.columns.actions'),
      render: (l) => (
        <Button type="button" variant="outline" size="sm" disabled={open.isPending} onClick={() => open.mutate(l.id)} aria-label={t('list.openFor', { number: l.letterNumber })}>
          {t('list.open')}
        </Button>
      ),
    },
  ]

  return (
    <section aria-labelledby="issued-letters-title" className="flex min-w-0 flex-col gap-3">
      <h2 id="issued-letters-title" className="font-semibold">
        {t('list.title')}
      </h2>
      {isError ? (
        <ErrorNotice message={t('list.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={data?.items}
            rowKey={(l) => l.id}
            isLoading={isPending}
            caption={t('list.caption')}
            empty={<EmptyState title={t('list.empty.title')} description={t('list.empty.body')} />}
          />
          {data && data.totalCount > PAGE_SIZE && <Pagination page={page} pageSize={PAGE_SIZE} totalCount={data.totalCount} onPageChange={setPage} />}
        </>
      )}
    </section>
  )
}

/** AI-09, general letters: staff write an invitation or a notice (the AI can draft it), and the system numbers, signs and stores it as a PDF. */
export function GeneralLettersPage() {
  const { t } = useTranslation('letters')

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('title')} description={t('description')} />
      <NewLetter />
      <IssuedLetters />
    </div>
  )
}
