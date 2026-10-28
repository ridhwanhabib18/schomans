import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { AnnouncementDetailPage } from './AnnouncementDetailPage'
import { AnnouncementEditPage } from './AnnouncementEditPage'
import { AnnouncementsInboxPage } from './AnnouncementsInboxPage'
import { AnnouncementsManagePage } from './AnnouncementsManagePage'
import { PublicAnnouncementPage } from './PublicAnnouncementPage'

const api = vi.hoisted(() => ({
  listMyAnnouncements: vi.fn(),
  getMyAnnouncement: vi.fn(),
  markAnnouncementRead: vi.fn(),
  listPublicAnnouncements: vi.fn(),
  getPublicAnnouncement: vi.fn(),
  listAnnouncements: vi.fn(),
  getAnnouncement: vi.fn(),
  createAnnouncement: vi.fn(),
  updateAnnouncement: vi.fn(),
  deleteAnnouncement: vi.fn(),
  publishAnnouncement: vi.fn(),
  listAcademicYears: vi.fn(),
  listClassrooms: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

vi.mock('@/shared/ui/rich-text-editor', () => ({
  RichTextEditor: ({ value, onChange, label }: { value: string; onChange: (html: string) => void; label: string }) => (
    <textarea aria-label={label} value={value} onChange={(event) => onChange(event.target.value)} />
  ),
}))

const announcement = (overrides: Record<string, unknown> = {}) => ({
  id: 'a1',
  title: 'School trip',
  body: '<p>Bring lunch.</p>',
  isPublic: false,
  coverImageKey: null,
  coverImageUrl: null,
  targetRoles: ['Parent'],
  targetClassroomIds: [],
  status: 'Draft',
  publishedAt: null,
  createdAt: '2026-10-01T08:00:00Z',
  rowVersion: 'rv1',
  ...overrides,
})

const summary = (overrides: Record<string, unknown> = {}) => ({
  id: 'a1',
  title: 'School trip',
  excerpt: 'Bring lunch.',
  coverImageUrl: null,
  publishedAt: '2026-10-02T08:00:00Z',
  isRead: false,
  ...overrides,
})

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  api.listMyAnnouncements.mockResolvedValue(pageOf([summary(), summary({ id: 'a2', title: 'Holiday', isRead: true })], { pageSize: 10 }))
  api.getMyAnnouncement.mockResolvedValue({ id: 'a1', title: 'School trip', body: '<p>Bring <em>lunch</em>.</p>', coverImageUrl: null, publishedAt: '2026-10-02T08:00:00Z', isRead: false })
  api.markAnnouncementRead.mockResolvedValue(undefined)
  api.getPublicAnnouncement.mockResolvedValue({ id: 'a1', title: 'Open day', body: '<p>Welcome.</p>', coverImageUrl: null, publishedAt: '2026-10-02T08:00:00Z', isRead: false })
  api.listAnnouncements.mockResolvedValue(
    pageOf([
      announcement(),
      announcement({ id: 'a2', title: 'Holiday', status: 'Published', publishedAt: '2026-10-03T08:00:00Z', isPublic: true, targetRoles: [] }),
    ]),
  )
  api.getAnnouncement.mockResolvedValue(announcement())
  api.createAnnouncement.mockResolvedValue(announcement({ id: 'a9' }))
  for (const name of ['updateAnnouncement', 'deleteAnnouncement'] as const) api[name].mockResolvedValue(undefined)
  api.publishAnnouncement.mockResolvedValue({ announcement: announcement({ status: 'Published' }), notifiedUsers: 12 })
  api.listAcademicYears.mockResolvedValue(pageOf([{ id: 'y1', name: '2026/2027', isActive: true }]))
  api.listClassrooms.mockResolvedValue(pageOf([{ id: 'c1', name: 'X-A' }, { id: 'c2', name: 'X-B' }]))
  useToastStore.setState({ toasts: [] })
  signInAs('Admin')
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
  document.title = ''
})

describe('AnnouncementsInboxPage', () => {
  const renderInbox = () =>
    renderRoutes([{ path: '/announcements', element: <AnnouncementsInboxPage /> }, { path: '/announcements/:id', element: <p>Detail page</p> }], '/announcements')

  it('lists the announcements and marks the unread ones', async () => {
    renderInbox()

    expect(await screen.findByText('School trip')).toBeTruthy()
    expect(screen.getByText('Holiday')).toBeTruthy()
    expect(screen.getAllByLabelText('Unread')).toHaveLength(1)
  })

  it('opens an announcement', async () => {
    renderInbox()

    fireEvent.click(await screen.findByRole('link', { name: /School trip/ }))

    expect(await screen.findByText('Detail page')).toBeTruthy()
  })

  it('shows unread ones only on request', async () => {
    renderInbox()
    await screen.findByText('School trip')

    fireEvent.click(screen.getByRole('checkbox', { name: 'Show unread only' }))

    await waitFor(() => expect(api.listMyAnnouncements).toHaveBeenLastCalledWith({ Page: 1, PageSize: 10, UnreadOnly: true }))
  })

  it('tells the difference between having none and having read everything', async () => {
    api.listMyAnnouncements.mockResolvedValue(pageOf([]))
    renderInbox()
    expect(await screen.findByText('No announcements yet')).toBeTruthy()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Show unread only' }))
    expect(await screen.findByText('You have read everything')).toBeTruthy()
  })

  it('offers a retry when the list cannot be loaded', async () => {
    api.listMyAnnouncements.mockRejectedValueOnce(apiError(500)).mockResolvedValue(pageOf([summary()]))
    renderInbox()

    expect(await screen.findByText('Announcements could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('School trip')).toBeTruthy()
  })
})

describe('AnnouncementDetailPage', () => {
  const renderDetail = () => renderRoutes([{ path: '/announcements/:id', element: <AnnouncementDetailPage /> }], '/announcements/a1')

  it('shows the announcement and marks it read once', async () => {
    renderDetail()

    expect(await screen.findByRole('heading', { level: 1, name: 'School trip' })).toBeTruthy()
    expect(screen.getByText('lunch').tagName).toBe('EM')
    await waitFor(() => expect(api.markAnnouncementRead).toHaveBeenCalledWith('a1'))
    expect(api.markAnnouncementRead).toHaveBeenCalledTimes(1)
  })

  it('does not mark an announcement read again when it is already read', async () => {
    api.getMyAnnouncement.mockResolvedValue({ id: 'a1', title: 'School trip', body: '<p>x</p>', coverImageUrl: null, publishedAt: '2026-10-02T08:00:00Z', isRead: true })
    renderDetail()

    await screen.findByRole('heading', { level: 1, name: 'School trip' })
    expect(api.markAnnouncementRead).not.toHaveBeenCalled()
  })

  it('says the announcement cannot be found when it is not meant for the reader', async () => {
    api.getMyAnnouncement.mockRejectedValue(apiError(404))
    renderDetail()

    expect(await screen.findByText('This announcement could not be found. It may not be meant for you.')).toBeTruthy()
  })
})

describe('PublicAnnouncementPage', () => {
  it('shows a public announcement to a guest', async () => {
    useAuthStore.setState({ status: 'anonymous', accessToken: null, user: null })
    renderRoutes([{ path: '/announcements/public/:id', element: <PublicAnnouncementPage /> }], '/announcements/public/a1')

    expect(await screen.findByRole('heading', { level: 1, name: 'Open day' })).toBeTruthy()
    expect(api.getPublicAnnouncement).toHaveBeenCalledWith('a1')
    expect(document.title).toBe('Open day · SchoMans')
  })

  it('says it does not exist when the announcement is not public', async () => {
    api.getPublicAnnouncement.mockRejectedValue(apiError(404))
    renderRoutes([{ path: '/announcements/public/:id', element: <PublicAnnouncementPage /> }], '/announcements/public/a1')

    expect(await screen.findByText('Announcement not found')).toBeTruthy()
  })
})

describe('AnnouncementsManagePage', () => {
  const renderManage = () => renderRoutes([{ path: '/manage/announcements', element: <AnnouncementsManagePage /> }], '/manage/announcements')

  it('lists announcements with their audience, status and public mark', async () => {
    renderManage()

    expect(await screen.findByText('School trip')).toBeTruthy()
    expect(screen.getByText('Parent')).toBeTruthy()
    expect(screen.getByText('Everybody in the school')).toBeTruthy()
    expect(screen.getAllByText('Public')).toHaveLength(1)
    expect(screen.getAllByText('Published').length).toBeGreaterThan(0)
  })

  it('offers to publish only a draft', async () => {
    renderManage()
    await screen.findByText('School trip')

    expect(screen.getAllByRole('button', { name: /^Publish/ })).toHaveLength(1)
  })

  it('publishes after confirmation and says how many people were notified', async () => {
    renderManage()
    fireEvent.click(await screen.findByRole('button', { name: /^Publish/ }))

    const dialog = await screen.findByRole('alertdialog')
    expect(api.publishAnnouncement).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(api.publishAnnouncement).toHaveBeenCalledWith('a1'))
    await waitFor(() => expect(toasts()).toEqual(['success:Published. 12 people were notified.']))
  })

  it('reports a failed publication', async () => {
    api.publishAnnouncement.mockRejectedValue(apiError(422, { title: 'Nobody is in the audience.' }))
    renderManage()
    fireEvent.click(await screen.findByRole('button', { name: /^Publish/ }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(toasts()).toEqual(['error:Nobody is in the audience.']))
  })

  it('filters by status and title', async () => {
    renderManage()
    await screen.findByText('School trip')

    fireEvent.change(screen.getByRole('combobox', { name: 'Status' }), { target: { value: 'Published' } })
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'holi' } })

    await waitFor(() => expect(api.listAnnouncements).toHaveBeenLastCalledWith(expect.objectContaining({ Status: 'Published', Search: 'holi', Page: 1 })))
  })

  it('deletes after confirmation', async () => {
    renderManage()
    fireEvent.click((await screen.findAllByRole('button', { name: /^Delete/ }))[0]!)
    fireEvent.click(await screen.findByRole('button', { name: 'Delete announcement' }))

    await waitFor(() => expect(api.deleteAnnouncement).toHaveBeenCalledWith('a1'))
    await waitFor(() => expect(toasts()).toEqual(['success:The announcement was deleted.']))
  })

  it('shows the empty state', async () => {
    api.listAnnouncements.mockResolvedValue(pageOf([]))
    renderManage()

    expect(await screen.findByText('No announcements')).toBeTruthy()
  })
})

