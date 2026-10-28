import { useQuery } from '@tanstack/react-query'
import { listAcademicYears } from '@/api/generated/schomans'

export interface TermOption {
  id: string
  /** The term with its academic year, for example "2026/2027 - Term 1". */
  label: string
  isActive: boolean
}

/** The terms of the academic years, newest year first, for pickers on pages of several features. */
export function useTermOptions(enabled = true) {
  return useQuery({
    queryKey: ['shared', 'term-options'],
    queryFn: async (): Promise<TermOption[]> => {
      const years = await listAcademicYears({ Page: 1, PageSize: 100 })
      return [...years.items]
        .sort((a, b) => b.startDate.localeCompare(a.startDate))
        .flatMap((year) =>
          [...year.terms]
            .sort((a, b) => a.startDate.localeCompare(b.startDate))
            .map((term) => ({ id: term.id, label: `${year.name} - ${term.name}`, isActive: term.isActive })),
        )
    },
    enabled,
    staleTime: 5 * 60_000,
  })
}
