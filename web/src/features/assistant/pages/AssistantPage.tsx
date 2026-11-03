import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Card, CardContent } from '@/shared/ui/card'
import { Textarea } from '@/shared/ui/form-controls'
import { PageHeader } from '@/shared/ui/page-parts'
import { useAskAssistant } from '../api/assistant'
import type { AssistantSource, ChatMessage } from '../api/types'
import { assistantErrorText } from '../errors'
import '../i18n'

const MAX_QUESTION = 500
/** The server accepts at most 10 earlier turns. */
const MAX_HISTORY = 10

const EXAMPLES = ['attendance', 'grades', 'requests', 'risk'] as const

function Sources({ sources }: { sources: AssistantSource[] }) {
  const { t } = useTranslation('assistant')

  return (
    <div className="mt-3 flex flex-col gap-2 border-t border-border pt-3">
      <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{t('sources')}</p>
      <ul className="flex flex-col gap-2">
        {sources.map((source, index) => (
          <li key={`${source.tool}-${index}`} className="rounded-md bg-muted/50 p-2 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{source.label}</span>
              {source.url && (
                <Link to={source.url} className="text-primary underline-offset-4 hover:underline">
                  {t('openReport')}
                  <span className="sr-only">: {source.label}</span>
                </Link>
              )}
            </div>
            <dl className="mt-1 grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-muted-foreground">
              {source.figures.map((figure) => (
                <div key={figure.label} className="contents">
                  <dt>{figure.label}</dt>
                  <dd className="text-foreground">{figure.value}</dd>
                </div>
              ))}
            </dl>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** The principal asks questions in plain language; the assistant answers from the school's data and shows its sources. */
export function AssistantPage() {
  const { t } = useTranslation('assistant')
  const { t: tCommon } = useTranslation()
  const ask = useAskAssistant()
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [question, setQuestion] = useState('')
  const [error, setError] = useState<string>()
  const nextId = useRef(1)

  function send(text: string) {
    const trimmed = text.trim()
    if (trimmed === '' || ask.isPending) {
      return
    }

    const history = messages.slice(-MAX_HISTORY).map(({ role, text: body }) => ({ role, text: body }))
    setError(undefined)
    setQuestion('')
    setMessages((current) => [...current, { id: nextId.current++, role: 'user', text: trimmed }])
    ask.mutate(
      { question: trimmed, history },
      {
        onSuccess: (result) => setMessages((current) => [...current, { id: nextId.current++, role: 'assistant', text: result.answer, sources: result.sources }]),
        onError: (e) => setError(assistantErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={
          messages.length > 0 ? (
            <Button
              variant="outline"
              onClick={() => {
                setMessages([])
                setError(undefined)
              }}
            >
              {t('newConversation')}
            </Button>
          ) : undefined
        }
      />

      {messages.length === 0 ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm font-medium">{t('examples.title')}</p>
          <div className="flex flex-wrap gap-2">
            {EXAMPLES.map((example) => (
              <Button key={example} variant="outline" size="sm" onClick={() => send(t(`examples.${example}`))}>
                {t(`examples.${example}`)}
              </Button>
            ))}
          </div>
        </div>
      ) : (
        <ol className="flex flex-col gap-3" aria-live="polite">
          {messages.map((message) => (
            <li key={message.id} className={message.role === 'user' ? 'self-end sm:max-w-[80%]' : 'sm:max-w-[90%]'}>
              <Card className={message.role === 'user' ? 'bg-primary/5' : undefined}>
                <CardContent className="py-3">
                  <p className="text-xs font-medium text-muted-foreground">{message.role === 'user' ? t('you') : t('assistant')}</p>
                  <p className="mt-1 whitespace-pre-line text-sm">{message.text}</p>
                  {message.sources && message.sources.length > 0 && <Sources sources={message.sources} />}
                </CardContent>
              </Card>
            </li>
          ))}
          {ask.isPending && <li className="text-sm text-muted-foreground">{t('input.sending')}</li>}
        </ol>
      )}

      {error && <Alert tone="error" message={error} />}

      <form
        noValidate
        className="flex flex-col gap-2"
        onSubmit={(event) => {
          event.preventDefault()
          send(question)
        }}
      >
        <label className="text-sm font-medium" htmlFor="assistant-question">
          {t('input.label')}
        </label>
        <Textarea
          id="assistant-question"
          rows={3}
          maxLength={MAX_QUESTION}
          placeholder={t('input.placeholder')}
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && !event.shiftKey) {
              event.preventDefault()
              send(question)
            }
          }}
        />
        <div className="flex flex-wrap items-center justify-between gap-2">
          <span className="text-xs text-muted-foreground">{t('limit')}</span>
          <Button type="submit" disabled={ask.isPending || question.trim() === ''}>
            {ask.isPending ? t('input.sending') : t('input.send')}
          </Button>
        </div>
      </form>
    </div>
  )
}
