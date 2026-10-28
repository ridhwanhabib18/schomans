import { act, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useChildStore } from '@/stores/useChildStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { AttendanceReportPage } from './AttendanceReportPage'
import { CheckInPage } from './CheckInPage'
import { ChildAttendancePage, MyAttendancePage } from './MyAttendancePage'
import { ScanPage } from './ScanPage'
import { SessionPage } from './SessionPage'
import { TodayLessonsPage } from './TodayLessonsPage'

const api = vi.hoisted(() => ({
  listTodayLessons: vi.fn(),
  openAttendanceSession: vi.fn(),
  getAttendanceSession: vi.fn(),
  closeAttendanceSession: vi.fn(),
  markAttendance: vi.fn(),
  scanAttendanceQr: vi.fn(),
  getAttendanceScanSettings: vi.fn(),
  getClassroomAttendanceSummary: vi.fn(),
  getMyAttendance: vi.fn(),
  getStudentAttendance: vi.fn(),
  listMyChildren: vi.fn(),
  listAcademicYears: vi.fn(),
  listClassrooms: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

// A hub that records what the page does and lets the test push events.
const hub = vi.hoisted(() => {
  const state = {
    handlers: new Map<string, (payload: unknown) => void>(),
    reconnected: undefined as undefined | (() => Promise<void>),
    closed: undefined as undefined | (() => void),
    start: vi.fn(),
    invoke: vi.fn(),
    stop: vi.fn(),
  }
  return state
})

vi.mock('@/shared/lib/realtime', () => ({
  ATTENDANCE_EVENTS: { studentCheckedIn: 'StudentCheckedIn', qrRotated: 'QrRotated', sessionClosed: 'SessionClosed' },
  createAttendanceConnection: () => ({
    on: (name: string, handler: (payload: unknown) => void) => hub.handlers.set(name, handler),
    onreconnecting: () => undefined,
    onreconnected: (handler: () => Promise<void>) => {
      hub.reconnected = handler
    },
    onclose: (handler: () => void) => {
      hub.closed = handler
    },
    start: hub.start,
    invoke: hub.invoke,
    stop: hub.stop,
  }),
}))

vi.mock('qrcode', () => ({ default: { toDataURL: vi.fn().mockResolvedValue('data:image/png;base64,AAAA') } }))

const scanner = vi.hoisted(() => ({ onResult: undefined as undefined | ((r: { data: string }) => void), hasCamera: vi.fn(), start: vi.fn(), stop: vi.fn(), destroy: vi.fn() }))
vi.mock('qr-scanner', () => ({
  default: class {
    static hasCamera = scanner.hasCamera
    constructor(_video: unknown, onResult: (r: { data: string }) => void) {
      scanner.onResult = onResult
    }
    start = scanner.start
    stop = scanner.stop
    destroy = scanner.destroy
  },
}))

const TOKEN = '0123456789abcdef0123456789abcdef.AbC123'

const student = (overrides: Record<string, unknown> = {}) => ({
  studentId: 's1',
  fullName: 'Rina Putri',
  nisn: '1000000001',
  status: null,
  scannedAt: null,
  isManual: false,
  note: null,
  ...overrides,
})

const session = (overrides: Record<string, unknown> = {}) => ({
  id: 'ses1',
  scheduleId: 'sch1',
  classroomId: 'c1',
  classroomName: 'Class A',
  subjectName: 'Mathematics',
  teacherName: 'Budi Santoso',
  date: '2026-10-28',
  startTime: '07:00:00',
  endTime: '08:30:00',
  isOpen: true,
  openedAt: '2026-10-28T00:05:00Z',
  closedAt: null,
  qr: { token: TOKEN, rotatedAt: '2026-10-28T00:05:00Z', nextRotationAt: '2099-01-01T00:00:00Z' },
  students: [student(), student({ studentId: 's2', fullName: 'Budi Hartono', nisn: '1000000002' })],
  ...overrides,
})

const lesson = (overrides: Record<string, unknown> = {}) => ({
  scheduleId: 'sch1',
  classroomId: 'c1',
  classroomName: 'Class A',
  subjectName: 'Mathematics',
  startTime: '07:00:00',
  endTime: '08:30:00',
  sessionId: null,
  sessionOpen: false,
  presentCount: 0,
  studentCount: 30,
  ...overrides,
})

const counts = { present: 3, excused: 1, sick: 0, absent: 1, total: 5, presentPercent: 60 }
const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  hub.handlers.clear()
  hub.start.mockReset().mockResolvedValue(undefined)
  hub.invoke.mockReset().mockResolvedValue(undefined)
  hub.stop.mockReset().mockResolvedValue(undefined)
  scanner.hasCamera.mockReset().mockResolvedValue(true)
  scanner.start.mockReset().mockResolvedValue(undefined)
  scanner.stop.mockReset()
  scanner.destroy.mockReset()
  api.getAttendanceScanSettings.mockResolvedValue({ geofenceEnabled: false, radiusMeters: 200 })
  api.listAcademicYears.mockResolvedValue(pageOf([{ id: 'y1', name: '2026/2027', isActive: true }]))
  api.listClassrooms.mockResolvedValue(pageOf([{ id: 'c1', name: 'Class A' }, { id: 'c2', name: 'Class B' }]))
  useToastStore.setState({ toasts: [] })
  useChildStore.setState({ childId: null })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
  vi.unstubAllGlobals()
})

describe('TodayLessonsPage', () => {
  const renderToday = () =>
    renderRoutes([{ path: '/attendance', element: <TodayLessonsPage /> }, { path: '/attendance/sessions/:id', element: <p>Session screen</p> }], '/attendance')

  it('lists the lessons with the state of their attendance', async () => {
    signInAs('Teacher')
    api.listTodayLessons.mockResolvedValue([
      lesson(),
      lesson({ scheduleId: 'sch2', classroomName: 'Class B', sessionId: 'a', sessionOpen: true, presentCount: 12 }),
      lesson({ scheduleId: 'sch3', classroomName: 'Class C', sessionId: 'b', presentCount: 28 }),
    ])
    renderToday()

    expect(await screen.findByText('Not opened')).toBeTruthy()
    expect(screen.getByText('Open: 12 of 30 present')).toBeTruthy()
    expect(screen.getByText('Closed: 28 of 30 present')).toBeTruthy()
    expect(screen.getByRole('link', { name: /Continue/ }).getAttribute('href')).toBe('/attendance/sessions/a')
    expect(screen.getByRole('link', { name: /View/ }).getAttribute('href')).toBe('/attendance/sessions/b')
  })

  it('opens a lesson and goes to its session screen', async () => {
    signInAs('Teacher')
    api.listTodayLessons.mockResolvedValue([lesson()])
    api.openAttendanceSession.mockResolvedValue(session())
    renderToday()

    fireEvent.click(await screen.findByRole('button', { name: /Open attendance/ }))

    await waitFor(() => expect(api.openAttendanceSession).toHaveBeenCalledWith({ scheduleId: 'sch1' }))
    expect(await screen.findByText('Session screen')).toBeTruthy()
  })

  it('says why a lesson could not be opened', async () => {
    signInAs('Teacher')
    api.listTodayLessons.mockResolvedValue([lesson()])
    api.openAttendanceSession.mockRejectedValue(apiError(409, { code: 'attendance.session_already_closed' }))
    renderToday()

    fireEvent.click(await screen.findByRole('button', { name: /Open attendance/ }))

    await waitFor(() => expect(toasts()).toEqual(['error:Attendance for this lesson was already closed today. Open the session to change statuses.']))
  })

  it('shows the empty state and offers a retry on a failure', async () => {
    signInAs('Teacher')
    api.listTodayLessons.mockResolvedValueOnce([]).mockRejectedValueOnce(apiError(500))
    const { queryClient } = renderToday()
    expect(await screen.findByText('No lessons today')).toBeTruthy()

    await act(async () => {
      await queryClient.invalidateQueries()
    })
    expect(await screen.findByText("Today's lessons could not be loaded.")).toBeTruthy()
  })
})

describe('SessionPage', () => {
  const renderSession = () => renderRoutes([{ path: '/attendance/sessions/:id', element: <SessionPage /> }, { path: '/attendance', element: <p>Lessons</p> }], '/attendance/sessions/ses1')

  it('shows the QR code, the check-in address inside it, and the class list', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()

    const image = await screen.findByAltText('QR code to check in to this lesson')
    expect(image.getAttribute('data-checkin-url')).toContain(`/attend?t=${encodeURIComponent(TOKEN)}`)
    expect(screen.getByRole('heading', { level: 1, name: 'Mathematics, Class A' })).toBeTruthy()
    expect(screen.getByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText('0 present, 2 not scanned')).toBeTruthy()
    expect(screen.getAllByText('Not scanned yet')).toHaveLength(2)
  })

  it('joins the session on the hub and turns live', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()

    expect(await screen.findByText('Live')).toBeTruthy()
    expect(hub.invoke).toHaveBeenCalledWith('JoinSession', 'ses1')
  })

  it('shows a student who scans, and the new code, as they arrive', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()
    await screen.findByText('Live')

    act(() => hub.handlers.get('StudentCheckedIn')?.(student({ status: 'Present', scannedAt: '2026-10-28T00:06:00Z' })))
    expect(await screen.findByText('1 present, 1 not scanned')).toBeTruthy()
    expect(screen.queryByText('0 present, 2 not scanned')).toBeNull()

    act(() => hub.handlers.get('QrRotated')?.({ token: 'fedcba9876543210fedcba9876543210.Zzz', rotatedAt: '2026-10-28T00:06:00Z', nextRotationAt: '2099-01-01T00:00:00Z' }))
    await waitFor(() => expect(screen.getByAltText('QR code to check in to this lesson').getAttribute('data-checkin-url')).toContain('fedcba9876543210fedcba9876543210'))
  })

  it('says it is not live when the connection fails', async () => {
    signInAs('Teacher')
    hub.start.mockRejectedValue(new Error('down'))
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()

    expect(await screen.findByText('Not live: the list refreshes when you reload')).toBeTruthy()
  })

  it('catches up after the connection comes back', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()
    await screen.findByText('Live')
    const calls = api.getAttendanceSession.mock.calls.length

    await act(async () => {
      await hub.reconnected?.()
    })

    expect(hub.invoke).toHaveBeenCalledTimes(2)
    await waitFor(() => expect(api.getAttendanceSession.mock.calls.length).toBeGreaterThan(calls))
  })

  it('closes the session only after the teacher confirms, saying how many are marked absent', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    api.closeAttendanceSession.mockResolvedValue(
      session({ isOpen: false, qr: null, students: [student({ status: 'Absent' }), student({ studentId: 's2', fullName: 'Budi Hartono', status: 'Absent' })] }),
    )
    renderSession()
    fireEvent.click(await screen.findByRole('button', { name: 'Close attendance' }))

    const dialog = await screen.findByRole('alertdialog')
    expect(within(dialog).getByText(/the 2 student\(s\) who have not scanned are marked absent/)).toBeTruthy()
    expect(api.closeAttendanceSession).not.toHaveBeenCalled()
    fireEvent.click(within(dialog).getByRole('button', { name: 'Close attendance' }))

    await waitFor(() => expect(api.closeAttendanceSession).toHaveBeenCalledWith('ses1'))
    await waitFor(() => expect(toasts()).toEqual(['success:Attendance closed.']))
    expect(await screen.findByText(/Attendance is closed/)).toBeTruthy()
    expect(screen.queryByAltText('QR code to check in to this lesson')).toBeNull()
  })

  it('lets the teacher set a status by hand, with a note', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    api.markAttendance.mockResolvedValue(student({ status: 'Sick', isManual: true, note: 'Flu' }))
    renderSession()
    fireEvent.click((await screen.findAllByRole('button', { name: /^Change/ }))[0]!)

    const dialog = await screen.findByRole('dialog')
    fireEvent.click(within(dialog).getByLabelText('Sick'))
    fireEvent.change(within(dialog).getByLabelText(/Note/), { target: { value: 'Flu' } })
    fireEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(api.markAttendance).toHaveBeenCalledWith('ses1', 's1', { sessionId: 'ses1', studentId: 's1', status: 'Sick', note: 'Flu' }))
    expect(await screen.findByText(/Set by the teacher/)).toBeTruthy()
    expect(screen.getByText('Sick')).toBeTruthy()
  })

  it('says why a manual change failed', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    api.markAttendance.mockRejectedValue(apiError(403, { code: 'attendance.not_your_lesson' }))
    renderSession()
    fireEvent.click((await screen.findAllByRole('button', { name: /^Change/ }))[0]!)

    fireEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Save' }))

    expect(await screen.findByText('This is not your lesson.')).toBeTruthy()
  })

  it('fills the screen with the code for the projector and goes back', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockResolvedValue(session())
    renderSession()

    fireEvent.click(await screen.findByRole('button', { name: 'Show full screen' }))
    expect(screen.getByRole('button', { name: 'Exit full screen' })).toBeTruthy()
    expect(screen.getByText('0 present, 2 not scanned')).toBeTruthy()

    fireEvent.click(screen.getByRole('button', { name: 'Exit full screen' }))
    expect(await screen.findByRole('button', { name: 'Show full screen' })).toBeTruthy()
  })

  it('shows admin staff the list without the code and without any way to change it', async () => {
    signInAs('Admin')
    api.getAttendanceSession.mockResolvedValue(session({ qr: null }))
    renderSession()

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText(/You are viewing this session/)).toBeTruthy()
    expect(screen.queryByAltText('QR code to check in to this lesson')).toBeNull()
    expect(screen.queryByRole('button', { name: /^Change/ })).toBeNull()
  })

  it('offers a retry when the session cannot be loaded', async () => {
    signInAs('Teacher')
    api.getAttendanceSession.mockRejectedValueOnce(apiError(500)).mockResolvedValue(session())
    renderSession()

    expect(await screen.findByText('This attendance session could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
  })
})

