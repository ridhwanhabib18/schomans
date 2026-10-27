import { beforeEach, describe, expect, it } from 'vitest'
import { useToastStore } from './useToastStore'

beforeEach(() => useToastStore.setState({ toasts: [] }))

describe('useToastStore', () => {
  it('adds a toast and gives every toast its own id', () => {
    const first = useToastStore.getState().show({ kind: 'success', title: 'Saved' })
    const second = useToastStore.getState().show({ kind: 'info', title: 'New announcement', linkUrl: '/announcements/1' })

    expect(first).not.toBe(second)
    expect(useToastStore.getState().toasts.map((toast) => toast.title)).toEqual(['Saved', 'New announcement'])
  })

  it('keeps only the newest four so a burst cannot cover the page', () => {
    for (let index = 1; index <= 6; index++) {
      useToastStore.getState().show({ kind: 'info', title: `Toast ${index}` })
    }

    expect(useToastStore.getState().toasts.map((toast) => toast.title)).toEqual(['Toast 3', 'Toast 4', 'Toast 5', 'Toast 6'])
  })

  it('dismisses one toast and leaves the others', () => {
    const keep = useToastStore.getState().show({ kind: 'info', title: 'Keep' })
    const drop = useToastStore.getState().show({ kind: 'error', title: 'Drop' })

    useToastStore.getState().dismiss(drop)

    expect(useToastStore.getState().toasts.map((toast) => toast.id)).toEqual([keep])
  })
})
