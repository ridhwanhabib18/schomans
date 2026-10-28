import { ArrowLeft, Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { formatDateTime } from '@/shared/lib/format'
import { parseProblem } from '@/shared/lib/problem'
import { Button, buttonVariants } from '@/shared/ui/button'
import { ConfirmDialog } from '@/shared/ui/confirm-dialog'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAssignment, useDeleteAssignment } from '../api/assignments'
import { AttachmentList } from '../components/AttachmentList'
import { ReviewSection } from '../components/ReviewSection'
import { SubmissionPanel, SubmissionSummary } from '../components/SubmissionPanel'
import '../i18n'

/**
 * One assignment for everybody who may see it. The teacher who gave it reviews and scores the work; the principal
 * looks on; a student hands in their own work; a parent sees the work of the child in `?child=`.
 */
export function AssignmentDetailPage() {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const { id = '' } = useParams()
  const [params] = useSearchParams()
  const childId = params.get('child') ?? undefined
  const role = useAuthStore(selectAuthRole)
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const { data, isPending, isError, refetch } = useAssignment(id, role === 'Parent' ? childId : undefined)
  const remove = useDeleteAssignment()
  const [confirmDelete, setConfirmDelete] = useState(false)

  const back = role === 'Parent' ? '/my-children/assignments' : '/assignments'

  return (
    <div className="flex flex-col gap-6">
      <Link to={back} className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}>
        <ArrowLeft aria-hidden="true" />
        {t('form.back')}
      </Link>

      {isPending ? (
        <div role="status" aria-busy="true" className="flex flex-col gap-3">
          <Skeleton className="h-10 w-2/3" />
          <Skeleton className="h-40" />
        </div>
      ) : isError ? (
        <ErrorNotice message={t('detail.error')} onRetry={() => void refetch()} />
      ) : (
        <>
          <PageHeader
            title={data.title}
            actions={
              data.canEdit && (
                <div className="flex gap-2">
                  <Link to={`/assignments/${data.id}/edit`} className={buttonVariants({ variant: 'outline' })}>
                    <Pencil aria-hidden="true" />
                    {t('detail.edit')}
                  </Link>
                  <Button variant="outline" className="text-destructive" onClick={() => setConfirmDelete(true)}>
                    <Trash2 aria-hidden="true" />
                    {t('detail.delete')}
                  </Button>
                </div>
              )
            }
          />

          <dl className="grid gap-3 rounded-xl border border-border bg-card p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
            <div>
              <dt className="text-muted-foreground">{t('detail.subject')}</dt>
              <dd className="font-medium">{data.subjectName}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">{t('detail.teacher')}</dt>
              <dd className="font-medium">{data.teacherName}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">{t('detail.due')}</dt>
              <dd className="font-medium">{formatDateTime(data.dueAt)}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">{t('detail.classrooms')}</dt>
              <dd className="font-medium">{data.classrooms.map((c) => c.name).join(', ')}</dd>
            </div>
          </dl>

          <section aria-labelledby="instructions" className="flex flex-col gap-2">
            <h2 id="instructions" className="text-lg font-semibold">
              {t('detail.instructions')}
            </h2>
            <p className="whitespace-pre-wrap">{data.description || t('detail.noInstructions')}</p>
          </section>

          {data.attachments.length > 0 && (
            <section aria-labelledby="attachments" className="flex flex-col gap-2">
              <h2 id="attachments" className="text-lg font-semibold">
                {t('detail.attachments')}
              </h2>
              <AttachmentList assignmentId={data.id} attachments={data.attachments} studentId={role === 'Parent' ? childId : undefined} />
            </section>
          )}

          {role === 'Student' && <SubmissionPanel key={`${data.id}-${data.mySubmission?.submittedAt ?? ''}`} assignment={data} />}

          {role === 'Parent' && (
            <section aria-labelledby="child-work" className="flex flex-col gap-3 rounded-xl border border-border bg-card p-4">
              <h2 id="child-work" className="text-lg font-semibold">
                {t('submit.titleChild')}
              </h2>
              {data.mySubmission ? <SubmissionSummary submission={data.mySubmission} /> : <p className="text-sm text-muted-foreground">{t('submit.notYet')}</p>}
            </section>
          )}

          {(role === 'Teacher' || role === 'Principal') && <ReviewSection assignmentId={data.id} canScore={data.canEdit} />}

          <ConfirmDialog
            open={confirmDelete}
            onOpenChange={setConfirmDelete}
            title={t('detail.deleteTitle')}
            description={t('detail.deleteBody', { title: data.title })}
            confirmLabel={t('detail.delete')}
            destructive
            pending={remove.isPending}
            onConfirm={() =>
              remove.mutateAsync(data.id).then(
                () => {
                  setConfirmDelete(false)
                  showToast({ kind: 'success', title: t('detail.deleted') })
                  void navigate('/assignments')
                },
                (error: unknown) => {
                  setConfirmDelete(false)
                  const problem = parseProblem(error)
                  showToast({ kind: 'error', title: t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
                },
              )
            }
          />
        </>
      )}
    </div>
  )
}
