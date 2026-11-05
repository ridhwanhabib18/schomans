import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useSaveQuestions, useUpdateQuestion, type Question, type QuestionInput } from '../api/questions'
import { assignmentErrorText } from '../errors'
import { blankQuestion, isQuestionValid, QuestionEditor } from './QuestionEditor'
import '../i18n'

const toInput = (q: Question): QuestionInput => ({ type: q.type, difficulty: q.difficulty, body: q.body, options: q.options, answerKey: q.answerKey, explanation: q.explanation })

/** Adds a question to the bank of the subject, or changes one the teacher wrote. */
export function QuestionDialog({ subjectId, question, onClose }: { subjectId: string; question?: Question; onClose: () => void }) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSaveQuestions()
  const update = useUpdateQuestion()
  const [value, setValue] = useState<QuestionInput>(question ? toInput(question) : blankQuestion())
  const [error, setError] = useState<string>()
  const pending = save.isPending || update.isPending

  function submit() {
    setError(undefined)
    const done = () => {
      showToast({ kind: 'success', title: t('questions.saved') })
      onClose()
    }
    const fail = (e: unknown) => setError(assignmentErrorText(e, t, tCommon('errors.generic')))
    if (question) {
      update.mutate({ id: question.id, question: value }, { onSuccess: done, onError: fail })
    } else {
      save.mutate({ subjectId, questions: [value] }, { onSuccess: done, onError: fail })
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{question ? t('questions.edit') : t('questions.add')}</DialogTitle>
        </DialogHeader>
        <form
          noValidate
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault()
            if (isQuestionValid(value) && !pending) {
              submit()
            }
          }}
        >
          {error && <Alert tone="error" message={error} />}
          <QuestionEditor value={value} onChange={setValue} typeEditable={!question} label="dialog" />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={!isQuestionValid(value) || pending}>
              {pending ? tCommon('actions.saving') : t('questions.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
