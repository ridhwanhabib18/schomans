import { Sparkles, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import {
  QUESTION_DIFFICULTIES,
  QUESTION_TYPES,
  useGenerateQuestions,
  useSaveQuestions,
  type GeneratedQuestions,
  type QuestionDifficulty,
  type QuestionInput,
  type QuestionType,
} from '../api/questions'
import { assignmentErrorText } from '../errors'
import { isQuestionValid, QuestionEditor } from './QuestionEditor'
import '../i18n'

export const MAX_PDF_BYTES = 10 * 1024 * 1024
export const MAX_QUESTIONS = 15

/** The drafts the AI wrote: the teacher edits or removes each one and saves the rest to the bank. Nothing is saved before that. */
function Review({ subjectId, result, onClose }: { subjectId: string; result: GeneratedQuestions; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSaveQuestions()
  const [drafts, setDrafts] = useState<QuestionInput[]>(result.questions)
  const [error, setError] = useState<string>()
  const valid = drafts.length > 0 && drafts.every(isQuestionValid)

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-muted-foreground">{t('questions.generate.reviewIntro', { count: drafts.length, pages: result.pages })}</p>
      {result.truncated && <Alert tone="info" message={t('questions.generate.truncated')} />}
      {error && <Alert tone="error" message={error} />}
      <ol className="flex flex-col gap-4">
        {drafts.map((draft, index) => (
          <li key={index} className="flex flex-col gap-3 rounded-lg border border-border p-3">
            <div className="flex items-center justify-between gap-2">
              <span className="font-medium">{t('questions.generate.draftNumber', { number: index + 1 })}</span>
              <Button type="button" variant="outline" size="sm" onClick={() => setDrafts((current) => current.filter((_, i) => i !== index))}>
                <Trash2 aria-hidden="true" />
                {t('questions.generate.discard')}
                <span className="sr-only"> {t('questions.generate.draftNumber', { number: index + 1 })}</span>
              </Button>
            </div>
            <QuestionEditor value={draft} onChange={(next) => setDrafts((current) => current.map((d, i) => (i === index ? next : d)))} label={`draft-${index}`} />
          </li>
        ))}
      </ol>
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onClose}>
          {tCommon('actions.cancel')}
        </Button>
        <Button
          type="button"
          disabled={!valid || save.isPending}
          onClick={() => {
            setError(undefined)
            save.mutate(
              { subjectId, questions: drafts },
              {
                onSuccess: (saved) => {
                  showToast({ kind: 'success', title: t('questions.generate.saved', { count: saved.length }) })
                  onClose()
                },
                onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))),
              },
            )
          }}
        >
          {save.isPending ? tCommon('actions.saving') : t('questions.generate.saveAll', { count: drafts.length })}
        </Button>
      </DialogFooter>
    </div>
  )
}

/** AI-06: the teacher gives a PDF, a number, a difficulty and a type; the AI drafts questions from its text for the teacher to review. */
export function GenerateQuestionsDialog({ subjectId, onClose }: { subjectId: string; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const generate = useGenerateQuestions()
  const [file, setFile] = useState<File | null>(null)
  const [count, setCount] = useState(5)
  const [difficulty, setDifficulty] = useState<QuestionDifficulty>('Easy')
  const [type, setType] = useState<QuestionType>('MultipleChoice')
  const [error, setError] = useState<string>()
  const [result, setResult] = useState<GeneratedQuestions>()

  const fileProblem = file && (!file.name.toLowerCase().endsWith('.pdf') ? t('questions.generate.notPdf') : file.size > MAX_PDF_BYTES ? t('questions.generate.tooBig') : undefined)
  const ready = file !== null && !fileProblem && Number.isInteger(count) && count >= 1 && count <= MAX_QUESTIONS

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('questions.generate.title')}</DialogTitle>
          {!result && <DialogDescription>{t('questions.generate.intro')}</DialogDescription>}
        </DialogHeader>
        {result ? (
          <Review subjectId={subjectId} result={result} onClose={onClose} />
        ) : (
          <form
            noValidate
            className="flex flex-col gap-4"
            onSubmit={(event) => {
              event.preventDefault()
              if (!ready || generate.isPending) {
                return
              }
              setError(undefined)
              generate.mutate(
                { subjectId, count, difficulty, type, file },
                { onSuccess: setResult, onError: (e) => setError(assignmentErrorText(e, t, tCommon('errors.generic'))) },
              )
            }}
          >
            {error && <Alert tone="error" message={error} />}
            <FormField label={t('questions.generate.file')} required hint={t('questions.generate.fileHint')} error={fileProblem || undefined}>
              <Input type="file" accept="application/pdf,.pdf" onChange={(event) => setFile(event.target.files?.[0] ?? null)} />
            </FormField>
            <div className="flex flex-wrap gap-3">
              <FormField label={t('questions.generate.count')} required>
                <Input type="number" min={1} max={MAX_QUESTIONS} value={Number.isFinite(count) ? count : ''} className="w-24" onChange={(event) => setCount(event.target.value === '' ? Number.NaN : Number(event.target.value))} />
              </FormField>
              <FormField label={t('questions.editor.type')}>
                <Select value={type} onChange={(event) => setType(event.target.value as QuestionType)}>
                  {QUESTION_TYPES.map((value) => (
                    <option key={value} value={value}>
                      {t(`questions.type.${value}`)}
                    </option>
                  ))}
                </Select>
              </FormField>
              <FormField label={t('questions.editor.difficulty')}>
                <Select value={difficulty} onChange={(event) => setDifficulty(event.target.value as QuestionDifficulty)}>
                  {QUESTION_DIFFICULTIES.map((value) => (
                    <option key={value} value={value}>
                      {t(`questions.difficulty.${value}`)}
                    </option>
                  ))}
                </Select>
              </FormField>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {tCommon('actions.cancel')}
              </Button>
              <Button type="submit" disabled={!ready || generate.isPending}>
                <Sparkles aria-hidden="true" />
                {generate.isPending ? t('questions.generate.working') : t('questions.generate.submit')}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
