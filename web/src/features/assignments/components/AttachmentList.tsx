import { FileText } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { AttachmentDto } from '@/api/generated/model'
import { Button } from '@/shared/ui/button'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useOpenAttachment } from '../api/assignments'
import '../i18n'

/** The teacher's files of an assignment. Each opens through a short-lived address: the files are private. */
export function AttachmentList({ assignmentId, attachments, studentId }: { assignmentId: string; attachments: AttachmentDto[]; studentId?: string }) {
  const { t } = useTranslation('assignments')
  const showToast = useToastStore(selectShowToast)
  const open = useOpenAttachment(assignmentId, studentId)

  return (
    <ul className="flex flex-col gap-2">
      {attachments.map((file) => (
        <li key={file.id}>
          <Button
            variant="outline"
            size="sm"
            disabled={open.isPending}
            onClick={() => open.mutate(file.id, { onError: () => showToast({ kind: 'error', title: t('detail.downloadFailed') }) })}
          >
            <FileText aria-hidden="true" />
            <span className="sr-only">{t('detail.download', { name: '' })}</span>
            {file.fileName}
          </Button>
        </li>
      ))}
    </ul>
  )
}
