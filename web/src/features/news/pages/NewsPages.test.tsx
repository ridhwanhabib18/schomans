import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { NewsEditPage } from './NewsEditPage'
import { NewsManagePage } from './NewsManagePage'
import { PublicNewsArticlePage } from './PublicNewsArticlePage'
import { PublicNewsListPage } from './PublicNewsListPage'

const api = vi.hoisted(() => ({
  listPublicNews: vi.fn(),
  getPublicNews: vi.fn(),
  listNews: vi.fn(),
  getNews: vi.fn(),
  createNews: vi.fn(),
  updateNews: vi.fn(),
  deleteNews: vi.fn(),
  publishNews: vi.fn(),
  unpublishNews: vi.fn(),
  createUploadUrl: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

// ProseMirror is tested on its own; here the form only needs something that holds the HTML.
vi.mock('@/shared/ui/rich-text-editor', () => ({
  RichTextEditor: ({ value, onChange, label }: { value: string; onChange: (html: string) => void; label: string }) => (
    <textarea aria-label={label} value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}))

const article = (overrides: Record<string, unknown> = {}) => ({
  id: 'n1',
  title: 'Open house',
  slug: 'open-house',
  body: '<p>Come and see.</p>',
  coverImageKey: null,
  coverImageUrl: null,
  category: 'News',
  metaDescription: 'Visit us',
  status: 'Draft',
  publishedAt: null,
  createdAt: '2026-10-01T08:00:00Z',
  rowVersion: 'rv1',
  ...overrides,
})

const summary = (overrides: Record<string, unknown> = {}) => ({
  slug: 'open-house',
  title: 'Open house',
  excerpt: 'Come and see.',
  coverImageUrl: null,
  category: 'News',
  publishedAt: '2026-10-01T08:00:00Z',
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listPublicNews.mockResolvedValue(pageOf([summary(), summary({ slug: 'award', title: 'Science award', category: 'Award' })], { pageSize: 9 }))
  api.listNews.mockResolvedValue(pageOf([article(), article({ id: 'n2', title: 'Awards night', slug: 'awards-night', status: 'Published', publishedAt: '2026-10-05T08:00:00Z' })]))
  api.getNews.mockResolvedValue(article())
  api.createNews.mockResolvedValue(article({ id: 'n9' }))
  for (const name of ['updateNews', 'deleteNews', 'publishNews', 'unpublishNews'] as const) api[name].mockResolvedValue(undefined)
  useToastStore.setState({ toasts: [] })
  signInAs('Admin')
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
  vi.unstubAllGlobals()
  document.title = ''
})

describe('PublicNewsListPage', () => {
  const renderList = () => renderRoutes([{ path: '/news', element: <PublicNewsListPage /> }, { path: '/news/:slug', element: <p>Article page</p> }], '/news')

  it('shows the published articles and opens one', async () => {
    renderList()

    expect(await screen.findByText('Science award')).toBeTruthy()
    fireEvent.click(screen.getByRole('link', { name: 'Open house' }))

    expect(await screen.findByText('Article page')).toBeTruthy()
  })

  it('sets the page title for search engines', async () => {
    renderList()
    await screen.findByText('Science award')

    expect(document.title).toBe('School news · SchoMans')
  })

  it('filters by category and starts again from the first page', async () => {
    renderList()
    await screen.findByText('Science award')

    fireEvent.click(screen.getByRole('button', { name: 'Awards' }))

    await waitFor(() => expect(api.listPublicNews).toHaveBeenLastCalledWith({ Page: 1, PageSize: 9, Category: 'Award' }))
    expect(screen.getByRole('button', { name: 'Awards' }).getAttribute('aria-pressed')).toBe('true')
  })

  it('says so when a category has no news', async () => {
    api.listPublicNews.mockResolvedValue(pageOf([]))
    renderList()

    expect(await screen.findByText('There is no news in this category yet.')).toBeTruthy()
  })

  it('offers a retry when the news cannot be loaded', async () => {
    api.listPublicNews.mockRejectedValueOnce(apiError(500)).mockResolvedValue(pageOf([summary()]))
    renderList()

    expect(await screen.findByText('The news could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Come and see.')).toBeTruthy()
  })
})

describe('PublicNewsArticlePage', () => {
  const renderArticle = () => renderRoutes([{ path: '/news/:slug', element: <PublicNewsArticlePage /> }], '/news/open-house')

  it('shows the article and puts its title and description in the meta tags', async () => {
    api.getPublicNews.mockResolvedValue({
      slug: 'open-house',
      title: 'Open house',
      body: '<p>Come and <strong>see</strong>.</p>',
      coverImageUrl: 'http://storage/cover.png',
      category: 'News',
      metaDescription: 'Visit us on Saturday.',
      publishedAt: '2026-10-01T08:00:00Z',
    })
    renderArticle()

    expect(await screen.findByRole('heading', { level: 1, name: 'Open house' })).toBeTruthy()
    expect(screen.getByText('see').tagName).toBe('STRONG')
    expect(document.title).toBe('Open house · SchoMans')
    expect(document.head.querySelector<HTMLMetaElement>('meta[name="description"]')?.content).toBe('Visit us on Saturday.')
    expect(document.head.querySelector<HTMLMetaElement>('meta[property="og:image"]')?.content).toBe('http://storage/cover.png')
    expect(api.getPublicNews).toHaveBeenCalledWith('open-house')
  })

  it('says the article does not exist when the address is wrong', async () => {
    api.getPublicNews.mockRejectedValue(apiError(404))
    renderArticle()

    expect(await screen.findByText('This article does not exist')).toBeTruthy()
  })
})

describe('NewsManagePage', () => {
  const renderManage = () =>
    renderRoutes(
      [
        { path: '/manage/news', element: <NewsManagePage /> },
        { path: '/manage/news/new', element: <p>New article page</p> },
        { path: '/manage/news/:id', element: <p>Edit article page</p> },
      ],
      '/manage/news',
    )

  it('lists drafts and published articles, and links to the site only for the published ones', async () => {
    renderManage()

    expect(await screen.findByText('Open house')).toBeTruthy()
    expect(screen.getByText('Awards night')).toBeTruthy()
    expect(screen.getAllByRole('link', { name: /^View on the site/ })).toHaveLength(1)
    expect(screen.getByRole('link', { name: /^View on the site/ }).getAttribute('href')).toBe('/news/awards-night')
  })

  it('passes the filters to the API', async () => {
    renderManage()
    await screen.findByText('Open house')

    fireEvent.change(screen.getByRole('combobox', { name: 'Status' }), { target: { value: 'Draft' } })
    fireEvent.change(screen.getByRole('combobox', { name: 'Category' }), { target: { value: 'Award' } })
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'night' } })

    await waitFor(() => expect(api.listNews).toHaveBeenLastCalledWith(expect.objectContaining({ Status: 'Draft', Category: 'Award', Search: 'night', Page: 1 })))
  })

  it('leads to the new-article page', async () => {
    renderManage()

    fireEvent.click(await screen.findByRole('link', { name: 'New article' }))

    expect(await screen.findByText('New article page')).toBeTruthy()
  })

  it('deletes an article after confirmation, and reports a failure', async () => {
    renderManage()
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete article' }))

    await waitFor(() => expect(api.deleteNews).toHaveBeenCalledWith('n1'))
    await waitFor(() => expect(toasts()).toEqual(['success:The article was deleted.']))

    api.deleteNews.mockRejectedValue(apiError(500, { title: 'Boom' }))
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[1]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete article' }))
    await waitFor(() => expect(toasts()).toContain('error:Boom'))
  })

  it('shows the empty state', async () => {
    api.listNews.mockResolvedValue(pageOf([]))
    renderManage()

    expect(await screen.findByText('No articles')).toBeTruthy()
  })
})

describe('NewsEditPage', () => {
  const renderEdit = (path = '/manage/news/n1') =>
    renderRoutes(
      [
        { path: '/manage/news', element: <p>News list</p> },
        { path: '/manage/news/new', element: <NewsEditPage /> },
        { path: '/manage/news/:id', element: <NewsEditPage /> },
      ],
      path,
    )

  it('creates a draft, then moves to its edit page', async () => {
    renderEdit('/manage/news/new')

    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Science fair' } })
    fireEvent.change(screen.getByLabelText('Article text'), { target: { value: '<p>Join us.</p>' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))

    await waitFor(() => expect(api.createNews).toHaveBeenCalledWith({ title: 'Science fair', body: '<p>Join us.</p>', category: 'News', metaDescription: null }))
    await waitFor(() => expect(api.getNews).toHaveBeenCalledWith('n9'))
    expect(toasts()).toEqual(['success:Saved.'])
  })

  it('shows the validation message of a field', async () => {
    api.createNews.mockRejectedValue(apiError(400, { errors: { Title: ['Enter a title.'] } }))
    renderEdit('/manage/news/new')

    fireEvent.click(await screen.findByRole('button', { name: 'Save draft' }))

    expect(await screen.findByText('Enter a title.')).toBeTruthy()
  })

  it('explains that a cover image needs a saved draft', async () => {
    renderEdit('/manage/news/new')

    expect(await screen.findByText('Save the draft first, then you can add a cover image.')).toBeTruthy()
    expect((screen.getByLabelText('Choose an image') as HTMLInputElement).disabled).toBe(true)
  })

  it('loads a saved article, with its address, and only enables saving after a change', async () => {
    renderEdit()

    expect(((await screen.findByLabelText(/^Title/)) as HTMLInputElement).value).toBe('Open house')
    expect(screen.getByText('Address: /news/open-house')).toBeTruthy()
    const save = screen.getByRole('button', { name: 'Save changes' }) as HTMLButtonElement
    expect(save.disabled).toBe(true)

    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: 'Open house 2027' } })
    expect(save.disabled).toBe(false)
    fireEvent.click(save)

    await waitFor(() =>
      expect(api.updateNews).toHaveBeenCalledWith('n1', {
        id: 'n1',
        title: 'Open house 2027',
        body: '<p>Come and see.</p>',
        category: 'News',
        metaDescription: 'Visit us',
        coverImageKey: null,
        rowVersion: 'rv1',
      }),
    )
  })

  it('does not let a draft with unsaved changes be published', async () => {
    renderEdit()
    await screen.findByLabelText(/^Title/)

    expect((screen.getByRole('button', { name: 'Publish' }) as HTMLButtonElement).disabled).toBe(false)
    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: 'Changed' } })

    expect((screen.getByRole('button', { name: 'Publish' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('publishes only after the editor confirms, and tells them where it will appear', async () => {
    renderEdit()
    fireEvent.click(await screen.findByRole('button', { name: 'Publish' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/\/news\/open-house/)).toBeTruthy()
    expect(api.publishNews).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(api.publishNews).toHaveBeenCalledWith('n1'))
    await waitFor(() => expect(toasts()).toEqual(['success:The article is published.']))
  })

  it('unpublishes a published article after confirmation', async () => {
    api.getNews.mockResolvedValue(article({ status: 'Published', publishedAt: '2026-10-05T08:00:00Z' }))
    renderEdit()
    fireEvent.click(await screen.findByRole('button', { name: 'Unpublish' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Unpublish' }))

    await waitFor(() => expect(api.unpublishNews).toHaveBeenCalledWith('n1'))
  })

  it('shows the concurrency message with a way to load the latest version', async () => {
    api.updateNews.mockRejectedValue(apiError(409, { code: 'concurrency.conflict' }))
    renderEdit()
    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Changed' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Somebody else changed this article while you were editing it.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Load the latest version' })).toBeTruthy()
  })

  it('shows a general error when publishing fails', async () => {
    api.publishNews.mockRejectedValue(apiError(422, { title: 'The article has no text.' }))
    renderEdit()
    fireEvent.click(await screen.findByRole('button', { name: 'Publish' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Publish' }))

    expect(await screen.findByText('The article has no text.')).toBeTruthy()
  })

  it('offers a retry when the article cannot be loaded', async () => {
    api.getNews.mockRejectedValueOnce(apiError(500)).mockResolvedValue(article())
    renderEdit()

    expect(await screen.findByText('The article could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByLabelText(/^Title/)).toBeTruthy()
  })

  describe('cover image', () => {
    const picture = (type = 'image/png', size = 1024) => {
      const file = new File(['x'], 'cover.png', { type })
      Object.defineProperty(file, 'size', { value: size })
      return file
    }

    beforeEach(() => {
      vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:preview'), revokeObjectURL: vi.fn() }))
    })

    it('asks for a presigned URL, sends the file straight to storage, and saves the key with the article', async () => {
      const fetchMock = vi.fn().mockResolvedValue({ ok: true, status: 200 })
      vi.stubGlobal('fetch', fetchMock)
      api.createUploadUrl.mockResolvedValue({ url: 'http://storage/put?sig=1', key: 'news/n1/cover.png', contentType: 'image/png' })
      renderEdit()

      fireEvent.change(await screen.findByLabelText('Choose an image'), { target: { files: [picture()] } })

      await waitFor(() => expect(api.createUploadUrl).toHaveBeenCalledWith({ module: 'news', entityId: 'n1', contentType: 'image/png', sizeBytes: 1024 }))
      await waitFor(() => expect(fetchMock).toHaveBeenCalledWith('http://storage/put?sig=1', expect.objectContaining({ method: 'PUT', headers: { 'Content-Type': 'image/png' } })))
      expect(await screen.findByAltText('Cover image preview')).toBeTruthy()

      fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))
      await waitFor(() => expect(api.updateNews).toHaveBeenCalledWith('n1', expect.objectContaining({ coverImageKey: 'news/n1/cover.png' })))
    })

    it.each([
      ['image/gif', 1024, 'Only JPEG, PNG and WebP images can be used.'],
      ['image/png', 6 * 1024 * 1024, 'The image must be at most 5 MB.'],
    ])('refuses %s of %i bytes before anything is sent', async (type, size, message) => {
      renderEdit()

      fireEvent.change(await screen.findByLabelText('Choose an image'), { target: { files: [picture(type, size)] } })

      expect(await screen.findByText(message)).toBeTruthy()
      expect(api.createUploadUrl).not.toHaveBeenCalled()
    })

    it('says the upload failed when storage refuses the file', async () => {
      vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 403 }))
      api.createUploadUrl.mockResolvedValue({ url: 'http://storage/put', key: 'k', contentType: 'image/png' })
      renderEdit()

      fireEvent.change(await screen.findByLabelText('Choose an image'), { target: { files: [picture()] } })

      expect(await screen.findByText('The image could not be uploaded. Please try again.')).toBeTruthy()
    })

    it('removes the image from the article', async () => {
      api.getNews.mockResolvedValue(article({ coverImageKey: 'news/n1/old.png', coverImageUrl: 'http://storage/old.png' }))
      renderEdit()

      fireEvent.click(await screen.findByRole('button', { name: 'Remove image' }))
      fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

      await waitFor(() => expect(api.updateNews).toHaveBeenCalledWith('n1', expect.objectContaining({ coverImageKey: null })))
      expect(screen.queryByAltText('Cover image preview')).toBeNull()
    })
  })
})
