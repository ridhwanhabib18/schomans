import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  activateAcademicYear,
  createAcademicYear,
  createClassroom,
  createStudent,
  createSubject,
  createTeacher,
  deleteClassroom,
  deleteStudent,
  deleteSubject,
  deleteTeacher,
  importStudents,
  listAcademicYears,
  listClassrooms,
  listStudents,
  listSubjects,
  listTeachers,
  rolloverAcademicYear,
  updateAcademicYear,
  updateClassroom,
  updateStudent,
  updateSubject,
  updateTeacher,
} from '@/api/generated/schomans'
import type {
  CreateAcademicYearCommand,
  CreateClassroomCommand,
  CreateStudentCommand,
  CreateSubjectCommand,
  CreateTeacherCommand,
  ListClassroomsParams,
  ListStudentsParams,
  ListSubjectsParams,
  ListTeachersParams,
  RolloverAcademicYearCommand,
  UpdateAcademicYearCommand,
  UpdateClassroomCommand,
  UpdateStudentCommand,
  UpdateSubjectCommand,
  UpdateTeacherCommand,
} from '@/api/generated/model'

/**
 * One factory for the query keys of all master data. Every mutation invalidates `all`: a student change moves the student
 * count of a classroom, a rollover changes everything, so a coarse key keeps the screens consistent and the code simple.
 */
export const masterdataKeys = {
  all: ['masterdata'] as const,
  students: (params: ListStudentsParams) => [...masterdataKeys.all, 'students', params] as const,
  teachers: (params: ListTeachersParams) => [...masterdataKeys.all, 'teachers', params] as const,
  classrooms: (params: ListClassroomsParams) => [...masterdataKeys.all, 'classrooms', params] as const,
  subjects: (params: ListSubjectsParams) => [...masterdataKeys.all, 'subjects', params] as const,
  years: () => [...masterdataKeys.all, 'academic-years'] as const,
  activeClassrooms: () => [...masterdataKeys.all, 'active-classrooms'] as const,
  teacherOptions: () => [...masterdataKeys.all, 'teacher-options'] as const,
}

function useInvalidateMasterdata() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: masterdataKeys.all })
}

// ---- students ----

export function useStudents(params: ListStudentsParams) {
  return useQuery({ queryKey: masterdataKeys.students(params), queryFn: () => listStudents(params), placeholderData: keepPreviousData })
}

export function useCreateStudent() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: CreateStudentCommand) => createStudent(command), onSuccess: invalidate })
}

export function useUpdateStudent() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateStudentCommand }) => updateStudent(id, command),
    onSuccess: invalidate,
  })
}

export function useDeleteStudent() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (id: string) => deleteStudent(id), onSuccess: invalidate })
}

export function useImportStudents() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (file: File) => importStudents({ file }), onSuccess: invalidate })
}

// ---- teachers ----

export function useTeachers(params: ListTeachersParams) {
  return useQuery({ queryKey: masterdataKeys.teachers(params), queryFn: () => listTeachers(params), placeholderData: keepPreviousData })
}

export function useCreateTeacher() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: CreateTeacherCommand) => createTeacher(command), onSuccess: invalidate })
}

export function useUpdateTeacher() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateTeacherCommand }) => updateTeacher(id, command),
    onSuccess: invalidate,
  })
}

export function useDeleteTeacher() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (id: string) => deleteTeacher(id), onSuccess: invalidate })
}

/** Every teacher, for a "homeroom teacher" dropdown (up to 100, the largest page). */
export function useTeacherOptions(enabled = true) {
  return useQuery({
    queryKey: masterdataKeys.teacherOptions(),
    queryFn: async () => (await listTeachers({ Page: 1, PageSize: 100, Sort: 'name' })).items,
    enabled,
  })
}

// ---- classrooms ----

export function useClassrooms(params: ListClassroomsParams) {
  return useQuery({ queryKey: masterdataKeys.classrooms(params), queryFn: () => listClassrooms(params), placeholderData: keepPreviousData })
}

export function useCreateClassroom() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: CreateClassroomCommand) => createClassroom(command), onSuccess: invalidate })
}

export function useUpdateClassroom() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateClassroomCommand }) => updateClassroom(id, command),
    onSuccess: invalidate,
  })
}

export function useDeleteClassroom() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (id: string) => deleteClassroom(id), onSuccess: invalidate })
}

/** The classrooms of the current academic year, for filters and "classroom" dropdowns. */
export function useActiveClassrooms() {
  return useQuery({
    queryKey: masterdataKeys.activeClassrooms(),
    queryFn: async () => {
      const years = await listAcademicYears({ Page: 1, PageSize: 100 })
      const current = years.items.find((year) => year.isActive)
      if (!current) {
        return []
      }

      return (await listClassrooms({ AcademicYearId: current.id, Page: 1, PageSize: 100, Sort: 'name' })).items
    },
  })
}

// ---- subjects ----

export function useSubjects(params: ListSubjectsParams) {
  return useQuery({ queryKey: masterdataKeys.subjects(params), queryFn: () => listSubjects(params), placeholderData: keepPreviousData })
}

export function useCreateSubject() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: CreateSubjectCommand) => createSubject(command), onSuccess: invalidate })
}

export function useUpdateSubject() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateSubjectCommand }) => updateSubject(id, command),
    onSuccess: invalidate,
  })
}

export function useDeleteSubject() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (id: string) => deleteSubject(id), onSuccess: invalidate })
}

// ---- academic years ----

export function useAcademicYears() {
  return useQuery({ queryKey: masterdataKeys.years(), queryFn: () => listAcademicYears({ Page: 1, PageSize: 100 }) })
}

export function useCreateAcademicYear() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: CreateAcademicYearCommand) => createAcademicYear(command), onSuccess: invalidate })
}

export function useUpdateAcademicYear() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({
    mutationFn: ({ id, command }: { id: string; command: UpdateAcademicYearCommand }) => updateAcademicYear(id, command),
    onSuccess: invalidate,
  })
}

export function useActivateAcademicYear() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (id: string) => activateAcademicYear(id), onSuccess: invalidate })
}

export function useRolloverAcademicYear() {
  const invalidate = useInvalidateMasterdata()
  return useMutation({ mutationFn: (command: RolloverAcademicYearCommand) => rolloverAcademicYear(command), onSuccess: invalidate })
}
