import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createAnnouncement,
  deleteAnnouncement,
  getAnnouncement,
  getMyAnnouncement,
  getPublicAnnouncement,
  listAcademicYears,
  listAnnouncements,
  listClassrooms,
  listMyAnnouncements,
  listPublicAnnouncements,
  markAnnouncementRead,
  publishAnnouncement,
  updateAnnouncement,
} from '@/api/generated/schomans'
import type { CreateAnnouncementCommand, ListAnnouncementsParams, UpdateAnnouncementCommand } from '@/api/generated/model'

/** One factory for the query keys; the real-time bridge invalidates `all` when a notification arrives. */
export const announcementKeys = {
  all: ['announcements'] as const,
  mine: (page: number, pageSize: number, unreadOnly: boolean) => [...announcementKeys.all, 'mine', { page, pageSize, unreadOnly }] as const,
  mineDetail: (id: string) => [...announcementKeys.all, 'mine-detail', id] as const,
  manage: (params: ListAnnouncementsParams) => [...announcementKeys.all, 'manage', params] as const,
  manageDetail: (id: string) => [...announcementKeys.all, 'manage-detail', id] as const,
  publicList: (page: number, pageSize: number) => [...announcementKeys.all, 'public', { page, pageSize }] as const,
  publicDetail: (id: string) => [...announcementKeys.all, 'public-detail', id] as const,
  classroomOptions: () => [...announcementKeys.all, 'classroom-options'] as const,
}

// ---- readers ----

export function useMyAnnouncements(page: number, pageSize: number, unreadOnly: boolean) {
  return useQuery({
    queryKey: announcementKeys.mine(page, pageSize, unreadOnly),
    queryFn: () => listMyAnnouncements({ Page: page, PageSize: pageSize, UnreadOnly: unreadOnly }),
    placeholderData: keepPreviousData,
  })
}

export function useMyAnnouncement(id: string) {
  return useQuery({ queryKey: announcementKeys.mineDetail(id), queryFn: () => getMyAnnouncement(id) })
}

export function useMarkAnnouncementRead() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => markAnnouncementRead(id),
    // The read mark also clears a notification, so the lists and the badge must refresh.
    onSuccess: () => queryClient.invalidateQueries(),
  })
}

// ---- guests ----

export function usePublicAnnouncements(page = 1, pageSize = 5) {
  return useQuery({
    queryKey: announcementKeys.publicList(page, pageSize),
    queryFn: () => listPublicAnnouncements({ Page: page, PageSize: pageSize }),
  })
}

export function usePublicAnnouncement(id: string) {
  return useQuery({ queryKey: announcementKeys.publicDetail(id), queryFn: () => getPublicAnnouncement(id) })
}

// ---- editors ----

export function useManagedAnnouncements(params: ListAnnouncementsParams) {
  return useQuery({
    queryKey: announcementKeys.manage(params),
    queryFn: () => listAnnouncements(params),
    placeholderData: keepPreviousData,
  })
}

export function useManagedAnnouncement(id: string | undefined) {
  return useQuery({
    queryKey: announcementKeys.manageDetail(id ?? ''),
    queryFn: () => getAnnouncement(id!),
    enabled: id !== undefined,
  })
}

export function useCreateAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (command: CreateAnnouncementCommand) => createAnnouncement(command),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: announcementKeys.all }),
  })
}

export function useUpdateAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateAnnouncementCommand }) => updateAnnouncement(id, command),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: announcementKeys.all }),
  })
}

export function usePublishAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => publishAnnouncement(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: announcementKeys.all }),
  })
}

export function useDeleteAnnouncement() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteAnnouncement(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: announcementKeys.all }),
  })
}

/** The classrooms of the current academic year, to pick the audience of an announcement. */
export function useClassroomOptions() {
  return useQuery({
    queryKey: announcementKeys.classroomOptions(),
    queryFn: async () => {
      const years = await listAcademicYears({ Page: 1, PageSize: 100 })
      const current = years.items.find((year) => year.isActive)
      if (!current) {
        return []
      }

      const classrooms = await listClassrooms({ AcademicYearId: current.id, Page: 1, PageSize: 100, Sort: 'name' })
      return classrooms.items
    },
  })
}
