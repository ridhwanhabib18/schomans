import { Skeleton } from '@/shared/ui/skeleton'

export function DashboardSkeleton() {
  return (
    <div role="status" aria-busy="true" className="flex flex-col gap-4">
      <Skeleton className="h-9 w-64" />
      <Skeleton className="h-5 w-48" />
      <Skeleton className="h-40 w-full" />
    </div>
  )
}
