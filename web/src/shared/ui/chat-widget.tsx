import { MessageCircle } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { useAskChatbot, type ChatbotAnswer, type ChatbotAudience } from '@/shared/hooks/useChatbot'
import { parseProblem } from '@/shared/lib/problem'
import { Alert } from './alert'
import { Button } from './button'
import { Input } from './form-controls'

export const CHAT_QUESTION_MAX = 300

interface Message {
  id: number
  role: 'user' | 'bot'
  text: string
  reply?: ChatbotAnswer
}

/**
 * AI-08: a small chat for questions about the school and admissions. It answers only from what the school has published and links to
 * the source of every answer; when the sources do not have the answer it says so and gives the staff contact. Nothing is stored in the browser.
 */
export function ChatWidget({ audience }: { audience: ChatbotAudience }) {
  const { t } = useTranslation()
  const ask = useAskChatbot(audience)
  const [messages, setMessages] = useState<Message[]>([])
  const [question, setQuestion] = useState('')
  const [error, setError] = useState<string>()
  const nextId = useRef(1)

  function send() {
    const text = question.trim()
    if (text === '' || ask.isPending) {
      return
    }

    setError(undefined)
    setQuestion('')
    setMessages((current) => [...current, { id: nextId.current++, role: 'user', text }])
    ask.mutate(text, {
      onSuccess: (reply) => setMessages((current) => [...current, { id: nextId.current++, role: 'bot', text: reply.answer, reply }]),
      onError: (e) => {
        const problem = parseProblem(e)
        const known = problem.code ? t(`chatbot.errors.${problem.code}`, { defaultValue: '' }) : ''
        setError(known || (problem.status === 429 ? t('chatbot.errors.tooMany') : problem.title) || t('errors.generic'))
      },
    })
  }

  const inputId = `chat-question-${audience}`

  return (
    <section aria-labelledby={`${inputId}-title`} className="flex flex-col gap-4 rounded-lg border border-border bg-card p-4 text-card-foreground">
      <header>
        <h2 id={`${inputId}-title`} className="flex items-center gap-2 text-lg font-semibold">
          <MessageCircle aria-hidden="true" className="size-5 text-primary" />
          {t(`chatbot.title.${audience}`)}
        </h2>
        <p className="text-sm text-muted-foreground">{t(`chatbot.intro.${audience}`)}</p>
      </header>

      {messages.length > 0 && (
        <ol className="flex flex-col gap-3" aria-live="polite">
          {messages.map((message) => (
            <li key={message.id} className={message.role === 'user' ? 'self-end rounded-lg bg-primary/10 px-3 py-2 text-sm sm:max-w-[80%]' : 'rounded-lg bg-muted px-3 py-2 text-sm sm:max-w-[90%]'}>
              <p className="text-xs font-medium text-muted-foreground">{message.role === 'user' ? t('chatbot.you') : t('chatbot.bot')}</p>
              <p className="mt-1 whitespace-pre-line">{message.text}</p>
              {message.reply?.known && message.reply.sources.length > 0 && (
                <p className="mt-2 text-xs text-muted-foreground">
                  {t('chatbot.sources')}{' '}
                  {message.reply.sources.map((source, index) => (
                    <span key={source.url}>
                      {index > 0 && ', '}
                      <Link to={source.url} className="text-primary underline-offset-4 hover:underline">
                        {source.title}
                      </Link>
                    </span>
                  ))}
                </p>
              )}
            </li>
          ))}
          {ask.isPending && <li className="text-sm text-muted-foreground">{t('chatbot.thinking')}</li>}
        </ol>
      )}

      {error && <Alert tone="error" message={error} />}

      <form
        noValidate
        className="flex flex-col gap-2 sm:flex-row sm:items-end"
        onSubmit={(event) => {
          event.preventDefault()
          send()
        }}
      >
        <div className="flex min-w-0 flex-1 flex-col gap-1.5">
          <label htmlFor={inputId} className="text-sm font-medium">
            {t('chatbot.label')}
          </label>
          <Input id={inputId} value={question} maxLength={CHAT_QUESTION_MAX} placeholder={t('chatbot.placeholder')} onChange={(event) => setQuestion(event.target.value)} />
        </div>
        <Button type="submit" disabled={ask.isPending || question.trim() === ''}>
          {ask.isPending ? t('chatbot.thinking') : t('chatbot.send')}
        </Button>
      </form>
      <p className="text-xs text-muted-foreground">{t('chatbot.note')}</p>
    </section>
  )
}
