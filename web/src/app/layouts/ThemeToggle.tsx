import { Monitor, Moon, Sun } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { selectSetTheme, selectTheme, useUiStore, type ThemePreference } from '@/stores/useUiStore'

const order: readonly ThemePreference[] = ['system', 'light', 'dark']
const icons = { system: Monitor, light: Sun, dark: Moon } as const

/** Cycles the theme preference: system, light, dark. */
export function ThemeToggle() {
  const { t } = useTranslation()
  const theme = useUiStore(selectTheme)
  const setTheme = useUiStore(selectSetTheme)
  const Icon = icons[theme]
  const next = order[(order.indexOf(theme) + 1) % order.length] ?? 'system'

  return (
    <Button variant="ghost" size="icon" aria-label={t('theme.label', { mode: t(`theme.${theme}`) })} onClick={() => setTheme(next)}>
      <Icon aria-hidden="true" />
    </Button>
  )
}
