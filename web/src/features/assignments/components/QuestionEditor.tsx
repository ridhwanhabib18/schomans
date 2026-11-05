import { Plus, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { Input, Select, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { QUESTION_DIFFICULTIES, QUESTION_TYPES, type QuestionDifficulty, type QuestionInput, type QuestionType } from '../api/questions'
import '../i18n'

export const MIN_OPTIONS = 2
export const MAX_OPTIONS = 5

export const blankQuestion = (type: QuestionType = 'MultipleChoice'): QuestionInput => ({
  type,
  difficulty: 'Easy',
  body: '',
  options: type === 'MultipleChoice' ? ['', ''] : [],
  answerKey: type === 'MultipleChoice' ? '0' : '',
  explanation: '',
})

/** Whether the API will accept the question: the same rules as the entity and the validator. */
export function isQuestionValid(q: QuestionInput): boolean {
  if (q.body.trim() === '' || q.body.trim().length > 1000 || q.explanation.trim().length > 500 || q.answerKey.trim() === '' || q.answerKey.trim().length > 500) {
    return false
  }
  if (q.type === 'ShortAnswer') {
    return q.options.length === 0
  }
  const options = q.options.map((o) => o.trim())
  const position = Number(q.answerKey.trim())
  return (
    options.length >= MIN_OPTIONS &&
    options.length <= MAX_OPTIONS &&
    options.every((o) => o !== '' && o.length <= 300) &&
    new Set(options.map((o) => o.toUpperCase())).size === options.length &&
    Number.isInteger(position) &&
    position >= 0 &&
    position < options.length
  )
}

interface QuestionEditorProps {
  value: QuestionInput
  onChange: (value: QuestionInput) => void
  /** A new question can still change its type; a draft keeps the type that was asked for. */
  typeEditable?: boolean
  /** Makes the field ids unique when several editors are on one page. */
  label: string
}

/** One question: the text, the difficulty, the options with the right one marked (or the model answer), and the explanation. */
export function QuestionEditor({ value, onChange, typeEditable, label }: QuestionEditorProps) {
  const { t } = useTranslation('assignments')
  const right = Number(value.answerKey)
  const set = (patch: Partial<QuestionInput>) => onChange({ ...value, ...patch })

  function changeType(type: QuestionType) {
    onChange({ ...blankQuestion(type), difficulty: value.difficulty, body: value.body, explanation: value.explanation })
  }

  function removeOption(index: number) {
    const options = value.options.filter((_, i) => i !== index)
    const key = right === index ? 0 : right > index ? right - 1 : right
    set({ options, answerKey: String(key) })
  }

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <div className="flex flex-wrap gap-3">
        {typeEditable && (
          <FormField label={t('questions.editor.type')}>
            <Select value={value.type} onChange={(event) => changeType(event.target.value as QuestionType)}>
              {QUESTION_TYPES.map((type) => (
                <option key={type} value={type}>
                  {t(`questions.type.${type}`)}
                </option>
              ))}
            </Select>
          </FormField>
        )}
        <FormField label={t('questions.editor.difficulty')}>
          <Select value={value.difficulty} onChange={(event) => set({ difficulty: event.target.value as QuestionDifficulty })}>
            {QUESTION_DIFFICULTIES.map((difficulty) => (
              <option key={difficulty} value={difficulty}>
                {t(`questions.difficulty.${difficulty}`)}
              </option>
            ))}
          </Select>
        </FormField>
      </div>
      <FormField label={t('questions.editor.body')} required>
        <Textarea rows={2} maxLength={1000} value={value.body} onChange={(event) => set({ body: event.target.value })} />
      </FormField>

      {value.type === 'MultipleChoice' ? (
        <fieldset className="flex flex-col gap-2">
          <legend className="text-sm font-medium">{t('questions.editor.options')}</legend>
          {value.options.map((option, index) => (
            <div key={index} className="flex items-center gap-2">
              <input
                type="radio"
                name={`right-${label}`}
                checked={right === index}
                onChange={() => set({ answerKey: String(index) })}
                aria-label={t('questions.editor.markRight', { number: index + 1 })}
                className="size-4 accent-[var(--primary)]"
              />
              <Input
                value={option}
                maxLength={300}
                aria-label={t('questions.editor.option', { number: index + 1 })}
                onChange={(event) => set({ options: value.options.map((o, i) => (i === index ? event.target.value : o)) })}
              />
              {value.options.length > MIN_OPTIONS && (
                <Button type="button" variant="outline" size="sm" onClick={() => removeOption(index)} aria-label={t('questions.editor.removeOption', { number: index + 1 })}>
                  <Trash2 aria-hidden="true" />
                </Button>
              )}
            </div>
          ))}
          <div>
            <Button type="button" variant="outline" size="sm" disabled={value.options.length >= MAX_OPTIONS} onClick={() => set({ options: [...value.options, ''] })}>
              <Plus aria-hidden="true" />
              {t('questions.editor.addOption')}
            </Button>
          </div>
          <p className="text-xs text-muted-foreground">{t('questions.editor.rightHint')}</p>
        </fieldset>
      ) : (
        <FormField label={t('questions.editor.modelAnswer')} required hint={t('questions.editor.modelAnswerHint')}>
          <Textarea rows={2} maxLength={500} value={value.answerKey} onChange={(event) => set({ answerKey: event.target.value })} />
        </FormField>
      )}

      <FormField label={t('questions.editor.explanation')}>
        <Input maxLength={500} value={value.explanation} onChange={(event) => set({ explanation: event.target.value })} />
      </FormField>
    </div>
  )
}
