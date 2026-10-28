import { fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/stores/useAuthStore'
import { useChildStore } from '@/stores/useChildStore'
import { useToastStore } from '@/stores/useToastStore'
import { apiError, pageOf, renderRoutes, signInAs } from '@/test/utils'
import { AssignmentDetailPage } from './AssignmentDetailPage'
import { AssignmentEditorPage } from './AssignmentEditorPage'
import { AssignmentsHomePage } from './AssignmentsHomePage'
import { ChildAssignmentsPage } from './StudentAssignmentsPage'

const api = vi.hoisted(() => ({
  listAssignments: vi.fn(),
  listTeachingOptions: vi.fn(),
  createAssignment: vi.fn(),
  updateAssignment: vi.fn(),
  deleteAssignment: vi.fn(),
  createAssignmentAttachmentUploadUrl: vi.fn(),
  getAssignment: vi.fn(),
  getAssignmentAttachmentUrl: vi.fn(),
  listMyAssignments: vi.fn(),
  listChildAssignments: vi.fn(),
  createSubmissionUploadUrl: vi.fn(),
  submitAssignment: vi.fn(),
  listSubmissions: vi.fn(),
  getSubmission: vi.fn(),
  getSubmissionFileUrl: vi.fn(),
  gradeSubmission: vi.fn(),
  listMyChildren: vi.fn(),
}))

vi.mock('@/api/generated/schomans', () => api)

const upload = vi.hoisted(() => ({ put: vi.fn() }))
vi.mock('@/shared/lib/upload', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/upload')>()), putToPresignedUrl: upload.put }))

const open = vi.hoisted(() => ({ tab: vi.fn() }))
vi.mock('@/shared/lib/download', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/shared/lib/download')>()), openInNewTab: open.tab }))

const FUTURE = '2099-01-01T16:59:00Z'

const submission = (overrides: Record<string, unknown> = {}) => ({
  id: 'sub1',
  assignmentId: 'as1',
  studentId: 's1',
  studentName: 'Rina Putri',
  fileName: null,
  hasFile: false,
  textBody: 'My answer',
  submittedAt: '2026-10-28T02:00:00Z',
  isLate: false,
  score: null,
  feedback: null,
  gradedAt: null,
  ...overrides,
})

const assignment = (overrides: Record<string, unknown> = {}) => ({
  id: 'as1',
  title: 'Fractions',
  description: 'Do page 12',
  dueAt: FUTURE,
  subjectId: 'sub-math',
  subjectName: 'Mathematics',
  teacherId: 't1',
  teacherName: 'Budi Santoso',
  termName: 'Term 1',
  classrooms: [{ id: 'c1', name: 'Class A' }],
  attachments: [{ id: 'f1', fileName: 'page12.pdf' }],
  mySubmission: null,
  canEdit: false,
  createdAt: '2026-10-28T00:00:00Z',
  rowVersion: 'rv1',
  ...overrides,
})

const options = [
  { classroomId: 'c1', classroomName: 'Class A', subjectId: 'sub-math', subjectName: 'Mathematics' },
  { classroomId: 'c2', classroomName: 'Class B', subjectId: 'sub-math', subjectName: 'Mathematics' },
  { classroomId: 'c1', classroomName: 'Class A', subjectId: 'sub-sci', subjectName: 'Natural Sciences' },
]

const toasts = () => useToastStore.getState().toasts.map((toast) => `${toast.kind}:${toast.title}`)
const pdf = (name = 'work.pdf', size = 1000) => {
  const file = new File(['x'], name, { type: 'application/pdf' })
  Object.defineProperty(file, 'size', { value: size })
  return file
}

beforeEach(() => {
  Object.values(api).forEach((mock) => mock.mockReset())
  upload.put.mockReset().mockResolvedValue(undefined)
  open.tab.mockReset()
  api.listTeachingOptions.mockResolvedValue(options)
  useToastStore.setState({ toasts: [] })
  useChildStore.setState({ childId: null })
})

afterEach(() => {
  useAuthStore.setState({ status: 'loading', accessToken: null, user: null })
})

describe('AssignmentsHomePage', () => {
  const home = () => renderRoutes([{ path: '/assignments', element: <AssignmentsHomePage /> }, { path: '/my-children/assignments', element: <p>Children page</p> }, { path: '/assignments/new', element: <p>New page</p> }], '/assignments')

  it('shows a teacher the assignments they gave, with how far the class is', async () => {
    signInAs('Teacher')
    api.listAssignments.mockResolvedValue(pageOf([{ id: 'as1', title: 'Fractions', subjectName: 'Mathematics', classroomNames: ['Class A', 'Class B'], dueAt: FUTURE, studentCount: 60, submittedCount: 41, gradedCount: 10 }]))
    home()

    expect(await screen.findByRole('link', { name: 'Fractions' })).toBeTruthy()
    expect(screen.getByText('Class A, Class B')).toBeTruthy()
    expect(screen.getByText('41 of 60')).toBeTruthy()
    expect(screen.getByRole('link', { name: 'New assignment' }).getAttribute('href')).toBe('/assignments/new')
  })

  it('shows the principal the list without the way to give work', async () => {
    signInAs('Principal')
    api.listAssignments.mockResolvedValue(pageOf([]))
    home()

    expect(await screen.findByText('No assignments yet')).toBeTruthy()
    expect(screen.queryByRole('link', { name: 'New assignment' })).toBeNull()
    expect(screen.getByText('The assignments of every teacher.')).toBeTruthy()
  })

  it('searches by title', async () => {
    signInAs('Teacher')
    api.listAssignments.mockResolvedValue(pageOf([]))
    home()
    await screen.findByText('No assignments yet')

    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'frac' } })

    await waitFor(() => expect(api.listAssignments).toHaveBeenLastCalledWith(expect.objectContaining({ Search: 'frac', Page: 1 })))
  })

  it('shows a student their own assignments, nearest due first, with status and score', async () => {
    signInAs('Student')
    api.listMyAssignments.mockResolvedValue(
      pageOf([
        { id: 'a', title: 'Late essay', subjectName: 'English', teacherName: 'Ani', dueAt: '2026-10-01T10:00:00Z', status: 'NotSubmitted', isOverdue: true, submittedAt: null, score: null },
        { id: 'b', title: 'Fractions', subjectName: 'Mathematics', teacherName: 'Budi', dueAt: FUTURE, status: 'Submitted', isOverdue: false, submittedAt: '2026-10-28T02:00:00Z', score: 88 },
        { id: 'c', title: 'Poster', subjectName: 'Arts', teacherName: 'Citra', dueAt: FUTURE, status: 'Late', isOverdue: false, submittedAt: '2026-10-28T02:00:00Z', score: null },
      ]),
    )
    home()

    expect(await screen.findByText('Late essay')).toBeTruthy()
    expect(screen.getByText('Overdue')).toBeTruthy()
    expect(screen.getByText('Submitted')).toBeTruthy()
    expect(screen.getByText('Late')).toBeTruthy()
    expect(screen.getByText('Score 88')).toBeTruthy()
    expect(screen.getByRole('link', { name: /Fractions/ }).getAttribute('href')).toBe('/assignments/b')
  })

  it('sends a parent to the pages of their children', async () => {
    signInAs('Parent')
    home()

    expect(await screen.findByText('Children page')).toBeTruthy()
  })

  it('shows the empty state and offers a retry for a student', async () => {
    signInAs('Student')
    api.listMyAssignments.mockRejectedValueOnce(apiError(500)).mockResolvedValue(pageOf([]))
    home()

    expect(await screen.findByText('Your assignments could not be loaded.')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('No assignments')).toBeTruthy()
  })
})

