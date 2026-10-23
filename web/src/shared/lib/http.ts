import axios, { type AxiosError, type AxiosRequestConfig } from 'axios'
import { config } from '@/shared/lib/config'
import { useAuthStore } from '@/stores/useAuthStore'

/** The one axios instance of the app. Components never use it directly; they go through TanStack Query hooks. */
export const http = axios.create({ baseURL: config.apiUrl })

http.interceptors.request.use((request) => {
  const token = useAuthStore.getState().accessToken
  if (token) {
    request.headers.set('Authorization', `Bearer ${token}`)
  }
  return request
})

http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    // The token was rejected (expired or revoked): drop the session so the route guards send the user to login.
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      useAuthStore.getState().clearSession()
    }
    return Promise.reject(error)
  },
)

/** Mutator of the orval-generated client: every generated function calls the API through this. */
export const customInstance = <T>(requestConfig: AxiosRequestConfig, options?: AxiosRequestConfig): Promise<T> =>
  http.request<T>({ ...requestConfig, ...options }).then(({ data }) => data)

export type ErrorType<E> = AxiosError<E>
