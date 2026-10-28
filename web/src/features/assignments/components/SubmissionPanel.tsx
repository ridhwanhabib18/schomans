import { FileText, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { AssignmentDto, SubmissionDto } from '@/api/generated/model'
import { formatDateTime } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useOpenSubmissionFile, useSubmitAssignment, useUploadSubmissionFile } from '../api/assignments'
import { checkFile, SUBMISSION_TYPES } from '../files'
import '../i18n'

/** What was handed in: when, late or not, the file, the text, and the score with the teacher's feedback. */
export function SubmissionSummary({ submission }: { submission: SubmissionDto }) {
  const { t } = useTranslation('assignments')
  const showToast = useToastStore(selectShowToast)
  const openFile = useOpenSubmissionFile()

  return (
    <dl className="grid gap-3 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <dt className="sr-only">{t('submit.submittedAt', { date: '' })}</dt>
        <dd>{t('submit.submittedAt', { date: formatDateTime(submission.submittedAt) })}</dd>
        {submission.isLate && <Badge variant="warning">{t('submit.late')}</Badge>}
      </div>
      {submission.hasFile && (
        <div>
          <dt className="font-medium">{t('submit.file')}</dt>
          <dd>
            <Button
              variant="outline"
              size="sm"
              disabled={openFile.isPending}
              onClick={() => openFile.mutate(submission.id, { onError: () => showToast({ kind: 'error', title: t('detail.downloadFailed') }) })}
            >
              <FileText aria-hidden="true" />
              {submission.fileName ?? t('submit.openFile')}
            </Button>
          </dd>
        </div>
      )}
      {submission.textBody && (
        <div>
          <dt className="font-medium">{t('submit.text')}</dt>
          <dd className="whitespace-pre-wrap rounded-md bg-muted/60 p-3">{submission.textBody}</dd>
        </div>
      )}
      {submission.score !== null && (
        <div className="rounded-md border border-border p-3">
          <dt className="font-medium">{t('submit.score')}</dt>
          <dd className="text-2xl font-semibold tabular-nums">{submission.score}</dd>
          {submission.feedback && (
            <>
              <dt className="mt-2 font-medium">{t('submit.feedback')}</dt>
              <dd className="whitespace-pre-wrap">{submission.feedback}</dd>
            </>
          )}
        </div>
      )}
    </dl>
  )
}

/**
 * The student's side of an assignment: what they handed in, and the form to hand in (or again, until it is scored).
 * The file goes straight to storage through a presigned URL; then the work is saved with its key.
 */
export function SubmissionPanel({ assignment }: { assignment: AssignmentDto }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const upload = useUploadSubmissionFile(assignment.id)
  const submit = useSubmitAssignment()
  const input = useRef<HTMLInputElement>(null)
  const current = assignment.mySubmission
  const [file, setFile] = useState<File | null>(null)
  const [keepFile, setKeepFile] = useState(current?.hasFile ?? false)
  const [text, setText] = useState(current?.textBody ?? '')
  const [error, setError] = useState<string>()
  const graded = current?.score !== null && current?.score !== undefined
  const busy = upload.isPending || submit.isPending

  async function handIn() {
    setError(undefined)
    if (!file && !(keepFile && current?.hasFile) && text.trim() === '') {
      setError(t('submit.nothing'))
      return
    }

    try {
      // A new file replaces the old one; otherwise the earlier file stays when the student keeps it.
      const uploaded = file ? await upload.mutateAsync(file) : null
      const result = await submit.mutateAsync({
        assignmentId: assignment.id,
        fileKey: uploaded?.key ?? null,
        fileName: uploaded?.fileName ?? null,
        textBody: text.trim() || null,
        keepFile: !uploaded && keepFile && (current?.hasFile ?? false),
      })
      setFile(null)
      showToast({ kind: 'success', title: result.isLate ? t('submit.sentLate') : t('submit.sent') })
    } catch (e) {
      const problem = parseProblem(e)
      setError(t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }))
    }
  }

  return (
    <section aria-labelledby="my-work" className="flex flex-col gap-4 rounded-xl border border-border bg-card p-4">
      <h2 id="my-work" className="text-lg font-semibold">
        {t('submit.title')}
      </h2>

      {current ? <SubmissionSummary submission={current} /> : <p className="text-sm text-muted-foreground">{t('submit.notYet')}</p>}

      {graded ? (
        <Alert tone="info" message={t('submit.graded')} />
      ) : (
        <form
          noValidate
          className="flex flex-col gap-4 border-t border-border pt-4"
          onSubmit={(event) => {
            event.preventDefault()
            void handIn()
          }}
        >
          {error && <Alert tone="error" message={error} />}
          {current && <p className="text-sm text-muted-foreground">{t('submit.replaceHint')}</p>}

          <div className="flex flex-col gap-2">
            <span className="text-sm font-medium">{t('submit.pickFile')}</span>
            {current?.hasFile && !file && (
              <label className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={keepFile} onChange={(event) => setKeepFile(event.target.checked)} className="accent-[var(--primary)]" />
                {t('submit.keepFile', { name: current.fileName ?? '' })}
              </label>
            )}
            <input
              ref={input}
              type="file"
              accept={SUBMISSION_TYPES.join(',')}
              className="sr-only"
              aria-label={t('submit.pickFile')}
              onChange={(event) => {
                const picked = event.target.files?.[0] ?? null
                event.target.value = ''
                if (!picked) {
                  return
                }

                const problem = checkFile(picked, SUBMISSION_TYPES)
                if (problem) {
                  setError(problem === 'type' ? t('submit.wrongType') : t('submit.wrongSize'))
                  return
                }

                setError(undefined)
                setFile(picked)
              }}
            />
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" variant="outline" size="sm" onClick={() => input.current?.click()}>
                <Upload aria-hidden="true" />
                {t('submit.pickFile')}
              </Button>
              {file && (
                <span className="inline-flex items-center gap-1 text-sm">
                  <FileText aria-hidden="true" className="size-4" />
                  {file.name}
                  <Button type="button" variant="ghost" size="sm" onClick={() => setFile(null)}>
                    {t('submit.removeFile')}
                  </Button>
                </span>
              )}
            </div>
          </div>

          <FormField label={t('submit.textLabel')}>
            <Textarea value={text} rows={5} maxLength={20_000} onChange={(event) => setText(event.target.value)} />
          </FormField>

          <Button type="submit" className="self-start" disabled={busy}>
            {busy ? t('submit.sending') : current ? t('submit.replace') : t('submit.send')}
          </Button>
        </form>
      )}
    </section>
  )
}
