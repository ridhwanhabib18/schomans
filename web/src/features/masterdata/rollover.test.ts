import { describe, expect, it } from 'vitest'
import type { ClassroomDto } from '@/api/generated/model'
import { emptyYearDraft, initialMappings, summarize, toCommand, validateMappings, validateYear, type MappingDraft, type YearDraft } from './rollover'

const classroom = (id: string, name: string, studentCount: number, teacher = 't1'): ClassroomDto => ({
  id,
  name,
  academicYearId: 'y1',
  academicYearName: '2026/2027',
  homeroomTeacherId: teacher,
  homeroomTeacherName: 'Budi',
  studentCount,
  rowVersion: 'rv',
})

const classrooms = [classroom('c1', 'X-A', 30), classroom('c2', 'X-B', 28, 't2'), classroom('c3', 'XII-A', 25)]

const validYear = (): YearDraft => ({
  name: '2027/2028',
  startDate: '2027-07-12',
  endDate: '2028-06-24',
  terms: [
    { id: null, name: 'Odd', startDate: '2027-07-12', endDate: '2027-12-18' },
    { id: null, name: 'Even', startDate: '2028-01-03', endDate: '2028-06-24' },
  ],
})

const mapping = (overrides: Partial<MappingDraft>): MappingDraft => ({
  fromClassroomId: 'c1',
  graduate: false,
  toClassroomName: 'XI-A',
  homeroomTeacherId: 't1',
  ...overrides,
})

const keys = (problems: { key: string }[]) => problems.map((problem) => problem.key)

describe('initialMappings', () => {
  it('starts every classroom as "move up" with its current homeroom teacher', () => {
    expect(initialMappings(classrooms)).toEqual([
      { fromClassroomId: 'c1', graduate: false, toClassroomName: '', homeroomTeacherId: 't1' },
      { fromClassroomId: 'c2', graduate: false, toClassroomName: '', homeroomTeacherId: 't2' },
      { fromClassroomId: 'c3', graduate: false, toClassroomName: '', homeroomTeacherId: 't1' },
    ])
  })
})

describe('validateYear', () => {
  it('accepts a complete year', () => {
    expect(validateYear(validYear())).toEqual([])
  })

  it('asks for a name', () => {
    expect(keys(validateYear({ ...validYear(), name: '  ' }))).toContain('name')
  })

  it('refuses a year that does not end after it starts, or has no dates', () => {
    expect(keys(validateYear({ ...validYear(), endDate: '2027-07-12' }))).toContain('dates')
    expect(keys(validateYear({ ...validYear(), startDate: '' }))).toContain('dates')
  })

  it('refuses the empty draft', () => {
    expect(keys(validateYear(emptyYearDraft())).sort()).toEqual(['dates', 'name', 'terms'])
  })

  const oneTerm = (term: Partial<YearDraft['terms'][number]>): YearDraft['terms'] => [
    { id: null, name: 'Odd', startDate: '2027-08-01', endDate: '2027-09-01', ...term },
  ]

  it.each<[string, (year: YearDraft) => YearDraft]>([
    ['no terms', (year) => ({ ...year, terms: [] })],
    [
      'five terms',
      (year) => ({ ...year, terms: Array.from({ length: 5 }, (_, index) => ({ id: null, name: `T${index}`, startDate: '2027-08-01', endDate: '2027-09-01' })) }),
    ],
    ['equal names ignoring case', (year) => ({ ...year, terms: year.terms.map((term, index) => ({ ...term, name: index === 0 ? 'Same' : 'SAME' })) })],
    ['a term outside the year', (year) => ({ ...year, terms: oneTerm({ startDate: '2027-06-01' }) })],
    ['a term that ends before it starts', (year) => ({ ...year, terms: oneTerm({ startDate: '2027-09-01', endDate: '2027-08-01' }) })],
    ['a term without a name', (year) => ({ ...year, terms: oneTerm({ name: ' ' }) })],
  ])('refuses %s', (_name, change) => {
    expect(keys(validateYear(change(validYear())))).toContain('terms')
  })
})

describe('validateMappings', () => {
  it('accepts a graduate and a move with a name and a teacher', () => {
    expect(validateMappings([mapping({}), mapping({ fromClassroomId: 'c3', graduate: true, toClassroomName: '' })], classrooms)).toEqual([])
  })

  it('asks for the name of the new classroom, naming the old one', () => {
    expect(validateMappings([mapping({ toClassroomName: ' ' })], classrooms)).toEqual([{ key: 'classroomName', params: { from: 'X-A' } }])
  })

  it('asks for a teacher once the name is there', () => {
    expect(validateMappings([mapping({ homeroomTeacherId: '' })], classrooms)).toEqual([{ key: 'teacher', params: { from: 'X-A' } }])
  })

  it('needs one homeroom teacher for classrooms that move into the same new classroom, whatever the casing', () => {
    const problems = validateMappings([mapping({}), mapping({ fromClassroomId: 'c2', toClassroomName: ' xi-a ', homeroomTeacherId: 't2' })], classrooms)

    expect(problems).toEqual([{ key: 'teacherSame', params: { to: 'xi-a' } }])
  })

  it('accepts several classrooms moving into one with the same teacher', () => {
    expect(validateMappings([mapping({}), mapping({ fromClassroomId: 'c2', toClassroomName: 'XI-A' })], classrooms)).toEqual([])
  })

  it('reports a conflicting target only once', () => {
    const problems = validateMappings(
      [mapping({}), mapping({ fromClassroomId: 'c2', homeroomTeacherId: 't2' }), mapping({ fromClassroomId: 'c3', homeroomTeacherId: 't3' })],
      classrooms,
    )

    expect(problems.filter((problem) => problem.key === 'teacherSame')).toHaveLength(1)
  })

  it('ignores the fields of a graduating classroom', () => {
    expect(validateMappings([mapping({ graduate: true, toClassroomName: '', homeroomTeacherId: '' })], classrooms)).toEqual([])
  })
})

describe('summarize', () => {
  it('counts the students that move up and graduate, and the distinct new classrooms', () => {
    const summary = summarize(
      [mapping({}), mapping({ fromClassroomId: 'c2', toClassroomName: 'xi-a' }), mapping({ fromClassroomId: 'c3', graduate: true, toClassroomName: '' })],
      classrooms,
    )

    expect(summary).toEqual({ moving: 58, graduating: 25, created: 1 })
  })

  it('creates nothing when everybody graduates', () => {
    expect(summarize([mapping({ graduate: true })], classrooms)).toEqual({ moving: 0, graduating: 30, created: 0 })
  })
})

describe('toCommand', () => {
  it('trims the names and sends no name or teacher for a graduating classroom', () => {
    const command = toCommand({ ...validYear(), name: ' 2027/2028 ' }, [
      mapping({ toClassroomName: ' XI-A ' }),
      mapping({ fromClassroomId: 'c3', graduate: true, toClassroomName: 'ignored', homeroomTeacherId: 't9' }),
    ])

    expect(command.name).toBe('2027/2028')
    expect(command.mappings).toEqual([
      { fromClassroomId: 'c1', graduate: false, toClassroomName: 'XI-A', homeroomTeacherId: 't1' },
      { fromClassroomId: 'c3', graduate: true, toClassroomName: null, homeroomTeacherId: null },
    ])
    expect(command.terms).toHaveLength(2)
  })
})