describe('ChildAssignmentsPage', () => {
  it('shows a parent the assignments of the chosen child, linking with the child', async () => {
    signInAs('Parent')
    api.listMyChildren.mockResolvedValue([{ studentId: 's1', fullName: 'Rina Putri', classroomName: 'Class A' }])
    api.listChildAssignments.mockResolvedValue(
      pageOf([{ id: 'b', title: 'Fractions', subjectName: 'Mathematics', teacherName: 'Budi', dueAt: FUTURE, status: 'Submitted', isOverdue: false, submittedAt: '2026-10-28T02:00:00Z', score: 77 }]),
    )
    renderRoutes([{ path: '/my-children/assignments', element: <ChildAssignmentsPage /> }], '/my-children/assignments')

    const link = await screen.findByRole('link', { name: /Fractions/ })

    expect(link.getAttribute('href')).toBe('/assignments/b?child=s1')
    expect(api.listChildAssignments).toHaveBeenCalledWith('s1', { Page: 1, PageSize: 20 })
    expect(screen.getByText('Score 77')).toBeTruthy()
  })
})

describe('AssignmentDetailPage', () => {
  const detail = (path = '/assignments/as1') =>
    renderRoutes([{ path: '/assignments/:id', element: <AssignmentDetailPage /> }, { path: '/assignments', element: <p>Assignments list</p> }, { path: '/my-children/assignments', element: <p>Children list</p> }], path)

  it('shows the assignment, its attachments, and opens an attachment through a short-lived address', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    api.getAssignmentAttachmentUrl.mockResolvedValue({ url: 'https://storage.test/file?sig=1', expiresAt: FUTURE })
    detail()

    expect(await screen.findByRole('heading', { level: 1, name: 'Fractions' })).toBeTruthy()
    expect(screen.getByText('Do page 12')).toBeTruthy()
    expect(screen.getByText('Class A')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: /page12\.pdf/ }))

    await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/file?sig=1'))
    expect(api.getAssignmentAttachmentUrl).toHaveBeenCalledWith('as1', 'f1', undefined)
  })

  it('tells a student whose work is not handed in what to do, and hands in a typed answer', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    api.submitAssignment.mockResolvedValue(submission())
    detail()
    expect(await screen.findByText('Not handed in yet.')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Or type your answer'), { target: { value: 'My answer' } })
    fireEvent.click(screen.getByRole('button', { name: 'Hand in' }))

    await waitFor(() => expect(api.submitAssignment).toHaveBeenCalledWith('as1', { assignmentId: 'as1', fileKey: null, fileName: null, textBody: 'My answer', keepFile: false }))
    await waitFor(() => expect(toasts()).toEqual(['success:Your work was handed in.']))
  })

  it('uploads a file straight to storage before handing it in', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    api.createSubmissionUploadUrl.mockResolvedValue({ url: 'https://storage.test/put', key: 'assignments/as1/submissions/s1/k.pdf', contentType: 'application/pdf', expiresAt: FUTURE })
    api.submitAssignment.mockResolvedValue(submission({ hasFile: true, fileName: 'work.pdf' }))
    detail()
    await screen.findByText('Not handed in yet.')

    fireEvent.change(screen.getByLabelText('File (PDF or image, at most 10 MB)', { selector: 'input' }), { target: { files: [pdf('work.pdf', 4096)] } })
    fireEvent.click(screen.getByRole('button', { name: 'Hand in' }))

    await waitFor(() => expect(api.submitAssignment).toHaveBeenCalled())
    expect(api.createSubmissionUploadUrl).toHaveBeenCalledWith('as1', { assignmentId: 'as1', contentType: 'application/pdf', sizeBytes: 4096 })
    expect(upload.put).toHaveBeenCalledWith('https://storage.test/put', expect.any(File), 'application/pdf')
    expect(api.submitAssignment).toHaveBeenCalledWith('as1', expect.objectContaining({ fileKey: 'assignments/as1/submissions/s1/k.pdf', fileName: 'work.pdf', keepFile: false }))
  })

  it('refuses a wrong file type or a file over 10 MB before anything is uploaded', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    detail()
    await screen.findByText('Not handed in yet.')
    const picker = screen.getByLabelText('File (PDF or image, at most 10 MB)', { selector: 'input' })

    fireEvent.change(picker, { target: { files: [new File(['x'], 'a.docx', { type: 'application/msword' })] } })
    expect(await screen.findByText('Hand in a PDF or an image (JPEG, PNG, WebP).')).toBeTruthy()
    fireEvent.change(picker, { target: { files: [pdf('big.pdf', 11 * 1024 * 1024)] } })
    expect(await screen.findByText('The file must be at most 10 MB.')).toBeTruthy()

    expect(api.createSubmissionUploadUrl).not.toHaveBeenCalled()
  })

  it('asks for a file or a text before it sends anything', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    detail()
    fireEvent.click(await screen.findByRole('button', { name: 'Hand in' }))

    expect(await screen.findByText('Choose a file or type your answer.')).toBeTruthy()
    expect(api.submitAssignment).not.toHaveBeenCalled()
  })

  it('says when work was handed in late', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    api.submitAssignment.mockResolvedValue(submission({ isLate: true }))
    detail()
    fireEvent.change(await screen.findByLabelText('Or type your answer'), { target: { value: 'x' } })

    fireEvent.click(screen.getByRole('button', { name: 'Hand in' }))

    await waitFor(() => expect(toasts()).toEqual(['success:Your work was handed in after the due date and is marked late.']))
  })

  it('lets a student hand in again and keep the earlier file', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment({ mySubmission: submission({ hasFile: true, fileName: 'work.pdf' }) }))
    api.submitAssignment.mockResolvedValue(submission({ hasFile: true, fileName: 'work.pdf', textBody: 'More' }))
    detail()
    expect(await screen.findByText('Keep work.pdf')).toBeTruthy()

    fireEvent.change(screen.getByLabelText('Or type your answer'), { target: { value: 'More' } })
    fireEvent.click(screen.getByRole('button', { name: 'Hand in again' }))

    await waitFor(() => expect(api.submitAssignment).toHaveBeenCalledWith('as1', { assignmentId: 'as1', fileKey: null, fileName: null, textBody: 'More', keepFile: true }))
  })

  it('shows the score and feedback and locks the work once it is scored', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment({ mySubmission: submission({ score: 88.5, feedback: 'Well explained', gradedAt: '2026-10-29T00:00:00Z' }) }))
    detail()

    expect(await screen.findByText('88.5')).toBeTruthy()
    expect(screen.getByText('Well explained')).toBeTruthy()
    expect(screen.getByText('Your work is scored, so it can no longer be changed.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /Hand in/ })).toBeNull()
  })

  it('explains a refused hand-in', async () => {
    signInAs('Student')
    api.getAssignment.mockResolvedValue(assignment())
    api.submitAssignment.mockRejectedValue(apiError(409, { code: 'assignments.already_graded' }))
    detail()
    fireEvent.change(await screen.findByLabelText('Or type your answer'), { target: { value: 'x' } })

    fireEvent.click(screen.getByRole('button', { name: 'Hand in' }))

    expect(await screen.findByText('Your work is already scored and cannot be replaced.')).toBeTruthy()
  })

  it('shows a parent the work of the chosen child, read only', async () => {
    signInAs('Parent')
    api.getAssignment.mockResolvedValue(assignment({ mySubmission: submission({ score: 77 }) }))
    detail('/assignments/as1?child=s1')

    expect(await screen.findByText('Work of your child')).toBeTruthy()
    expect(api.getAssignment).toHaveBeenCalledWith('as1', { studentId: 's1' })
    expect(screen.queryByRole('button', { name: /Hand in/ })).toBeNull()
    expect(screen.getByRole('link', { name: 'Assignments' }).getAttribute('href')).toBe('/my-children/assignments')
  })

  it('says the assignment cannot be found when it is not for the reader', async () => {
    signInAs('Student')
    api.getAssignment.mockRejectedValue(apiError(403))
    detail()

    expect(await screen.findByText('This assignment could not be found. It may not be for you.')).toBeTruthy()
  })

  describe('as the teacher', () => {
    const rows = [
      { studentId: 's1', studentName: 'Rina Putri', nisn: '1', classroomName: 'Class A', submissionId: 'sub1', submittedAt: '2026-10-28T02:00:00Z', status: 'Submitted', score: null },
      { studentId: 's2', studentName: 'Budi Hartono', nisn: '2', classroomName: 'Class A', submissionId: null, submittedAt: null, status: 'NotSubmitted', score: null },
    ]

    it('sees every student, handed in or not, and the progress', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue(rows)
      detail()

      expect(await screen.findByText('1 of 2 handed in, 0 scored')).toBeTruthy()
      expect(screen.getByText('Budi Hartono')).toBeTruthy()
      expect(screen.getAllByRole('button', { name: /^Review/ })).toHaveLength(1)
      expect(screen.getByRole('link', { name: 'Edit' }).getAttribute('href')).toBe('/assignments/as1/edit')
    })

    it('opens a student\'s work and scores it with feedback', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue(rows)
      api.getSubmission.mockResolvedValue(submission())
      api.gradeSubmission.mockResolvedValue(submission({ score: 90, gradedAt: '2026-10-29T00:00:00Z' }))
      detail()
      fireEvent.click(await screen.findByRole('button', { name: /^Review/ }))

      const dialog = await screen.findByRole('dialog', { name: 'Work of Rina Putri' })
      expect(await within(dialog).findByText('My answer')).toBeTruthy()
      fireEvent.change(within(dialog).getByLabelText(/Score/), { target: { value: '90' } })
      fireEvent.change(within(dialog).getByLabelText('Feedback'), { target: { value: 'Good work' } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Save score' }))

      await waitFor(() => expect(api.gradeSubmission).toHaveBeenCalledWith('sub1', { submissionId: 'sub1', score: 90, feedback: 'Good work' }))
      await waitFor(() => expect(toasts()).toEqual(['success:Score saved. The student was notified.']))
    })

    it('shows the validation message of a score', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue(rows)
      api.getSubmission.mockResolvedValue(submission())
      api.gradeSubmission.mockRejectedValue(apiError(400, { errors: { Score: ['A score has at most two decimals.'] } }))
      detail()
      fireEvent.click(await screen.findByRole('button', { name: /^Review/ }))
      const dialog = await screen.findByRole('dialog')
      await within(dialog).findByText('My answer')

      fireEvent.change(within(dialog).getByLabelText(/Score/), { target: { value: '88.123' } })
      fireEvent.click(within(dialog).getByRole('button', { name: 'Save score' }))

      expect(await within(dialog).findByText('A score has at most two decimals.')).toBeTruthy()
    })

    it('lets the principal read the work but not score it', async () => {
      signInAs('Principal')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: false }))
      api.listSubmissions.mockResolvedValue(rows)
      api.getSubmission.mockResolvedValue(submission())
      detail()
      fireEvent.click(await screen.findByRole('button', { name: /^Review/ }))

      const dialog = await screen.findByRole('dialog')
      await within(dialog).findByText('My answer')

      expect(within(dialog).queryByRole('button', { name: 'Save score' })).toBeNull()
      expect(screen.queryByRole('link', { name: 'Edit' })).toBeNull()
    })

    it('deletes the assignment after confirmation and goes back to the list', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue([])
      api.deleteAssignment.mockResolvedValue(undefined)
      detail()
      fireEvent.click(await screen.findByRole('button', { name: 'Delete' }))

      fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

      await waitFor(() => expect(api.deleteAssignment).toHaveBeenCalledWith('as1'))
      expect(await screen.findByText('Assignments list')).toBeTruthy()
      expect(toasts()).toEqual(['success:The assignment was deleted.'])
    })

    it('explains why an assignment with scores cannot be deleted', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue([])
      api.deleteAssignment.mockRejectedValue(apiError(409, { code: 'assignments.has_scores' }))
      detail()
      fireEvent.click(await screen.findByRole('button', { name: 'Delete' }))

      fireEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Delete' }))

      await waitFor(() => expect(toasts()).toEqual(['error:Some work is already scored, so the assignment cannot be deleted.']))
    })

    it('opens a handed-in file through a short-lived address', async () => {
      signInAs('Teacher')
      api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
      api.listSubmissions.mockResolvedValue(rows)
      api.getSubmission.mockResolvedValue(submission({ hasFile: true, fileName: 'photo.jpg', textBody: null }))
      api.getSubmissionFileUrl.mockResolvedValue({ url: 'https://storage.test/photo?sig=2', expiresAt: FUTURE })
      detail()
      fireEvent.click(await screen.findByRole('button', { name: /^Review/ }))

      fireEvent.click(await screen.findByRole('button', { name: /photo\.jpg/ }))

      await waitFor(() => expect(open.tab).toHaveBeenCalledWith('https://storage.test/photo?sig=2'))
    })
  })
})

