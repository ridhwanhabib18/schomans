import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import type { ApplicantForm } from '../api/admissions'
import '../i18n'

interface ApplicationFormProps {
  value: ApplicantForm
  onChange: (value: ApplicantForm) => void
  /** False once the application is handed in: the form is then only read. */
  editable: boolean
  dirty: boolean
  saving: boolean
  onSave: () => void
}

/** The registration form. It can be saved as a draft with empty fields; handing the application in needs all of them. */
export function ApplicationForm({ value, onChange, editable, dirty, saving, onSave }: ApplicationFormProps) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const set = <K extends keyof ApplicantForm>(key: K, next: ApplicantForm[K]) => onChange({ ...value, [key]: next })

  return (
    <form
      noValidate
      className="flex min-w-0 flex-col gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        onSave()
      }}
    >
      <div className="grid min-w-0 gap-4 sm:grid-cols-2">
        <FormField label={t('form.fullName')} required className="sm:col-span-2">
          <Input value={value.fullName} maxLength={200} disabled={!editable} onChange={(event) => set('fullName', event.target.value)} />
        </FormField>
        <FormField label={t('form.dateOfBirth')} required>
          <Input type="date" value={value.dateOfBirth ?? ''} disabled={!editable} onChange={(event) => set('dateOfBirth', event.target.value || null)} />
        </FormField>
        <FormField label={t('form.placeOfBirth')} required>
          <Input value={value.placeOfBirth} maxLength={100} disabled={!editable} onChange={(event) => set('placeOfBirth', event.target.value)} />
        </FormField>
        <FormField label={t('form.address')} required className="sm:col-span-2">
          <Input value={value.address} maxLength={300} disabled={!editable} onChange={(event) => set('address', event.target.value)} />
        </FormField>
        <FormField label={t('form.guardianName')} required>
          <Input value={value.guardianName} maxLength={200} disabled={!editable} onChange={(event) => set('guardianName', event.target.value)} />
        </FormField>
        <FormField label={t('form.guardianPhone')} required>
          <Input type="tel" value={value.guardianPhone} maxLength={30} disabled={!editable} onChange={(event) => set('guardianPhone', event.target.value)} />
        </FormField>
        <FormField label={t('form.previousSchool')} required className="sm:col-span-2">
          <Input value={value.previousSchool} maxLength={200} disabled={!editable} onChange={(event) => set('previousSchool', event.target.value)} />
        </FormField>
      </div>

      {editable ? (
        <div>
          <Button type="submit" disabled={saving || !dirty}>
            {saving ? tCommon('actions.saving') : t('form.save')}
          </Button>
        </div>
      ) : (
        <p className="text-sm text-muted-foreground">{t('form.readOnly')}</p>
      )}
    </form>
  )
}
