import { create } from 'zustand'

export type ThemePreference = 'light' | 'dark' | 'system'

const STORAGE_KEY = 'schomans.theme'

function readStoredTheme(): ThemePreference {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return stored === 'light' || stored === 'dark' || stored === 'system' ? stored : 'system'
  } catch {
    // Storage can be unavailable (private mode, blocked site data); the app still works.
    return 'system'
  }
}

interface UiState {
  theme: ThemePreference
  setTheme: (theme: ThemePreference) => void
}

export const useUiStore = create<UiState>((set) => ({
  theme: readStoredTheme(),
  setTheme: (theme) => {
    try {
      localStorage.setItem(STORAGE_KEY, theme)
    } catch {
      // Not persisting the preference is fine.
    }
    set({ theme })
  },
}))

export const selectTheme = (state: UiState) => state.theme
export const selectSetTheme = (state: UiState) => state.setTheme

/** True when the given preference should render dark right now. */
export function isDark(theme: ThemePreference, systemPrefersDark: boolean): boolean {
  return theme === 'dark' || (theme === 'system' && systemPrefersDark)
}
