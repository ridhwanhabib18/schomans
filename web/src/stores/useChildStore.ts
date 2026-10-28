import { create } from 'zustand'

interface ChildState {
  /** The child a parent is looking at on the attendance and assignment pages. Browser state only. */
  childId: string | null
  setChildId: (childId: string | null) => void
}

export const useChildStore = create<ChildState>((set) => ({
  childId: null,
  setChildId: (childId) => set({ childId }),
}))

export const selectChildId = (state: ChildState) => state.childId
export const selectSetChildId = (state: ChildState) => state.setChildId
