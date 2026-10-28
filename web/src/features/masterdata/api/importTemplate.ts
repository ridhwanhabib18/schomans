import { useMutation } from '@tanstack/react-query'
import { http } from '@/shared/lib/http'

/**
 * Downloads the .xlsx template of the student import. The request carries the bearer token, so it cannot be a plain
 * link: the file is fetched as a blob and handed to the browser through a temporary address.
 */
export async function downloadStudentImportTemplate(): Promise<void> {
  const response = await http.get<Blob>('/api/v1/students/import-template', { responseType: 'blob' })
  const address = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = address
  link.download = 'students-import-template.xlsx'
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(address)
}

export function useDownloadImportTemplate() {
  return useMutation({ mutationFn: downloadStudentImportTemplate })
}
