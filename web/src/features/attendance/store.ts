import { create } from 'zustand'

interface AttendanceSessionState {
  /** The QR code fills the screen for the classroom projector. Browser state only. */
  presenting: boolean
  setPresenting: (presenting: boolean) => void
}

export const useAttendanceSessionStore = create<AttendanceSessionState>((set) => ({
  presenting: false,
  setPresenting: (presenting) => set({ presenting }),
}))

export const selectPresenting = (state: AttendanceSessionState) => state.presenting
export const selectSetPresenting = (state: AttendanceSessionState) => state.setPresenting
