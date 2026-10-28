// Hand-written until `npm run api:generate` is run against the API with the analytics endpoint; the shapes mirror its DTOs.

export interface MonthRate {
  month: string
  /** Percent present; null in a month without attendance records. */
  rate: number | null
}

export interface ClassroomAttendanceTrend {
  classroomId: string
  classroomName: string
  months: MonthRate[]
}

export interface SubjectAverage {
  subjectId: string
  subjectName: string
  average: number
  students: number
}

export interface PrincipalAnalytics {
  attendanceTrend: ClassroomAttendanceTrend[]
  subjectAverages: SubjectAverage[]
}

export interface AttentionStudent {
  studentId: string
  studentName: string
  classroomName: string
  attendanceRate: number | null
  lowAttendance: boolean
  belowPassingSubjects: string[]
}

export interface TeacherAnalytics {
  attendanceThreshold: number
  flagged: AttentionStudent[]
}

export interface ChildMonth {
  month: string
  averageGrade: number | null
  attendanceRate: number | null
}

export interface ChildAnalytics {
  studentId: string
  studentName: string
  months: ChildMonth[]
}

export interface Analytics {
  role: string
  principal: PrincipalAnalytics | null
  teacher: TeacherAnalytics | null
  children: ChildAnalytics[]
}
