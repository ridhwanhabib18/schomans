import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/form-controls'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useGradeBands, useReplaceGradeBands } from '../api/grading'
import type { GradeBand } from '../api/types'
import { gradingErrorText } from '../errors'
import '../i18n'

const MAX_BANDS = 10

interface Draft {
  key: number
  label: string
  minScore: string
}

/** Why the draft cannot be saved, as a key of the `bands` texts; null when it is fine. */
function problemOf(drafts: Draft[]): 'invalid' | 'duplicate' | 'needZero' | 'tooMany' | null {
  if (drafts.length === 0 || drafts.some((d) => d.label.trim() === '' || d.minScore === '' || Number(d.minScore) < 0 || Number(d.minScore) > 100)) {
    return 'invalid'
  }
  if (drafts.length > MAX_BANDS) {
    return 'tooMany'
  }
  if (new Set(drafts.map((d) => d.label.trim().toUpperCase())).size !== drafts.length || new Set(drafts.map((d) => Number(d.minScore))).size !== drafts.length) {
    return 'duplicate'
  }
  return drafts.some((d) => Number(d.minScore) === 0) ? null : 'needZero'
}

function BandsEditor({ bands }: { bands: GradeBand[] }) {
  const { t } = useTranslation('grading')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const replace = useReplaceGradeBands()
  const [nextKey, setNextKey] = useState(bands.length)
  const [drafts, setDrafts] = useState<Draft[]>(() => bands.map((b, index) => ({ key: index, label: b.label, minScore: String(b.minScore) })))
  const [error, setError] = useState<string>()
  const problem = problemOf(drafts)

  const update = (key: number, patch: Partial<Draft>) => setDrafts((current) => current.map((d) => (d.key === key ? { ...d, ...patch } : d)))

  function save() {
    setError(undefined)
    replace.mutate(
      drafts.map((d) => ({ label: d.label.trim(), minScore: Number(d.minScore) })),
      {
        onSuccess: () => showToast({ kind: 'success', title: t('bands.saved') }),
        onError: (e) => setError(gradingErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <form
      noValidate
      className="flex max-w-xl flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        save()
      }}
    >
      {error && <Alert tone="error" message={error} />}
      <ul className="flex flex-col gap-3">
        {drafts.map((draft) => (
          <li key={draft.key} className="flex items-end gap-3">
            <label className="flex flex-1 flex-col gap-1.5 text-sm font-medium">
              {t('bands.label')}
              <Input maxLength={10} value={draft.label} onChange={(event) => update(draft.key, { label: event.target.value })} />
            </label>
            <label className="flex flex-1 flex-col gap-1.5 text-sm font-medium">
              {t('bands.minScore')}
              <Input
                type="number"
                min={0}
                max={100}
                step="0.01"
                className="tabular-nums"
                value={draft.minScore}
                onChange={(event) => update(draft.key, { minScore: event.target.value })}
              />
            </label>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              disabled={drafts.length === 1}
              onClick={() => setDrafts((current) => current.filter((d) => d.key !== draft.key))}
            >
              <Trash2 aria-hidden="true" />
              <span className="sr-only">{t('bands.remove', { label: draft.label || '…' })}</span>
            </Button>
          </li>
        ))}
      </ul>
      <div>
        <Button
          type="button"
          variant="outline"
          disabled={drafts.length >= MAX_BANDS}
          onClick={() => {
            setDrafts((current) => [...current, { key: nextKey, label: '', minScore: '' }])
            setNextKey((key) => key + 1)
          }}
        >
          <Plus aria-hidden="true" />
          {t('bands.add')}
        </Button>
      </div>
      {problem && <p className="text-sm text-destructive">{t(`bands.${problem}`)}</p>}
      <div>
        <Button type="submit" disabled={problem !== null || replace.isPending}>
          {replace.isPending ? tCommon('actions.saving') : t('bands.save')}
        </Button>
      </div>
    </form>
  )
}

/** The admin sets the letters on grades and report cards (A to E by default). */
export function GradeBandsPage() {
  const { t } = useTranslation('grading')
  const { data, isPending, isError, refetch } = useGradeBands()

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={t('bands.title')} description={t('bands.description')} />
      {isPending ? (
        <Skeleton className="h-64 max-w-xl" />
      ) : isError ? (
        <ErrorNotice message={t('bands.error')} onRetry={() => void refetch()} />
      ) : (
        <BandsEditor key={data.map((b) => `${b.id}:${b.label}:${b.minScore}`).join('|')} bands={[...data].sort((a, b) => b.minScore - a.minScore)} />
      )}
    </div>
  )
}
