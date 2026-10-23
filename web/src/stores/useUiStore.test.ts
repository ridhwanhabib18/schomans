import { afterEach, describe, expect, it } from 'vitest'
import { isDark, useUiStore } from './useUiStore'

afterEach(() => {
  localStorage.clear()
  useUiStore.setState({ theme: 'system' })
})

describe('useUiStore', () => {
  it('stores the theme preference and remembers it', () => {
    useUiStore.getState().setTheme('dark')

    expect(useUiStore.getState().theme).toBe('dark')
    expect(localStorage.getItem('schomans.theme')).toBe('dark')
  })

  it('still works when browser storage is unavailable', () => {
    const original = Storage.prototype.setItem
    Storage.prototype.setItem = () => {
      throw new Error('blocked')
    }
    try {
      useUiStore.getState().setTheme('light')
      expect(useUiStore.getState().theme).toBe('light')
    } finally {
      Storage.prototype.setItem = original
    }
  })
})

describe('isDark', () => {
  it('follows the system only for the system preference', () => {
    expect(isDark('dark', false)).toBe(true)
    expect(isDark('light', true)).toBe(false)
    expect(isDark('system', true)).toBe(true)
    expect(isDark('system', false)).toBe(false)
  })
})
