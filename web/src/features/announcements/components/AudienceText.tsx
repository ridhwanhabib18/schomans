import { useTranslation } from 'react-i18next'
import type { AppRole } from '@/shared/lib/roles'
import { describeAudience } from '../audience'
import '../i18n'

/** The audience as a sentence: "Teachers and Parents", "Students, parents and homeroom teachers of 2 classroom(s)". */
export function useAudienceText(roles: readonly AppRole[], classroomCount: number): string {
  const { t } = useTranslation('announcements')
  const { t: tCommon } = useTranslation()
  const description = describeAudience({ roles, classroomCount })
  const names = description.roles.map((role) => tCommon(`roles.${role}`))
  const roleList = new Intl.ListFormat('en', { style: 'long', type: 'conjunction' }).format(names)

  return t(`audience.${description.kind}`, { roles: roleList, count: classroomCount })
}

export function AudienceText({ roles, classroomCount }: { roles: readonly AppRole[]; classroomCount: number }) {
  return <>{useAudienceText(roles, classroomCount)}</>
}
