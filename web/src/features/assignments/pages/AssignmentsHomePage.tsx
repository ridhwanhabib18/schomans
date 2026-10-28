import { Navigate } from 'react-router'
import { selectAuthRole, useAuthStore } from '@/stores/useAuthStore'
import { StudentAssignmentsPage } from './StudentAssignmentsPage'
import { TeacherAssignmentsPage } from './TeacherAssignmentsPage'

/** One address for everybody: teachers and the principal get their list, students their own, parents their children's. */
export function AssignmentsHomePage() {
  const role = useAuthStore(selectAuthRole)

  if (role === 'Student') {
    return <StudentAssignmentsPage />
  }

  if (role === 'Parent') {
    return <Navigate to="/my-children/assignments" replace />
  }

  return <TeacherAssignmentsPage />
}
