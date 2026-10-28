import { cleanup, render } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useDocumentMeta } from './useDocumentMeta'

function Page({ title, description, image }: { title: string; description?: string; image?: string | null }) {
  useDocumentMeta({ title, description, image })
  return null
}

const meta = (selector: string) => document.head.querySelector<HTMLMetaElement>(selector)?.content

beforeEach(() => {
  document.title = 'Before'
  document.head.querySelectorAll('meta').forEach((element) => element.remove())
})

describe('useDocumentMeta', () => {
  it('sets the title and the meta tags while the page is shown', () => {
    render(<Page title="Open house" description="Come and see." image="http://x/cover.png" />)

    expect(document.title).toBe('Open house · SchoMans')
    expect(meta('meta[name="description"]')).toBe('Come and see.')
    expect(meta('meta[property="og:title"]')).toBe('Open house · SchoMans')
    expect(meta('meta[property="og:image"]')).toBe('http://x/cover.png')
  })

  it('shows only the product name when there is no title yet', () => {
    render(<Page title="" />)

    expect(document.title).toBe('SchoMans')
  })

  it('leaves out the tags it has no content for', () => {
    render(<Page title="Open house" />)

    expect(meta('meta[name="description"]')).toBeUndefined()
    expect(meta('meta[property="og:image"]')).toBeUndefined()
  })

  it('puts the previous title back and removes its tags when the page goes away', () => {
    render(<Page title="Open house" description="Come and see." />)

    cleanup()

    expect(document.title).toBe('Before')
    expect(meta('meta[property="og:title"]')).toBeUndefined()
    expect(meta('meta[name="description"]')).toBeUndefined()
  })

  it('updates the tags when the article changes', () => {
    const { rerender } = render(<Page title="First" description="One" />)

    rerender(<Page title="Second" description="Two" />)

    expect(document.title).toBe('Second · SchoMans')
    expect(meta('meta[name="description"]')).toBe('Two')
  })
})