describe('AnnouncementEditPage', () => {
  const renderEdit = (path = '/manage/announcements/a1') =>
    renderRoutes(
      [
        { path: '/manage/announcements', element: <p>Announcements list</p> },
        { path: '/manage/announcements/new', element: <AnnouncementEditPage /> },
        { path: '/manage/announcements/:id', element: <AnnouncementEditPage /> },
      ],
      path,
    )

  it('describes the audience as the editor changes it', async () => {
    renderEdit('/manage/announcements/new')
    await screen.findByLabelText(/^Title/)

    expect(screen.getByText('Everybody in the school')).toBeTruthy()

    fireEvent.click(screen.getByRole('checkbox', { name: 'Teacher' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Parent' }))
    expect(screen.getByText('Teacher and Parent')).toBeTruthy()

    fireEvent.click(await screen.findByRole('checkbox', { name: 'X-A' }))
    expect(screen.getByText('Teacher and Parent of 1 classroom(s)')).toBeTruthy()
  })

  it('creates a draft with the chosen roles, classrooms and public flag', async () => {
    renderEdit('/manage/announcements/new')

    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Parent meeting' } })
    fireEvent.change(screen.getByLabelText('Announcement message'), { target: { value: '<p>On Friday.</p>' } })
    fireEvent.click(screen.getByRole('checkbox', { name: 'Parent' }))
    fireEvent.click(await screen.findByRole('checkbox', { name: 'X-B' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Also show on the public pages' }))
    fireEvent.click(screen.getByRole('button', { name: 'Save draft' }))

    await waitFor(() =>
      expect(api.createAnnouncement).toHaveBeenCalledWith({
        title: 'Parent meeting',
        body: '<p>On Friday.</p>',
        isPublic: true,
        targetRoles: ['Parent'],
        targetClassroomIds: ['c2'],
      }),
    )
    await waitFor(() => expect(api.getAnnouncement).toHaveBeenCalledWith('a9'))
  })

  it('unchecking a role takes it out of the audience again', async () => {
    renderEdit('/manage/announcements/new')
    await screen.findByLabelText(/^Title/)

    fireEvent.click(screen.getByRole('checkbox', { name: 'Teacher' }))
    fireEvent.click(screen.getByRole('checkbox', { name: 'Teacher' }))

    expect(screen.getByText('Everybody in the school')).toBeTruthy()
  })

  it('shows the validation message of the title', async () => {
    api.createAnnouncement.mockRejectedValue(apiError(400, { errors: { Title: ['Enter a title.'] } }))
    renderEdit('/manage/announcements/new')

    fireEvent.click(await screen.findByRole('button', { name: 'Save draft' }))

    expect(await screen.findByText('Enter a title.')).toBeTruthy()
  })

  it('loads a draft and updates it with its row version', async () => {
    renderEdit()

    const title = (await screen.findByLabelText(/^Title/)) as HTMLInputElement
    expect(title.value).toBe('School trip')
    expect((screen.getByRole('checkbox', { name: 'Parent' }) as HTMLInputElement).checked).toBe(true)

    fireEvent.change(title, { target: { value: 'School trip 2027' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(api.updateAnnouncement).toHaveBeenCalledWith('a1', {
        id: 'a1',
        title: 'School trip 2027',
        body: '<p>Bring lunch.</p>',
        isPublic: false,
        targetRoles: ['Parent'],
        targetClassroomIds: [],
        coverImageKey: null,
        rowVersion: 'rv1',
      }),
    )
  })

  it('publishes a saved draft after confirmation, naming the audience', async () => {
    renderEdit()
    fireEvent.click(await screen.findByRole('button', { name: 'Publish' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText('Parent')).toBeTruthy()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Publish' }))

    await waitFor(() => expect(api.publishAnnouncement).toHaveBeenCalledWith('a1'))
    expect(await screen.findByText('Announcements list')).toBeTruthy()
    expect(toasts()).toEqual(['success:Published. 12 people were notified.'])
  })

  it('still confirms and moves on when publishing changes the row version and the form is rebuilt', async () => {
    api.getAnnouncement.mockResolvedValueOnce(announcement()).mockResolvedValue(announcement({ status: 'Published', rowVersion: 'rv2' }))
    renderEdit()
    fireEvent.click(await screen.findByRole('button', { name: 'Publish' }))
    fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Publish' }))

    expect(await screen.findByText('Announcements list')).toBeTruthy()
    expect(toasts()).toEqual(['success:Published. 12 people were notified.'])
  })

  it('does not let unsaved changes be published', async () => {
    renderEdit()
    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Changed' } })

    expect((screen.getByRole('button', { name: 'Publish' }) as HTMLButtonElement).disabled).toBe(true)
  })

  it('locks the audience of a published announcement but still lets the text be corrected', async () => {
    api.getAnnouncement.mockResolvedValue(announcement({ status: 'Published', publishedAt: '2026-10-03T08:00:00Z' }))
    renderEdit()

    expect(await screen.findByText(/This announcement is published, so its audience can no longer change/)).toBeTruthy()
    // The roles sit in a disabled fieldset, which disables its controls without setting their own attribute.
    expect(screen.getByRole('checkbox', { name: 'Teacher' }).matches(':disabled')).toBe(true)
    expect((screen.getByRole('checkbox', { name: 'Also show on the public pages' }) as HTMLInputElement).disabled).toBe(true)
    expect((screen.getByLabelText(/^Title/) as HTMLInputElement).disabled).toBe(false)
    expect(screen.queryByRole('button', { name: 'Publish' })).toBeNull()
  })

  it('shows the concurrency message', async () => {
    api.updateAnnouncement.mockRejectedValue(apiError(409, { code: 'concurrency.conflict' }))
    renderEdit()
    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Changed' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Somebody else changed this announcement while you were editing it.')).toBeTruthy()
  })

  it('says so when the classrooms cannot be loaded', async () => {
    api.listAcademicYears.mockRejectedValue(apiError(500))
    renderEdit('/manage/announcements/new')

    expect(await screen.findByText('The classrooms could not be loaded.')).toBeTruthy()
  })

  it('says so when the current year has no classrooms', async () => {
    api.listClassrooms.mockResolvedValue(pageOf([]))
    renderEdit('/manage/announcements/new')

    expect(await screen.findByText('There are no classrooms in the current academic year.')).toBeTruthy()
  })
})
