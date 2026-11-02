import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { Select } from '@/shared/ui/form-controls'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useApprovalChains, useSetApprovalChain } from '../api/requests'
import { APPROVER_ROLES, type ApprovalChain, type ApproverRole, type RequestType } from '../api/types'
import { requestErrorText } from '../errors'
import '../i18n'

const MAX_STEPS = 5

interface Step {
  key: number
  role: ApproverRole
}

/** The roles a step of this kind of request may have: a teacher's leave is not about a student, so it has no homeroom teacher. */
const rolesFor = (type: RequestType): readonly ApproverRole[] => (type === 'TeacherLeave' ? APPROVER_ROLES.filter((r) => r !== 'HomeroomTeacher') : APPROVER_ROLES)

/** `locked` means requests of this kind wait for a decision: the chain is shown, and the admin staff are told why it cannot change now. */
function ChainEditor({ chain, editable, locked }: { chain: ApprovalChain; editable: boolean; locked: boolean }) {
  const { t } = useTranslation('requests')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const save = useSetApprovalChain()
  const [nextKey, setNextKey] = useState(chain.roles.length)
  const [steps, setSteps] = useState<Step[]>(() => chain.roles.map((role, index) => ({ key: index, role })))
  const [error, setError] = useState<string>()
  const allowed = rolesFor(chain.type)
  const typeLabel = t(`type.${chain.type}`)

  const problem = steps.length === 0 ? 'none' : steps.length > MAX_STEPS ? 'tooMany' : steps.some((s) => !allowed.includes(s.role)) ? 'noHomeroom' : null

  const move = (index: number, by: -1 | 1) =>
    setSteps((current) => {
      const next = [...current]
      const [moved] = next.splice(index, 1)
      next.splice(index + by, 0, moved!)
      return next
    })

  function submit() {
    setError(undefined)
    save.mutate(
      { type: chain.type, roles: steps.map((s) => s.role) },
      {
        onSuccess: () => showToast({ kind: 'success', title: t('chains.saved') }),
        onError: (e) => setError(requestErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <Card className="min-w-0">
      <CardHeader>
        <CardTitle>{typeLabel}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {locked && <Alert tone="info" message={t('chains.pending')} />}
        {error && <Alert tone="error" message={error} />}
        <ol aria-label={t('chains.listLabel', { type: typeLabel })} className="flex flex-col gap-3">
          {steps.map((step, index) => (
            <li key={step.key} className="flex min-w-0 flex-wrap items-end gap-3">
              <label className="flex min-w-0 flex-1 flex-col gap-1.5 text-sm font-medium">
                {t('chains.role', { step: index + 1 })}
                <Select
                  value={step.role}
                  disabled={!editable}
                  onChange={(event) => setSteps((current) => current.map((s) => (s.key === step.key ? { ...s, role: event.target.value as ApproverRole } : s)))}
                >
                  {APPROVER_ROLES.map((role) => (
                    <option key={role} value={role} disabled={!allowed.includes(role)}>
                      {t(`role.${role}`)}
                    </option>
                  ))}
                </Select>
              </label>
              {editable && (
                <div className="flex gap-1">
                  <Button type="button" variant="ghost" size="icon" disabled={index === 0} onClick={() => move(index, -1)}>
                    <ArrowUp aria-hidden="true" />
                    <span className="sr-only">{t('chains.up', { step: index + 1 })}</span>
                  </Button>
                  <Button type="button" variant="ghost" size="icon" disabled={index === steps.length - 1} onClick={() => move(index, 1)}>
                    <ArrowDown aria-hidden="true" />
                    <span className="sr-only">{t('chains.down', { step: index + 1 })}</span>
                  </Button>
                  <Button type="button" variant="ghost" size="icon" disabled={steps.length === 1} onClick={() => setSteps((current) => current.filter((s) => s.key !== step.key))}>
                    <Trash2 aria-hidden="true" />
                    <span className="sr-only">{t('chains.remove', { step: index + 1 })}</span>
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ol>
        {editable && (
          <>
            {problem && <p className="text-sm text-destructive">{t(`chains.${problem}`)}</p>}
            <div className="flex flex-wrap gap-2">
              <Button
                type="button"
                variant="outline"
                disabled={steps.length >= MAX_STEPS}
                onClick={() => {
                  setSteps((current) => [...current, { key: nextKey, role: allowed[allowed.length - 1]! }])
                  setNextKey((key) => key + 1)
                }}
              >
                <Plus aria-hidden="true" />
                {t('chains.add')}
              </Button>
              <Button type="button" disabled={problem !== null || save.isPending} onClick={submit}>
                {save.isPending ? tCommon('actions.saving') : t('chains.save')}
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}

/** The admin staff set who decides, and in which order, for each kind of request; the principal can read the chains. */
export function ApprovalChainsPage() {
  const { t } = useTranslation('requests')
  const isAdmin = useAuthStore(selectAuthRole) === 'Admin'
  const { data, isPending, isError, refetch } = useApprovalChains()

  return (
    <div className="flex min-w-0 flex-col gap-6">
      <PageHeader title={t('chains.title')} description={isAdmin ? t('chains.description') : t('chains.readOnly')} />
      {isPending ? (
        <Skeleton className="h-64 max-w-xl" />
      ) : isError ? (
        <ErrorNotice message={t('chains.error')} onRetry={() => void refetch()} />
      ) : (
        <div className="grid max-w-3xl gap-6 [&>*]:min-w-0">
          {data.map((chain) => (
            // The key follows the saved roles, so a saved or changed chain starts the editor again from the server's answer.
            <ChainEditor key={`${chain.type}:${chain.roles.join(',')}:${chain.hasPendingRequests}`} chain={chain} editable={isAdmin && !chain.hasPendingRequests} locked={isAdmin && chain.hasPendingRequests} />
          ))}
        </div>
      )}
    </div>
  )
}