describe('CheckInPage', () => {
  const renderCheckIn = (search = `?t=${TOKEN}`) => renderRoutes([{ path: '/attend', element: <CheckInPage /> }], `/attend${search}`)
  const result = (overrides: Record<string, unknown> = {}) => ({
    sessionId: 'ses1',
    classroomName: 'Class A',
    subjectName: 'Mathematics',
    status: 'Present',
    scannedAt: '2026-10-28T00:06:00Z',
    alreadyRecorded: false,
    ...overrides,
  })

  it('checks the student in at once and shows what was recorded', async () => {
    signInAs('Student')
    api.scanAttendanceQr.mockResolvedValue(result())
    renderCheckIn()

    expect(await screen.findByText('You are checked in')).toBeTruthy()
    expect(api.scanAttendanceQr).toHaveBeenCalledWith({ token: TOKEN })
    expect(screen.getByText('Mathematics, Class A: Present')).toBeTruthy()
  })

  it('sends the check-in only once', async () => {
    signInAs('Student')
    api.scanAttendanceQr.mockResolvedValue(result())
    renderCheckIn()
    await screen.findByText('You are checked in')

    expect(api.scanAttendanceQr).toHaveBeenCalledTimes(1)
  })

  it('says when the student was already checked in', async () => {
    signInAs('Student')
    api.scanAttendanceQr.mockResolvedValue(result({ alreadyRecorded: true }))
    renderCheckIn()

    expect(await screen.findByText('You were already checked in')).toBeTruthy()
  })

  it.each([
    ['attendance.qr_expired', 'This code has expired. Scan the code that is on the screen now.'],
    ['attendance.not_in_classroom', 'This attendance code is for another classroom.'],
    ['attendance.session_closed', 'Attendance for this lesson is already closed. Ask your teacher.'],
  ])('explains a refused check-in (%s)', async (code, message) => {
    signInAs('Student')
    api.scanAttendanceQr.mockRejectedValue(apiError(400, { code }))
    renderCheckIn()

    expect(await screen.findByText(message)).toBeTruthy()
    expect(screen.getByText('You could not be checked in')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'Scan again' }).getAttribute('href')).toBe('/attendance/scan')
  })

  it('says there is no code when the address has none', async () => {
    signInAs('Student')
    renderCheckIn('')

    expect(await screen.findByText(/There is no attendance code in this address/)).toBeTruthy()
    expect(api.scanAttendanceQr).not.toHaveBeenCalled()
  })

  it('sends the location first when the school requires it', async () => {
    signInAs('Student')
    api.getAttendanceScanSettings.mockResolvedValue({ geofenceEnabled: true, radiusMeters: 200 })
    api.scanAttendanceQr.mockResolvedValue(result())
    vi.stubGlobal('navigator', { ...navigator, geolocation: { getCurrentPosition: (ok: (p: unknown) => void) => ok({ coords: { latitude: -6.2, longitude: 106.8 } }) } })
    renderCheckIn()

    expect(await screen.findByText('You are checked in')).toBeTruthy()
    expect(api.scanAttendanceQr).toHaveBeenCalledWith({ token: TOKEN, latitude: -6.2, longitude: 106.8 })
  })

  it('asks for location access when it is refused, and sends nothing', async () => {
    signInAs('Student')
    api.getAttendanceScanSettings.mockResolvedValue({ geofenceEnabled: true, radiusMeters: 200 })
    vi.stubGlobal('navigator', { ...navigator, geolocation: { getCurrentPosition: (_ok: unknown, fail: (e: unknown) => void) => fail(new Error('denied')) } })
    renderCheckIn()

    expect(await screen.findByText('Allow location access to check in: attendance only counts at school.')).toBeTruthy()
    expect(api.scanAttendanceQr).not.toHaveBeenCalled()
  })
})

