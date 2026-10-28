import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { AnnouncementDto } from '@/api/generated/model'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { CoverImageField } from '@/shared/ui/cover-image-field'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'
import { fieldError, isConcurrencyConflict, parseProblem, type Problem } from '@/shared/lib/problem'
import { isAppRole, type AppRole } from '@/shared/lib/roles'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateAnnouncement, usePublishAnnouncement, useUpdateAnnouncement } from '../api/announcements'
import { AudienceText } from './AudienceText'
import { AudiencePicker } from './AudiencePicker'
import '../i18n'

interface AnnouncementFormProps {
  /** The saved announcement, or undefined for a new one. */
  announcement: AnnouncementDto | undefined
  onReload: () => void
}

/** Create or edit an announcement. A published one keeps its audience fixed (the notifications are out) but its text can be corrected. */
export function AnnouncementForm({ announcement, onReload }: AnnouncementFormProps) {
  const { t } = useTranslation('announcements')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateAnnouncement()
  const update = useUpdateAnnouncement()
  const publish = usePublishAnnouncement()

  const initialRoles = announcement?.targetRoles.filter(isAppRole) ?? []
  const [title, setTitle] = useState(announcement?.title ?? '')
  const [body, setBody] = useState(announcement?.body ?? '')
  const [isPublic, setIsPublic] = useState(announcement?.isPublic ?? false)
  const [roles, setRoles] = useState<AppRole[]>(initialRoles)
  const [classroomIds, setClassroomIds] = useState<string[]>(announcement?.targetClassroomIds ?? [])
  const [coverKey, setCoverKey] = useState<string | null>(announcement?.coverImageKey ?? null)
  const [coverUrl, setCoverUrl] = useState<string | null>(announcement?.coverImageUrl ?? null)
  const [problem, setProblem] = useState<Problem>()
  const [confirmPublish, setConfirmPublish] = useState(false)

  const locked = announcement?.status === 'Published'
  const saving = create.isPending || update.isPending
  const dirty =
    !announcement ||
    title !== announcement.title ||
    body !== announcement.body ||
    isPublic !== announcement.isPublic ||
    coverKey !== (announcement.coverImageKey ?? null) ||
    roles.slice().sort().join() !== initialRoles.slice().sort().join() ||
    classroomIds.slice().sort().join() !== announcement.targetClassroomIds.slice().sort().join()

  function save() {
    setProblem(undefined)
    const fields = { title, body, isPublic, targetRoles: roles, targetClassroomIds: classroomIds }

    if (!announcement) {
      // mutateAsync, not mutate with callbacks: saving changes the row version, which remounts this form, and callbacks
      // passed to mutate are dropped once the component that called it is gone.
      create.mutateAsync(fields).then(
        (created) => {
          showToast({ kind: 'success', title: t('form.saved') })
          void navigate(`/manage/announcements/${created.id}`, { replace: true })
        },
        (error: unknown) => setProblem(parseProblem(error)),
      )
      return
    }

    update.mutateAsync({ id: announcement.id, command: { ...fields, id: announcement.id, coverImageKey: coverKey, rowVersion: announcement.rowVersion } }).then(
      () => showToast({ kind: 'success', title: t('form.saved') }),
      (error: unknown) => setProblem(parseProblem(error)),
    )
  }

  const generalError = problem && Object.keys(problem.fieldErrors).length === 0 && !isConcurrencyConflict(problem)

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
      {locked && <Alert tone="info" message={t('form.locked')} />}

      <FormField label={t('form.title')} required error={fieldError(problem, 'title')}>
        <Input value={title} maxLength={200} onChange={(event) => setTitle(event.target.value)} />
      </FormField>

      <div className="flex flex-col gap-1.5">
        <span className="text-sm font-medium">{t('form.body')}</span>
        <RichTextEditor value={body} onChange={setBody} label={t('form.bodyLabel')} invalid={fieldError(problem, 'body') !== undefined} />
        {fieldError(problem, 'body') && (
          <p role="alert" className="text-xs text-destructive">
            {fieldError(problem, 'body')}
          </p>
        )}
      </div>

      <CoverImageField
        module="announcements"
        entityId={announcement?.id}
        imageUrl={coverUrl}
        onUploaded={(key) => setCoverKey(key)}
        onRemove={() => {
          setCoverKey(null)
          setCoverUrl(null)
        }}
      />

      <section aria-labelledby="audience-title" className="flex flex-col gap-3 rounded-lg border border-border p-4">
        <h2 id="audience-title" className="font-medium">
          {t('form.audienceTitle')}
        </h2>
        <AudiencePicker
          roles={roles}
          classroomIds={classroomIds}
          isPublic={isPublic}
          onRolesChange={setRoles}
          onClassroomIdsChange={setClassroomIds}
          onPublicChange={setIsPublic}
          locked={locked}
        />
        <p className="rounded-md bg-muted p-3 text-sm" aria-live="polite">
          {t('form.summary', { audience: '' })}
          <strong>
            <AudienceText roles={roles} classroomCount={classroomIds.length} />
          </strong>
        </p>
      </section>

      <div className="flex flex-wrap gap-3">
        <Button type="submit" disabled={saving || !dirty}>
          {saving ? tCommon('actions.saving') : announcement ? t('form.save') : t('form.saveDraft')}
        </Button>
        {announcement && !locked && (
          <Button type="button" variant="outline" disabled={dirty || publish.isPending} onClick={() => setConfirmPublish(true)}>
            {t('form.publish')}
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={confirmPublish}
        onOpenChange={setConfirmPublish}
        title={t('form.publishTitle')}
        description={
          <>
            {t('form.publishBody')} <strong><AudienceText roles={roles} classroomCount={classroomIds.length} /></strong>
          </>
        }
        confirmLabel={t('form.publish')}
        pending={publish.isPending}
        onConfirm={() =>
          publish.mutateAsync(announcement!.id).then(
            (result) => {
              setConfirmPublish(false)
              showToast({ kind: 'success', title: t('form.published', { count: result.notifiedUsers }) })
              void navigate('/manage/announcements')
            },
            (error: unknown) => {
              setConfirmPublish(false)
              setProblem(parseProblem(error))
            },
          )
        }
      />
    </form>
  )
}
