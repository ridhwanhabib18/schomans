import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  createNews,
  deleteNews,
  getNews,
  getPublicNews,
  listNews,
  listPublicNews,
  publishNews,
  unpublishNews,
  updateNews,
} from '@/api/generated/schomans'
import type { CreateNewsCommand, ListNewsParams, NewsCategory, UpdateNewsCommand } from '@/api/generated/model'

export const newsKeys = {
  all: ['news'] as const,
  publicList: (page: number, pageSize: number, category: NewsCategory | undefined) => [...newsKeys.all, 'public', { page, pageSize, category }] as const,
  publicArticle: (slug: string) => [...newsKeys.all, 'public-article', slug] as const,
  manage: (params: ListNewsParams) => [...newsKeys.all, 'manage', params] as const,
  manageDetail: (id: string) => [...newsKeys.all, 'manage-detail', id] as const,
}

// ---- the public site ----

export function usePublicNewsList(page: number, pageSize: number, category?: NewsCategory) {
  return useQuery({
    queryKey: newsKeys.publicList(page, pageSize, category),
    queryFn: () => listPublicNews({ Page: page, PageSize: pageSize, Category: category }),
    placeholderData: keepPreviousData,
  })
}

export function usePublicNews(slug: string) {
  return useQuery({ queryKey: newsKeys.publicArticle(slug), queryFn: () => getPublicNews(slug) })
}

// ---- the editors ----

export function useManagedNewsList(params: ListNewsParams) {
  return useQuery({ queryKey: newsKeys.manage(params), queryFn: () => listNews(params), placeholderData: keepPreviousData })
}

export function useManagedNews(id: string | undefined) {
  return useQuery({ queryKey: newsKeys.manageDetail(id ?? ''), queryFn: () => getNews(id!), enabled: id !== undefined })
}

export function useCreateNews() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (command: CreateNewsCommand) => createNews(command),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: newsKeys.all }),
  })
}

export function useUpdateNews() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateNewsCommand }) => updateNews(id, command),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: newsKeys.all }),
  })
}

export function usePublishNews() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => publishNews(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: newsKeys.all }),
  })
}

export function useUnpublishNews() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => unpublishNews(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: newsKeys.all }),
  })
}

export function useDeleteNews() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => deleteNews(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: newsKeys.all }),
  })
}
