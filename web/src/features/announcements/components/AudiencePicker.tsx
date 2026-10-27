import { useTranslation } from 'react-i18next'
import type { AppRole } from '@/shared/lib/roles'
import { APP_ROLES } from '@/shared/lib/roles'
import { Checkbox } from '@/shared/ui/form-controls'
import { Skeleton } from '@/shared/ui/skeleton'
import { useClassroomOptions } from '../api/announcements'
import '../i18n'

interface AudiencePickerProps {
  roles: readonly AppRole[]
  classroomIds: readonly string[]
  isPublic: boolean
  onRolesChange: (roles: AppRole[]) => void
  onClassroomIdsChange: (ids: string[]) => void
  onPublicChange: (isPublic: boolean) => void
  /** Once an announcement is published its audience is fixed. */
  locked: boolean
}

function toggle<T>(list: readonly T[], value: T, on: boolean): T[] {
  return on ? [...list.filter((item) => item !== value), value] : list.filter((item) => item !== value)
}

/** Who gets the announcement: roles, classrooms, and whether guests may read it too. */
export function AudiencePicker({ roles, classroomIds, isPublic, onRolesChange, onClassroomIdsChange, onPublicChange, locked }: AudiencePickerProps) {
  const { t } = useTranslation('announcements')
  const { t: tCommon } = useTranslation()
  const classrooms = useClassroomOptions()

  return (
    <div className="flex flex-col gap-5">
      <fieldset className="flex flex-col gap-2" disabled={locked}>
        <legend className="text-sm font-medium">{t('form.rolesLegend')}</legend>
        <p className="text-xs text-muted-foreground">{t('form.rolesHint')}</p>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
          {APP_ROLES.map((role) => (
            <label key={role} className="flex items-center gap-2 text-sm">
              <Checkbox checked={roles.includes(role)} onChange={(event) => onRolesChange(toggle(roles, role, event.target.checked))} />
              {tCommon(`roles.${role}`)}
            </label>
          ))}
        </div>
      </fieldset>

      <fieldset className="flex flex-col gap-2" disabled={locked}>
        <legend className="text-sm font-medium">{t('form.classroomsLegend')}</legend>
        <p className="text-xs text-muted-foreground">{t('form.classroomsHint')}</p>
        {classrooms.isPending ? (
          <Skeleton className="h-16 w-full" />
        ) : classrooms.isError ? (
          <p role="alert" className="text-sm text-destructive">
            {t('form.classroomsLoadError')}
          </p>
        ) : classrooms.data.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t('form.classroomsEmpty')}</p>
        ) : (
          <div className="grid max-h-48 grid-cols-2 gap-2 overflow-y-auto rounded-md border border-border p-3 sm:grid-cols-3">
            {classrooms.data.map((classroom) => (
              <label key={classroom.id} className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={classroomIds.includes(classroom.id)}
                  onChange={(event) => onClassroomIdsChange(toggle(classroomIds, classroom.id, event.target.checked))}
                />
                {classroom.name}
              </label>
            ))}
          </div>
        )}
      </fieldset>

      <div className="flex flex-col gap-1">
        <label className="flex items-center gap-2 text-sm font-medium">
          <Checkbox checked={isPublic} disabled={locked} onChange={(event) => onPublicChange(event.target.checked)} />
          {t('form.public')}
        </label>
        <p className="text-xs text-muted-foreground">{t('form.publicHint')}</p>
      </div>
    </div>
  )
}
