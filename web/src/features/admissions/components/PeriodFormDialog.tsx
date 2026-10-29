import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { isoToWibLocal, wibLocalToIso } from '@/shared/lib/time'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Input, Select } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useAcademicYearOptions, useCreatePeriod, useUpdatePeriod, type AdmissionPeriod } from '../api/admissions'
import { admissionErrorText } from '../errors'
import '../i18n'

/** A new period, or the dates of an existing one (its academic year stays). Rendered only while open, so the fields start fresh. */
export function PeriodFormDialog({ period, onClose }: { period?: AdmissionPeriod; onClose: () => void }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const years = useAcademicYearOptions()
  const create = useCreatePeriod()
  const update = useUpdatePeriod()
  const [name, setName] = useState(period?.name ?? '')
  const [academicYearId, setAcademicYearId] = useState(period?.academicYearId ?? '')
  const [opensAt, setOpensAt] = useState(period ? isoToWibLocal(period.opensAt) : '')
  const [closesAt, setClosesAt] = useState(period ? isoToWibLocal(period.closesAt) : '')
  const [resultsAt, setResultsAt] = useState(period ? isoToWibLocal(period.resultsAnnouncedAt) : '')
  const [deadline, setDeadline] = useState(period ? isoToWibLocal(period.reregistrationDeadline) : '')
  const [error, setError] = useState<string>()

  const dates = [opensAt, closesAt, resultsAt, deadline]
  const complete = name.trim() !== '' && dates.every((value) => value !== '') && (period !== undefined || academicYearId !== '')
  // Values of one format ("2026-10-30T07:00") sort like the moments they stand for.
  const inOrder = dates.every((value, index) => index === 0 || (dates[index - 1] ?? '') < value)
  const pending = create.isPending || update.isPending

  function submit(event: FormEvent) {
    event.preventDefault()
    setError(undefined)
    if (!complete) {
      return
    }

    if (!inOrder) {
      setError(t('periods.form.order'))
      return
    }

    const values = {
      name: name.trim(),
      opensAt: wibLocalToIso(opensAt),
      closesAt: wibLocalToIso(closesAt),
      resultsAnnouncedAt: wibLocalToIso(resultsAt),
      reregistrationDeadline: wibLocalToIso(deadline),
    }
    const options = {
      onSuccess: () => {
        showToast({ kind: 'success', title: t('periods.saved') })
        onClose()
      },
      onError: (e: unknown) => setError(admissionErrorText(e, t, tCommon('errors.generic'))),
    }

    if (period) {
      update.mutate({ id: period.id, input: values }, options)
    } else {
      create.mutate({ ...values, academicYearId }, options)
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{period ? t('periods.form.editTitle') : t('periods.form.newTitle')}</DialogTitle>
          <DialogDescription>{t('periods.form.hint')}</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          {error && <Alert tone="error" message={error} />}
          <FormField label={t('periods.form.name')} required>
            <Input value={name} maxLength={100} onChange={(event) => setName(event.target.value)} />
          </FormField>
          {!period && (
            <FormField label={t('periods.form.year')} required>
              <Select value={academicYearId} onChange={(event) => setAcademicYearId(event.target.value)}>
                <option value="">{t('periods.form.chooseYear')}</option>
                {(years.data ?? []).map((year) => (
                  <option key={year.id} value={year.id}>
                    {year.name}
                  </option>
                ))}
              </Select>
            </FormField>
          )}
          <div className="grid min-w-0 gap-4 sm:grid-cols-2">
            <FormField label={t('periods.form.opensAt')} required>
              <Input type="datetime-local" value={opensAt} onChange={(event) => setOpensAt(event.target.value)} />
            </FormField>
            <FormField label={t('periods.form.closesAt')} required>
              <Input type="datetime-local" value={closesAt} onChange={(event) => setClosesAt(event.target.value)} />
            </FormField>
            <FormField label={t('periods.form.resultsAt')} required>
              <Input type="datetime-local" value={resultsAt} onChange={(event) => setResultsAt(event.target.value)} />
            </FormField>
            <FormField label={t('periods.form.deadline')} required>
              <Input type="datetime-local" value={deadline} onChange={(event) => setDeadline(event.target.value)} />
            </FormField>
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" disabled={pending || !complete}>
              {pending ? tCommon('actions.saving') : t('periods.form.save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
