import { http } from './http'

/** Hands a file to the browser's download through a temporary address. */
export function saveBlob(blob: Blob, fileName: string): void {
  const address = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = address
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(address)
}

/**
 * Downloads a file the API produces (an Excel export). The request carries the bearer token, so it cannot be a plain
 * link: the file is fetched as a blob first.
 */
export async function downloadFromApi(url: string, params: Record<string, string>, fileName: string): Promise<void> {
  const response = await http.get<Blob>(url, { params, responseType: 'blob' })
  saveBlob(response.data, fileName)
}

/** Opens a short-lived download address (a presigned storage URL) in a new tab. */
export function openInNewTab(url: string): void {
  window.open(url, '_blank', 'noopener,noreferrer')
}
