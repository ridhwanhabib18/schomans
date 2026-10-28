import { useEffect, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useMyChildren } from '@/shared/hooks/useMyChildren'
import { selectChildId, selectSetChildId, useChildStore } from '@/stores/useChildStore'
import { Select } from './form-controls'
import { Skeleton } from './skeleton'

/**
 * The child a parent looks at. The choice is kept while the parent moves between pages; the first child is chosen
 * when there is no choice yet (or the chosen one is not linked any more). Returns the chosen id to its render prop.
 */
export function ChildPicker({ children }: { children: (childId: string) => ReactNode }) {
  const { t } = useTranslation()
  const { data, isPending, isError } = useMyChildren()
  const childId = useChildStore(selectChildId)
  const setChildId = useChildStore(selectSetChildId)
  const valid = data?.some((child) => child.studentId === childId) ?? false

  useEffect(() => {
    if (data && data.length > 0 && !valid) {
      setChildId(data[0]!.studentId)
    }
  }, [data, valid, setChildId])

  if (isPending) {
    return <Skeleton className="h-10 w-64" />
  }

  if (isError || data.length === 0) {
    return <p className="text-sm text-muted-foreground">{t('children.none')}</p>
  }

  return (
    <div className="flex flex-col gap-6">
      {data.length > 1 && (
        <label className="flex max-w-sm flex-col gap-1.5 text-sm font-medium">
          {t('children.label')}
          <Select value={childId ?? ''} onChange={(event) => setChildId(event.target.value)}>
            {data.map((child) => (
              <option key={child.studentId} value={child.studentId}>
                {t('children.option', { name: child.fullName, classroom: child.classroomName })}
              </option>
            ))}
          </Select>
        </label>
      )}
      {valid && childId && children(childId)}
    </div>
  )
}
