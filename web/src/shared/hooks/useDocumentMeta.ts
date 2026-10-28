import { useEffect } from 'react'

interface DocumentMeta {
  title: string
  description?: string
  /** Path of a page image (an absolute URL) for link previews. */
  image?: string | null
}

const SUFFIX = 'SchoMans'

function setMeta(attribute: 'name' | 'property', key: string, content: string | undefined) {
  const selector = `meta[${attribute}="${key}"]`
  let element = document.head.querySelector<HTMLMetaElement>(selector)
  if (content === undefined) {
    element?.remove()
    return
  }

  if (!element) {
    element = document.createElement('meta')
    element.setAttribute(attribute, key)
    document.head.appendChild(element)
  }

  element.setAttribute('content', content)
}

/**
 * Gives the page its own title and meta tags (description, Open Graph) while it is shown, and puts the previous ones
 * back afterwards. Search engines that run JavaScript read them; prerendering is not needed for that.
 */
export function useDocumentMeta({ title, description, image }: DocumentMeta) {
  useEffect(() => {
    const previousTitle = document.title
    const previousDescription = document.head.querySelector<HTMLMetaElement>('meta[name="description"]')?.content

    const fullTitle = title ? `${title} · ${SUFFIX}` : SUFFIX
    document.title = fullTitle
    setMeta('name', 'description', description)
    setMeta('property', 'og:title', fullTitle)
    setMeta('property', 'og:description', description)
    setMeta('property', 'og:type', 'article')
    setMeta('property', 'og:image', image ?? undefined)

    return () => {
      document.title = previousTitle
      setMeta('name', 'description', previousDescription)
      setMeta('property', 'og:title', undefined)
      setMeta('property', 'og:description', undefined)
      setMeta('property', 'og:type', undefined)
      setMeta('property', 'og:image', undefined)
    }
  }, [title, description, image])
}
