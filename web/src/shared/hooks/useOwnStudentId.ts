import { useQuery } from '@tanstack/react-query'
import { getMyAttendance } from '@/api/generated/schomans'
import { currentMonth } from '@/shared/lib/time'

export const ownStudentIdKey = ['me', 'own-student-id'] as const

/**
 * The student record id of the signed-in student. The API has no endpoint for it, but the attendance of the student
 * names it, so that answer is used. Shared by the student pages of several features.
 */
export function useOwnStudentId(enabled = true) {
  return useQuery({
    queryKey: ownStudentIdKey,
    queryFn: async () => (await getMyAttendance({ Month: currentMonth() })).studentId,
    enabled,
    staleTime: 30 * 60_000,
  })
}
