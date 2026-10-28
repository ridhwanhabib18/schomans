import { describe, expect, it } from 'vitest'
import auth from '@/locales/en/auth.json'
import common from '@/locales/en/common.json'
import dashboard from '@/locales/en/dashboard.json'
import announcements from '@/locales/en/announcements.json'
import assignments from '@/locales/en/assignments.json'
import attendance from '@/locales/en/attendance.json'
import landing from '@/locales/en/landing.json'
import masterdata from '@/locales/en/masterdata.json'
import news from '@/locales/en/news.json'
import notifications from '@/locales/en/notifications.json'
import { APP_ROLES } from '@/shared/lib/roles'

describe('English locale files', () => {
  it.each(APP_ROLES)('have a label, a demo description and a dashboard title for %s', (role) => {
    expect(common.roles[role]).toBeTruthy()
    expect(auth.demo.roles[role]).toBeTruthy()
    expect(dashboard.title[role]).toBeTruthy()
  })

  it('have an error text for every business error code the master data forms can show', () => {
    expect(masterdata.students.errors.students.nisn_taken).toBeTruthy()
    expect(masterdata.rollover.errors.academic_years.rollover_mapping_incomplete).toBeTruthy()
    expect(masterdata.subjects.errors.subjects.name_taken).toBeTruthy()
  })

  it('have a text for every business error code of the classroom features', () => {
    for (const code of ['qr_expired', 'qr_invalid', 'session_closed', 'not_in_classroom', 'location_required', 'outside_school', 'not_today']) {
      expect(attendance.errors.attendance[code as keyof typeof attendance.errors.attendance], code).toBeTruthy()
    }
    for (const code of ['not_your_class', 'has_submissions', 'has_scores', 'already_graded']) {
      expect(assignments.errors.assignments[code as keyof typeof assignments.errors.assignments], code).toBeTruthy()
    }
    for (const code of ['classroom_busy', 'teacher_busy', 'has_sessions']) {
      expect(masterdata.schedules.errors.schedules[code as keyof typeof masterdata.schedules.errors.schedules], code).toBeTruthy()
    }
  })

  it('have no empty texts', () => {
    const empty: string[] = []
    const walk = (node: unknown, path: string) => {
      if (typeof node === 'string') {
        if (!node.trim()) empty.push(path)
      } else if (node && typeof node === 'object') {
        for (const [key, value] of Object.entries(node)) walk(value, `${path}.${key}`)
      }
    }
    walk({ common, auth, dashboard, landing, masterdata, news, announcements, notifications, attendance, assignments }, '')

    expect(empty).toEqual([])
  })
})
