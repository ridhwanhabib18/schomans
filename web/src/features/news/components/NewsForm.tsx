import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { NewsCategory, NewsDto } from '@/api/generated/model'
import { fieldError, isConcurrencyConflict, parseProblem, type Problem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { CoverImageField } from '@/shared/ui/cover-image-field'
import { Input, Select, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateNews, usePublishNews, useUnpublishNews, useUpdateNews } from '../api/news'
import '../i18n'

const CATEGORIES: readonly NonNullable<NewsCategory>[] = ['News', 'AdmissionInfo', 'Award']
const META_MAX = 300

interface NewsFormProps {
  /** The saved article, or undefined for a new one. */
  article: NewsDto | undefined
  onReload: () => void
}

/** Create or edit a news article: text, category, search description, cover image, and publishing. */
export function NewsForm({ article, onReload }: NewsFormProps) {
  const { t } = useTranslation('news')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateNews()
  const update = useUpdateNews()
  const publish = usePublishNews()
  const unpublish = useUnpublishNews()

  const [title, setTitle] = useState(article?.title ?? '')
  const [category, setCategory] = useState<NonNullable<NewsCategory>>((article?.category as NonNullable<NewsCategory> | undefined) ?? 'News')
  const [meta, setMeta] = useState(article?.metaDescription ?? '')
  const [body, setBody] = useState(article?.body ?? '')
  const [coverKey, setCoverKey] = useState<string | null>(article?.coverImageKey ?? null)
  const [coverUrl, setCoverUrl] = useState<string | null>(article?.coverImageUrl ?? null)
  const [problem, setProblem] = useState<Problem>()
  const [confirm, setConfirm] = useState<'publish' | 'unpublish' | null>(null)

  const saving = create.isPending || update.isPending
  const dirty =
    !article ||
    title !== article.title ||
    category !== article.category ||
    meta !== (article.metaDescription ?? '') ||
    body !== article.body ||
    coverKey !== (article.coverImageKey ?? null)

  function save() {
    setProblem(undefined)
    const fields = { title, body, category, metaDescription: meta.trim() || null }

    if (!article) {
      create.mutate(fields, {
        onSuccess: (created) => {
          showToast({ kind: 'success', title: t('form.saved') })
          void navigate(`/manage/news/${created.id}`, { replace: true })
        },
        onError: (error) => setProblem(parseProblem(error)),
      })
      return
    }

    update.mutate(
      { id: article.id, command: { ...fields, id: article.id, coverImageKey: coverKey, rowVersion: article.rowVersion } },
      {
        onSuccess: () => showToast({ kind: 'success', title: t('form.saved') }),
        onError: (error) => setProblem(parseProblem(error)),
      },
    )
  }

  function changePublication(action: 'publish' | 'unpublish') {
    const mutation = action === 'publish' ? publish : unpublish
    mutation.mutate(article!.id, {
      onSuccess: () => {
        setConfirm(null)
        showToast({ kind: 'success', title: t(action === 'publish' ? 'form.published' : 'form.unpublished') })
      },
      onError: (error) => {
        setConfirm(null)
        setProblem(parseProblem(error))
      },
    })
  }

  const generalError = problem && Object.keys(problem.fieldErrors).length === 0 && !isConcurrencyConflict(problem)
  const isPublished = article?.status === 'Published'

  return (
    <form
      className="flex max-w-3xl flex-col gap-6"
      onSubmit={(event) => {
        event.preventDefault()
        save()
      }}
    >
      {isConcurrencyConflict(problem) && (
        <Alert
          tone="error"
          message={t('form.conflict')}
          action={
            <Button type="button" variant="outline" size="sm" onClick={onReload}>
              {t('form.reload')}
            </Button>
          }
        />
      )}
      {generalError && <Alert tone="error" message={problem?.title ?? tCommon('errors.generic')} />}

      <FormField label={t('form.title')} required error={fieldError(problem, 'title')}>
        <Input value={title} maxLength={200} onChange={(event) => setTitle(event.target.value)} />
      </FormField>

      {article && (
        <p className="-mt-3 text-xs text-muted-foreground">
          <strong>{t('form.address', { slug: article.slug })}</strong> {t('form.addressHint')}
        </p>
      )}

      <FormField label={t('form.category')} error={fieldError(problem, 'category')}>
        <Select value={category} onChange={(event) => setCategory(event.target.value as NonNullable<NewsCategory>)}>
          {CATEGORIES.map((value) => (
            <option key={value} value={value}>
              {t(`categories.${value}`)}
            </option>
          ))}
        </Select>
      </FormField>

      <div className="flex flex-col gap-1.5">
        <span className="text-sm font-medium">
          {t('form.body')}
          <span aria-hidden="true" className="text-destructive"> *</span>
        </span>
        <RichTextEditor value={body} onChange={setBody} label={t('form.bodyLabel')} invalid={fieldError(problem, 'body') !== undefined} />
        {fieldError(problem, 'body') && (
          <p role="alert" className="text-xs text-destructive">
            {fieldError(problem, 'body')}
          </p>
        )}
      </div>

      <FormField label={t('form.metaDescription')} hint={t('form.metaHint', { count: meta.length })} error={fieldError(problem, 'metaDescription')}>
        <Textarea value={meta} rows={2} maxLength={META_MAX} onChange={(event) => setMeta(event.target.value)} />
      </FormField>

      <CoverImageField
        module="news"
        entityId={article?.id}
        imageUrl={coverUrl}
        onUploaded={(key) => setCoverKey(key)}
        onRemove={() => {
          setCoverKey(null)
          setCoverUrl(null)
        }}
      />

      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={saving || !dirty}>
          {saving ? tCommon('actions.saving') : article ? t('form.save') : t('form.saveDraft')}
        </Button>
        {article && !isPublished && (
          <Button type="button" variant="outline" disabled={dirty || publish.isPending} onClick={() => setConfirm('publish')}>
            {t('form.publish')}
          </Button>
        )}
        {article && isPublished && (
          <Button type="button" variant="outline" disabled={unpublish.isPending} onClick={() => setConfirm('unpublish')}>
            {t('form.unpublish')}
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={confirm === 'publish'}
        onOpenChange={(open) => !open && setConfirm(null)}
        title={t('form.publishTitle')}
        description={t('form.publishBody', { slug: article?.slug })}
        confirmLabel={t('form.publish')}
        pending={publish.isPending}
        onConfirm={() => changePublication('publish')}
      />
      <ConfirmDialog
        open={confirm === 'unpublish'}
        onOpenChange={(open) => !open && setConfirm(null)}
        title={t('form.unpublishTitle')}
        description={t('form.unpublishBody')}
        confirmLabel={t('form.unpublish')}
        pending={unpublish.isPending}
        onConfirm={() => changePublication('unpublish')}
      />
    </form>
  )
}
