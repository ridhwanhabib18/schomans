import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { CONCURRENCY_CONFLICT, fieldError, isConcurrencyConflict, parseProblem } from './problem'

function axiosError(status: number, data: unknown) {
  return new AxiosError('failed', 'ERR', undefined, undefined, {
    status,
    statusText: '',
    data,
    headers: {},
    config: { headers: new AxiosHeaders() },
  })
}

describe('parseProblem', () => {
  it('reads the code, the title and the status of a business error', () => {
    const problem = parseProblem(axiosError(409, { code: 'students.nisn_taken', title: 'NISN is taken' }))

    expect(problem).toEqual({ status: 409, code: 'students.nisn_taken', title: 'NISN is taken', fieldErrors: {} })
  })

  it('collects the validation messages per field and drops anything that is not a text', () => {
    const problem = parseProblem(axiosError(400, { errors: { FullName: ['Required.', 5], Nisn: 'not a list' } }))

    expect(problem.fieldErrors).toEqual({ FullName: ['Required.'] })
  })

  it('ignores a code or title that is not a string', () => {
    const problem = parseProblem(axiosError(500, { code: 12, title: null }))

    expect(problem.code).toBeUndefined()
    expect(problem.title).toBeUndefined()
  })

  it('returns an empty problem for an error that did not come from the API', () => {
    expect(parseProblem(new Error('boom'))).toEqual({ fieldErrors: {} })
    expect(parseProblem(undefined)).toEqual({ fieldErrors: {} })
  })

  it('copes with a response without a body', () => {
    expect(parseProblem(axiosError(502, undefined)).fieldErrors).toEqual({})
  })
})

describe('fieldError', () => {
  const problem = { fieldErrors: { FullName: ['Required.', 'Too short.'] } }

  it('finds the first message of a field whatever its casing', () => {
    expect(fieldError(problem, 'fullName')).toBe('Required.')
  })

  it('returns nothing for another field or without a problem', () => {
    expect(fieldError(problem, 'email')).toBeUndefined()
    expect(fieldError(undefined, 'email')).toBeUndefined()
  })
})

describe('isConcurrencyConflict', () => {
  it('is true only for the concurrency code', () => {
    expect(isConcurrencyConflict({ code: CONCURRENCY_CONFLICT, fieldErrors: {} })).toBe(true)
    expect(isConcurrencyConflict({ code: 'students.nisn_taken', fieldErrors: {} })).toBe(false)
    expect(isConcurrencyConflict(undefined)).toBe(false)
  })
})
