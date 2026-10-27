import { create } from 'zustand'

export type ToastKind = 'info' | 'success' | 'error'

export interface Toast {
  id: number
  kind: ToastKind
  title: string
  body?: string
  /** App path to open when the toast is clicked. */
  linkUrl?: string
}

interface ToastState {
  toasts: Toast[]
  show: (toast: Omit<Toast, 'id'>) => number
  dismiss: (id: number) => void
}

let nextId = 1

/** Short messages shown on top of the page. Browser state only: nothing here comes from the API. */
export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  show: (toast) => {
    const id = nextId++
    // Keep the newest few: a burst of notifications must not cover the page.
    set((state) => ({ toasts: [...state.toasts, { ...toast, id }].slice(-4) }))
    return id
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })),
}))

export const selectToasts = (state: ToastState) => state.toasts
export const selectShowToast = (state: ToastState) => state.show
export const selectDismissToast = (state: ToastState) => state.dismiss
