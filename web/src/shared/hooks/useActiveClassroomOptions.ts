import { useQuery } from '@tanstack/react-query'
import { listAcademicYears, listClassrooms } from '@/api/generated/schomans'

/** The classrooms of the current academic year, for pickers on pages of several features. */
export function useActiveClassroomOptions(enabled = true) {
  return useQuery({
    queryKey: ['shared', 'active-classrooms'],
    queryFn: async () => {
      const years = await listAcademicYears({ Page: 1, PageSize: 100 })
      const current = years.items.find((year) => year.isActive)
      return current ? (await listClassrooms({ AcademicYearId: current.id, Page: 1, PageSize: 100, Sort: 'name' })).items : []
    },
    enabled,
    staleTime: 5 * 60_000,
  })
}
