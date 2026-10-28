import type { ClassroomDto, RolloverAcademicYearCommand, RolloverMapping, TermInput } from '@/api/generated/model'

/**
 * The pure rules of the academic year rollover wizard, kept out of the components so they can be tested without a DOM.
 * They mirror what the server enforces; the server stays the authority, the wizard only saves the admin a round trip.
 */

export interface YearDraft {
  name: string
  startDate: string
  endDate: string
  terms: TermInput[]
}

export interface MappingDraft {
  fromClassroomId: string
  graduate: boolean
  toClassroomName: string
  homeroomTeacherId: string
}

/** One thing the admin still has to fix. `key` is the i18n key under `rollover.problems`, `params` fill its placeholders. */
export interface WizardProblem {
  key: 'name' | 'dates' | 'terms' | 'classroomName' | 'teacher' | 'teacherSame'
  params?: Record<string, string>
}

export interface RolloverSummary {
  moving: number
  graduating: number
  created: number
}

export const emptyYearDraft = (): YearDraft => ({
  name: '',
  startDate: '',
  endDate: '',
  terms: [{ id: null, name: '', startDate: '', endDate: '' }],
})

/** Every classroom starts as "move up" with the current homeroom teacher, which is the most common choice. */
export function initialMappings(classrooms: readonly ClassroomDto[]): MappingDraft[] {
  return classrooms.map((classroom) => ({
    fromClassroomId: classroom.id,
    graduate: false,
    toClassroomName: '',
    homeroomTeacherId: classroom.homeroomTeacherId,
  }))
}

const sameName = (a: string, b: string) => a.trim().toLowerCase() === b.trim().toLowerCase()

export function validateYear(draft: YearDraft): WizardProblem[] {
  const problems: WizardProblem[] = []

  if (draft.name.trim() === '') problems.push({ key: 'name' })
  if (draft.startDate === '' || draft.endDate === '' || draft.endDate <= draft.startDate) problems.push({ key: 'dates' })

  const names = draft.terms.map((term) => term.name.trim().toLowerCase())
  const termsValid =
    draft.terms.length >= 1 &&
    draft.terms.length <= 4 &&
    new Set(names).size === names.length &&
    draft.terms.every(
      (term) =>
        term.name.trim() !== '' &&
        term.startDate !== '' &&
        term.endDate !== '' &&
        term.startDate < term.endDate &&
        draft.startDate !== '' &&
        draft.endDate !== '' &&
        term.startDate >= draft.startDate &&
        term.endDate <= draft.endDate,
    )
  if (!termsValid) problems.push({ key: 'terms' })

  return problems
}

export function validateMappings(mappings: readonly MappingDraft[], classrooms: readonly ClassroomDto[]): WizardProblem[] {
  const problems: WizardProblem[] = []
  const nameOf = (id: string) => classrooms.find((classroom) => classroom.id === id)?.name ?? id
  const moving = mappings.filter((mapping) => !mapping.graduate)

  for (const mapping of moving) {
    const from = nameOf(mapping.fromClassroomId)
    if (mapping.toClassroomName.trim() === '') problems.push({ key: 'classroomName', params: { from } })
    else if (mapping.homeroomTeacherId === '') problems.push({ key: 'teacher', params: { from } })
  }

  // Classrooms moving into the same new classroom share one homeroom teacher: a classroom has exactly one.
  const teacherByTarget = new Map<string, string>()
  const reported = new Set<string>()
  for (const mapping of moving) {
    const target = mapping.toClassroomName.trim().toLowerCase()
    if (target === '' || mapping.homeroomTeacherId === '') continue
    const known = teacherByTarget.get(target)
    if (known === undefined) teacherByTarget.set(target, mapping.homeroomTeacherId)
    else if (known !== mapping.homeroomTeacherId && !reported.has(target)) {
      reported.add(target)
      problems.push({ key: 'teacherSame', params: { to: mapping.toClassroomName.trim() } })
    }
  }

  return problems
}

export function summarize(mappings: readonly MappingDraft[], classrooms: readonly ClassroomDto[]): RolloverSummary {
  const studentsOf = (id: string) => Number(classrooms.find((classroom) => classroom.id === id)?.studentCount ?? 0)
  const moving = mappings.filter((mapping) => !mapping.graduate)
  const targets: string[] = []
  for (const mapping of moving) {
    const name = mapping.toClassroomName.trim()
    if (!targets.some((known) => sameName(known, name))) targets.push(name)
  }

  return {
    moving: moving.reduce((sum, mapping) => sum + studentsOf(mapping.fromClassroomId), 0),
    graduating: mappings.filter((mapping) => mapping.graduate).reduce((sum, mapping) => sum + studentsOf(mapping.fromClassroomId), 0),
    created: targets.length,
  }
}

export function toCommand(year: YearDraft, mappings: readonly MappingDraft[]): RolloverAcademicYearCommand {
  const apiMappings: RolloverMapping[] = mappings.map((mapping) =>
    mapping.graduate
      ? { fromClassroomId: mapping.fromClassroomId, graduate: true, toClassroomName: null, homeroomTeacherId: null }
      : {
          fromClassroomId: mapping.fromClassroomId,
          graduate: false,
          toClassroomName: mapping.toClassroomName.trim(),
          homeroomTeacherId: mapping.homeroomTeacherId,
        },
  )

  return {
    name: year.name.trim(),
    startDate: year.startDate,
    endDate: year.endDate,
    terms: year.terms.map((term) => ({ ...term, name: term.name.trim() })),
    mappings: apiMappings,
  }
}
