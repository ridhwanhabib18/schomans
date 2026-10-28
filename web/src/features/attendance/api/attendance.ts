import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import {
  closeAttendanceSession,
  getAttendanceScanSettings,
  getAttendanceSession,
  getClassroomAttendanceSummary,
  getMyAttendance,
  getStudentAttendance,
  listTodayLessons,
  markAttendance,
  openAttendanceSession,
  scanAttendanceQr,
} from '@/api/generated/schomans'
import type { AttendanceSessionDto, MarkAttendanceCommand, QrCodeDto, ScanQrCommand, SessionStudentDto } from '@/api/generated/model'
import { downloadFromApi } from '@/shared/lib/download'
import { ATTENDANCE_EVENTS, createAttendanceConnection } from '@/shared/lib/realtime'
import { useAuthStore } from '@/stores/useAuthStore'

export const attendanceKeys = {
  all: ['attendance'] as const,
  today: () => [...attendanceKeys.all, 'today'] as const,
  session: (id: string) => [...attendanceKeys.all, 'session', id] as const,
  settings: () => [...attendanceKeys.all, 'scan-settings'] as const,
  summary: (classroomId: string, month: string) => [...attendanceKeys.all, 'summary', classroomId, month] as const,
  student: (studentId: string, month: string) => [...attendanceKeys.all, 'student', studentId, month] as const,
}

// ---- the teacher's lessons and sessions ----

export function useTodayLessons() {
  return useQuery({ queryKey: attendanceKeys.today(), queryFn: () => listTodayLessons() })
}

export function useOpenSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (scheduleId: string) => openAttendanceSession({ scheduleId }),
    onSuccess: (session) => {
      queryClient.setQueryData(attendanceKeys.session(session.id), session)
      void queryClient.invalidateQueries({ queryKey: attendanceKeys.today() })
    },
  })
}

export function useSession(id: string) {
  return useQuery({ queryKey: attendanceKeys.session(id), queryFn: () => getAttendanceSession(id) })
}

export function useCloseSession() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => closeAttendanceSession(id),
    onSuccess: (session) => {
      queryClient.setQueryData(attendanceKeys.session(session.id), session)
      void queryClient.invalidateQueries({ queryKey: attendanceKeys.today() })
    },
  })
}

export function useMarkAttendance(sessionId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (command: Omit<MarkAttendanceCommand, 'sessionId'>) =>
      markAttendance(sessionId, command.studentId, { ...command, sessionId }),
    onSuccess: (student) => queryClient.setQueryData<AttendanceSessionDto>(attendanceKeys.session(sessionId), (session) => withStudent(session, student)),
  })
}

/** Puts a changed student into a cached session, keeping the order of the list. */
export function withStudent(session: AttendanceSessionDto | undefined, student: SessionStudentDto): AttendanceSessionDto | undefined {
  if (!session) {
    return session
  }

  const known = session.students.some((s) => s.studentId === student.studentId)
  return {
    ...session,
    students: known ? session.students.map((s) => (s.studentId === student.studentId ? student : s)) : [...session.students, student],
  }
}

export type LiveState = 'connecting' | 'live' | 'offline'

/**
 * Keeps the session screen live over SignalR: a check-in or a manual mark updates the list, a new QR code replaces the
 * old one, and the closing reloads the session. After a dropped connection comes back, the session is reloaded to
 * catch up on whatever was missed.
 */
export function useLiveSession(sessionId: string, enabled: boolean): LiveState {
  const queryClient = useQueryClient()
  const [state, setState] = useState<LiveState>('connecting')

  useEffect(() => {
    if (!enabled) {
      return
    }

    const key = attendanceKeys.session(sessionId)
    const connection = createAttendanceConnection(() => useAuthStore.getState().accessToken)
    let stopped = false

    connection.on(ATTENDANCE_EVENTS.studentCheckedIn, (student: SessionStudentDto) =>
      queryClient.setQueryData<AttendanceSessionDto>(key, (session) => withStudent(session, student)),
    )
    connection.on(ATTENDANCE_EVENTS.qrRotated, (qr: QrCodeDto) =>
      queryClient.setQueryData<AttendanceSessionDto>(key, (session) => (session ? { ...session, qr } : session)),
    )
    connection.on(ATTENDANCE_EVENTS.sessionClosed, () => void queryClient.invalidateQueries({ queryKey: key }))
    connection.onreconnecting(() => setState('offline'))
    connection.onreconnected(async () => {
      await connection.invoke('JoinSession', sessionId)
      setState('live')
      void queryClient.invalidateQueries({ queryKey: key })
    })
    connection.onclose(() => !stopped && setState('offline'))

    connection
      .start()
      .then(() => connection.invoke('JoinSession', sessionId))
      .then(() => {
        if (!stopped) {
          setState('live')
          // Anything that happened between loading the page and joining the group.
          void queryClient.invalidateQueries({ queryKey: key })
        }
      })
      .catch(() => !stopped && setState('offline'))

    return () => {
      stopped = true
      void connection.stop()
    }
  }, [sessionId, enabled, queryClient])

  return enabled ? state : 'offline'
}

// ---- students ----

export function useScanSettings() {
  return useQuery({ queryKey: attendanceKeys.settings(), queryFn: () => getAttendanceScanSettings(), staleTime: 5 * 60_000 })
}

export function useScan() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (command: ScanQrCommand) => scanAttendanceQr(command),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [...attendanceKeys.all, 'student'] }),
  })
}

export function useMyAttendance(month: string) {
  return useQuery({ queryKey: attendanceKeys.student('me', month), queryFn: () => getMyAttendance({ Month: month }), placeholderData: keepPreviousData })
}

export function useStudentAttendance(studentId: string, month: string) {
  return useQuery({
    queryKey: attendanceKeys.student(studentId, month),
    queryFn: () => getStudentAttendance(studentId, { Month: month }),
    placeholderData: keepPreviousData,
  })
}

// ---- reports ----

export function useClassroomSummary(classroomId: string, month: string) {
  return useQuery({
    queryKey: attendanceKeys.summary(classroomId, month),
    queryFn: () => getClassroomAttendanceSummary(classroomId, { Month: month }),
    enabled: classroomId !== '',
    placeholderData: keepPreviousData,
  })
}

export function useExportSummary() {
  return useMutation({
    mutationFn: ({ classroomId, classroomName, month }: { classroomId: string; classroomName: string; month: string }) =>
      downloadFromApi(
        `/api/v1/attendance/classrooms/${classroomId}/summary/export`,
        { month },
        `attendance-${classroomName.replace(/[^\w-]+/g, '-')}-${month}.xlsx`,
      ),
  })
}