describe('AssignmentEditorPage', () => {
  const editor = (path = '/assignments/new') =>
    renderRoutes(
      [
        { path: '/assignments/new', element: <AssignmentEditorPage /> },
        { path: '/assignments/:id/edit', element: <AssignmentEditorPage /> },
        { path: '/assignments/:id', element: <p>Detail page</p> },
        { path: '/assignments', element: <p>Assignments list</p> },
      ],
      path,
    )

  it('offers only the subjects and classrooms the teacher teaches', async () => {
    signInAs('Teacher')
    editor()

    await screen.findByLabelText(/^Title/)
    expect(screen.getByRole('option', { name: 'Mathematics' })).toBeTruthy()
    expect(screen.queryByLabelText('Class B')).toBeNull() // no subject chosen yet

    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'sub-math' } })
    expect(screen.getByLabelText('Class A')).toBeTruthy()
    expect(screen.getByLabelText('Class B')).toBeTruthy()
    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'sub-sci' } })
    expect(screen.queryByLabelText('Class B')).toBeNull()
  })

  it('gives an assignment with a due date read as school time, then opens it', async () => {
    signInAs('Teacher')
    api.createAssignment.mockResolvedValue(assignment({ id: 'new1' }))
    editor()
    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Fractions' } })
    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'sub-math' } })
    fireEvent.click(screen.getByLabelText('Class A'))
    fireEvent.change(screen.getByLabelText(/Due date/), { target: { value: '2026-10-30T23:59' } })
    fireEvent.change(screen.getByLabelText(/Instructions/), { target: { value: 'Page 12' } })

    fireEvent.click(screen.getByRole('button', { name: 'Give assignment' }))

    await waitFor(() =>
      expect(api.createAssignment).toHaveBeenCalledWith({
        title: 'Fractions',
        description: 'Page 12',
        dueAt: '2026-10-30T16:59:00.000Z',
        subjectId: 'sub-math',
        classroomIds: ['c1'],
        attachments: [],
      }),
    )
    expect(await screen.findByText('Detail page')).toBeTruthy()
    expect(toasts()).toEqual(['success:Assignment given. The students were notified.'])
  })

  it('uploads attachments as they are picked and sends their keys', async () => {
    signInAs('Teacher')
    api.createAssignmentAttachmentUploadUrl.mockResolvedValue({ url: 'https://storage.test/put', key: 'assignments/uploads/t1/k.pdf', contentType: 'application/pdf', expiresAt: FUTURE })
    api.createAssignment.mockResolvedValue(assignment({ id: 'new1' }))
    editor()
    fireEvent.change(await screen.findByLabelText(/^Title/), { target: { value: 'Reading' } })
    fireEvent.change(screen.getByLabelText(/^Subject/), { target: { value: 'sub-math' } })
    fireEvent.click(screen.getByLabelText('Class A'))

    fireEvent.change(screen.getByLabelText('Add a file', { selector: 'input' }), { target: { files: [pdf('text.pdf', 2048)] } })
    expect(await screen.findByText('text.pdf')).toBeTruthy()
    fireEvent.click(screen.getByRole('button', { name: 'Give assignment' }))

    await waitFor(() => expect(api.createAssignment).toHaveBeenCalled())
    expect(upload.put).toHaveBeenCalledWith('https://storage.test/put', expect.any(File), 'application/pdf')
    expect(api.createAssignment.mock.calls[0]![0].attachments).toEqual([{ key: 'assignments/uploads/t1/k.pdf', fileName: 'text.pdf' }])
  })

  it('refuses a wrong type, a big file and a sixth attachment', async () => {
    signInAs('Teacher')
    api.createAssignmentAttachmentUploadUrl.mockResolvedValue({ url: 'https://storage.test/put', key: 'k', contentType: 'application/pdf', expiresAt: FUTURE })
    editor()
    const picker = await screen.findByLabelText('Add a file', { selector: 'input' })

    fireEvent.change(picker, { target: { files: [new File(['x'], 'a.exe', { type: 'application/x-msdownload' })] } })
    expect(await screen.findByText('a.exe is not a PDF, an image or an Office file.')).toBeTruthy()
    fireEvent.change(picker, { target: { files: [pdf('big.pdf', 11 * 1024 * 1024)] } })
    expect(await screen.findByText('big.pdf is larger than 10 MB.')).toBeTruthy()

    fireEvent.change(picker, { target: { files: [pdf('1.pdf'), pdf('2.pdf'), pdf('3.pdf'), pdf('4.pdf'), pdf('5.pdf'), pdf('6.pdf')] } })
    expect(await screen.findByText('An assignment has at most 5 attachments.')).toBeTruthy()
    expect(api.createAssignmentAttachmentUploadUrl).toHaveBeenCalledTimes(5)
  })

  it('removes an attachment before saving', async () => {
    signInAs('Teacher')
    api.createAssignmentAttachmentUploadUrl.mockResolvedValue({ url: 'https://storage.test/put', key: 'k1', contentType: 'application/pdf', expiresAt: FUTURE })
    editor()
    fireEvent.change(await screen.findByLabelText('Add a file', { selector: 'input' }), { target: { files: [pdf('text.pdf')] } })
    await screen.findByText('text.pdf')

    fireEvent.click(screen.getByRole('button', { name: 'Remove text.pdf' }))

    expect(screen.queryByText('text.pdf')).toBeNull()
  })

  it('says there is nothing to give assignments for without lessons in the timetable', async () => {
    signInAs('Teacher')
    api.listTeachingOptions.mockResolvedValue([])
    editor()

    expect(await screen.findByText(/You have no lessons in the timetable yet/)).toBeTruthy()
  })

  it('shows the validation messages of the form', async () => {
    signInAs('Teacher')
    api.createAssignment.mockRejectedValue(apiError(400, { errors: { Title: ['Enter a title.'], DueAt: ['The due date must be in the future.'] } }))
    editor()
    fireEvent.change(await screen.findByLabelText(/^Subject/), { target: { value: 'sub-math' } })

    fireEvent.click(screen.getByRole('button', { name: 'Give assignment' }))

    expect(await screen.findByText('Enter a title.')).toBeTruthy()
    expect(screen.getByText('The due date must be in the future.')).toBeTruthy()
  })

  it('edits a saved assignment: fixed subject, kept and removed attachments, the row version', async () => {
    signInAs('Teacher')
    api.getAssignment.mockResolvedValue(assignment({ canEdit: true, attachments: [{ id: 'f1', fileName: 'old.pdf' }, { id: 'f2', fileName: 'keep.pdf' }] }))
    api.updateAssignment.mockResolvedValue(assignment({ canEdit: true }))
    editor('/assignments/as1/edit')

    expect(((await screen.findByLabelText(/^Subject/)) as HTMLSelectElement).disabled).toBe(true)
    expect((screen.getByLabelText(/^Title/) as HTMLInputElement).value).toBe('Fractions')
    fireEvent.click(screen.getByRole('button', { name: 'Remove old.pdf' }))
    fireEvent.change(screen.getByLabelText(/^Title/), { target: { value: 'Fractions II' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }))

    await waitFor(() =>
      expect(api.updateAssignment).toHaveBeenCalledWith('as1', {
        id: 'as1',
        title: 'Fractions II',
        description: 'Do page 12',
        dueAt: FUTURE.replace('Z', '.000Z').replace('16:59:00', '16:59:00'),
        classroomIds: ['c1'],
        keepAttachmentIds: ['f2'],
        newAttachments: [],
        rowVersion: 'rv1',
      }),
    )
    expect(await screen.findByText('Detail page')).toBeTruthy()
  })

  it('explains a change that conflicts with somebody else\'s', async () => {
    signInAs('Teacher')
    api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
    api.updateAssignment.mockRejectedValue(apiError(409, { code: 'concurrency.conflict' }))
    editor('/assignments/as1/edit')
    fireEvent.click(await screen.findByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Somebody else changed this assignment while you were editing it.')).toBeTruthy()
    expect(screen.getByRole('button', { name: 'Load the latest version' })).toBeTruthy()
  })

  it('shows the reason when classrooms can no longer change', async () => {
    signInAs('Teacher')
    api.getAssignment.mockResolvedValue(assignment({ canEdit: true }))
    api.updateAssignment.mockRejectedValue(apiError(409, { code: 'assignments.has_submissions' }))
    editor('/assignments/as1/edit')
    fireEvent.click(await screen.findByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Students already handed in work, so the classrooms can no longer change.')).toBeTruthy()
  })
})