describe('ScanPage', () => {
  const renderScan = () => renderRoutes([{ path: '/attendance/scan', element: <ScanPage /> }, { path: '/attend', element: <p>Check-in page</p> }], '/attendance/scan')

  it('starts the camera and opens the check-in page when it reads an attendance code', async () => {
    signInAs('Student')
    const { router } = renderScan()
    await waitFor(() => expect(scanner.start).toHaveBeenCalled())

    act(() => scanner.onResult?.({ data: `http://localhost:5173/attend?t=${encodeURIComponent(TOKEN)}` }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/attend'))
    expect(router.state.location.search).toBe(`?t=${encodeURIComponent(TOKEN)}`)
    expect(scanner.stop).toHaveBeenCalled()
  })

  it('says so when the camera reads some other QR code, and keeps scanning', async () => {
    signInAs('Student')
    const { router } = renderScan()
    await waitFor(() => expect(scanner.start).toHaveBeenCalled())

    act(() => scanner.onResult?.({ data: 'https://example.com/menu' }))

    expect(await screen.findByText('That QR code is not a SchoMans attendance code.')).toBeTruthy()
    expect(router.state.location.pathname).toBe('/attendance/scan')
    fireEvent.click(screen.getByRole('button', { name: 'Scan again' }))
    await waitFor(() => expect(screen.queryByText('That QR code is not a SchoMans attendance code.')).toBeNull())
  })

  it('points to the camera app and the teacher when the phone has no camera', async () => {
    signInAs('Student')
    scanner.hasCamera.mockResolvedValue(false)
    renderScan()

    expect(await screen.findByText(/Your phone's camera cannot be used here/)).toBeTruthy()
    expect(scanner.start).not.toHaveBeenCalled()
  })

  it('asks for camera access when it is denied, and releases the camera when the page goes away', async () => {
    signInAs('Student')
    scanner.start.mockRejectedValue(new Error('denied'))
    const { unmount } = renderScan()

    expect(await screen.findByText(/Allow camera access to scan the code/)).toBeTruthy()
    unmount()
    expect(scanner.destroy).toHaveBeenCalled()
  })
})

describe('student and parent views', () => {
  const month = {
    studentId: 's1',
    fullName: 'Rina Putri',
    classroomName: 'Class A',
    month: '2026-10',
    counts,
    entries: [
      { sessionId: 'a', date: '2026-10-26', subjectName: 'Mathematics', startTime: '07:00:00', status: 'Present', scannedAt: null, note: null },
      { sessionId: 'b', date: '2026-10-27', subjectName: 'English', startTime: '08:30:00', status: 'Sick', scannedAt: null, note: 'Flu' },
    ],
  }

  it('shows a student their month with counts and every lesson', async () => {
    signInAs('Student')
    api.getMyAttendance.mockResolvedValue(month)
    renderRoutes([{ path: '/attendance/me', element: <MyAttendancePage /> }], '/attendance/me')

    expect(await screen.findByText('Mathematics')).toBeTruthy()
    expect(screen.getByText('60% present')).toBeTruthy()
    expect(screen.getByText('Flu')).toBeTruthy()
    expect(screen.getAllByText('Sick').length).toBeGreaterThan(0) // the count and the badge
  })

  it('asks the API for the previous month when the student goes back', async () => {
    signInAs('Student')
    api.getMyAttendance.mockResolvedValue(month)
    renderRoutes([{ path: '/attendance/me', element: <MyAttendancePage /> }], '/attendance/me')
    await screen.findByText('Mathematics')
    const first = api.getMyAttendance.mock.calls[0]![0].Month as string

    fireEvent.click(screen.getByRole('button', { name: 'Previous month' }))

    await waitFor(() => expect(api.getMyAttendance.mock.calls.at(-1)![0].Month).not.toBe(first))
    expect((screen.getByRole('button', { name: 'Next month' }) as HTMLButtonElement).disabled).toBe(false)
  })

  it('shows the empty month and offers a retry on a failure', async () => {
    signInAs('Student')
    api.getMyAttendance.mockRejectedValueOnce(apiError(500)).mockResolvedValue({ ...month, entries: [], counts: { ...counts, total: 0, present: 0, presentPercent: 0 } })
    renderRoutes([{ path: '/attendance/me', element: <MyAttendancePage /> }], '/attendance/me')

    expect(await screen.findByText('The attendance could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('No attendance was taken in this month.')).toBeTruthy()
  })

  it('lets a parent choose between children and keeps the choice', async () => {
    signInAs('Parent')
    api.listMyChildren.mockResolvedValue([
      { studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' },
      { studentId: 's2', fullName: 'Oka Setiawan', classroomName: 'Class A' },
    ])
    api.getStudentAttendance.mockImplementation((id: string) => Promise.resolve({ ...month, studentId: id, fullName: id === 's1' ? 'Rina Putri' : 'Oka Setiawan' }))
    renderRoutes([{ path: '/my-children/attendance', element: <ChildAttendancePage /> }], '/my-children/attendance')

    await waitFor(() => expect(api.getStudentAttendance).toHaveBeenCalledWith('s1', expect.objectContaining({ Month: expect.any(String) })))
    fireEvent.change(screen.getByLabelText('Child'), { target: { value: 's2' } })

    await waitFor(() => expect(api.getStudentAttendance).toHaveBeenCalledWith('s2', expect.anything()))
    expect(useChildStore.getState().childId).toBe('s2')
  })

  it('says so to a parent without linked children', async () => {
    signInAs('Parent')
    api.listMyChildren.mockResolvedValue([])
    renderRoutes([{ path: '/my-children/attendance', element: <ChildAttendancePage /> }], '/my-children/attendance')

    expect(await screen.findByText(/No children are linked to your account/)).toBeTruthy()
  })
})

describe('AttendanceReportPage', () => {
  const summary = {
    classroomId: 'c1',
    classroomName: 'Class A',
    month: '2026-10',
    sessionCount: 6,
    students: [
      { studentId: 's1', fullName: 'Rina Putri', nisn: '1000000001', counts: { present: 5, excused: 0, sick: 0, absent: 1, total: 6, presentPercent: 83.3 } },
      { studentId: 's2', fullName: 'Budi Hartono', nisn: '1000000002', counts: { present: 3, excused: 1, sick: 0, absent: 2, total: 6, presentPercent: 50 } },
      { studentId: 's3', fullName: 'Citra Dewi', nisn: '1000000003', counts: { present: 0, excused: 0, sick: 0, absent: 0, total: 0, presentPercent: 0 } },
    ],
  }

  it('shows the month per student, flags the ones below 80 percent, and counts the lessons', async () => {
    signInAs('Teacher')
    api.getClassroomAttendanceSummary.mockResolvedValue(summary)
    renderRoutes([{ path: '/attendance/reports', element: <AttendanceReportPage /> }], '/attendance/reports')

    expect(await screen.findByText('Rina Putri')).toBeTruthy()
    expect(screen.getByText('6 lesson(s) with attendance this month')).toBeTruthy()
    expect(screen.getAllByText('Below 80%')).toHaveLength(1)
    expect(screen.getByText('83.3%')).toBeTruthy()
    expect(api.getClassroomAttendanceSummary).toHaveBeenCalledWith('c1', expect.objectContaining({ Month: expect.stringMatching(/^\d{4}-\d{2}$/) }))
  })

  it('switches classroom', async () => {
    signInAs('Teacher')
    api.getClassroomAttendanceSummary.mockResolvedValue(summary)
    renderRoutes([{ path: '/attendance/reports', element: <AttendanceReportPage /> }], '/attendance/reports')
    await screen.findByText('Rina Putri')
    await waitFor(() => expect(screen.getByRole('option', { name: 'Class B' })).toBeTruthy())

    fireEvent.change(screen.getByLabelText('Classroom'), { target: { value: 'c2' } })

    await waitFor(() => expect(api.getClassroomAttendanceSummary).toHaveBeenCalledWith('c2', expect.anything()))
  })

  it('says so when the teacher does not work in the classroom', async () => {
    signInAs('Teacher')
    api.getClassroomAttendanceSummary.mockRejectedValue(apiError(403, { code: 'attendance.not_your_classroom' }))
    renderRoutes([{ path: '/attendance/reports', element: <AttendanceReportPage /> }], '/attendance/reports')

    expect(await screen.findByText('You can only see attendance of classrooms you work in.')).toBeTruthy()
  })

  it('exports the month to an Excel file', async () => {
    signInAs('Principal')
    api.getClassroomAttendanceSummary.mockResolvedValue(summary)
    const http = await import('@/shared/lib/http')
    const get = vi.spyOn(http.http, 'get').mockResolvedValue({ data: new Blob(['x']) })
    vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:x'), revokeObjectURL: vi.fn() }))
    renderRoutes([{ path: '/attendance/reports', element: <AttendanceReportPage /> }], '/attendance/reports')
    await screen.findByText('Rina Putri')

    fireEvent.click(screen.getByRole('button', { name: 'Export to Excel' }))

    await waitFor(() => expect(get).toHaveBeenCalledWith('/api/v1/attendance/classrooms/c1/summary/export', expect.objectContaining({ responseType: 'blob', params: { month: expect.any(String) } })))
    get.mockRestore()
  })

  it('tells the user when the export fails', async () => {
    signInAs('Principal')
    api.getClassroomAttendanceSummary.mockResolvedValue(summary)
    const http = await import('@/shared/lib/http')
    const get = vi.spyOn(http.http, 'get').mockRejectedValue(new Error('down'))
    renderRoutes([{ path: '/attendance/reports', element: <AttendanceReportPage /> }], '/attendance/reports')
    await screen.findByText('Rina Putri')

    fireEvent.click(screen.getByRole('button', { name: 'Export to Excel' }))

    await waitFor(() => expect(toasts()).toEqual(['error:The export failed. Please try again.']))
    get.mockRestore()
  })
})
