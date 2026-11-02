import { FileText, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Badge } from '@/shared/ui/badge'
import { Button } from '@/shared/ui/button'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { DOCUMENT_KINDS, useUploadApplicantDocument, type DocumentStatus, type DocumentType, type MyApplication } from '../api/admissions'
import { admissionErrorText } from '../errors'
import { checkDocument, DOCUMENT_TYPES } from '../files'
import '../i18n'

const STATUS_VARIANT: Record<DocumentStatus, 'muted' | 'success' | 'destructive'> = {
  Pending: 'muted',
  Verified: 'success',
  Rejected: 'destructive',
}

/**
 * The three documents of an application. While it is a draft each one can be uploaded or replaced; once it is handed in
 * only a document the staff sent back can be uploaded again.
 */
export function DocumentsSection({ application }: { application: MyApplication }) {
  const { t } = useTranslation('admissions')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const upload = useUploadApplicantDocument()
  const [error, setError] = useState<string>()
  const inputs = useRef<Partial<Record<DocumentType, HTMLInputElement | null>>>({})

  function pick(type: DocumentType, file: File | undefined) {
    if (!file) {
      return
    }

    const problem = checkDocument(file)
    if (problem) {
      setError(problem === 'type' ? t('documents.wrongType') : t('documents.wrongSize'))
      return
    }

    setError(undefined)
    upload.mutate(
      { type, file },
      {
        onSuccess: () => showToast({ kind: 'success', title: t('documents.uploaded') }),
        onError: (e) => setError(admissionErrorText(e, t, tCommon('errors.generic'))),
      },
    )
  }

  return (
    <div className="flex min-w-0 flex-col gap-3">
      <p className="text-sm text-muted-foreground">{t('documents.hint')}</p>
      {error && <Alert tone="error" message={error} />}
      <ul className="flex flex-col gap-3">
        {DOCUMENT_KINDS.map((type) => {
          const document = application.documents.find((d) => d.type === type)
          const mayChange = application.canEdit || (application.canReplaceRejectedDocuments && document?.status === 'Rejected')
          const label = t(`docTypes.${type}`)
          const busy = upload.isPending && upload.variables?.type === type

          return (
            <li key={type} className="flex min-w-0 flex-col gap-2 rounded-lg border border-border bg-card p-3 sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0">
                <p className="font-medium">{label}</p>
                {document ? (
                  <p className="flex min-w-0 items-center gap-2 text-sm text-muted-foreground">
                    <FileText aria-hidden="true" className="size-4 shrink-0" />
                    <span className="truncate">{document.fileName}</span>
                    <Badge variant={STATUS_VARIANT[document.status]}>{t(`docStatus.${document.status}`)}</Badge>
                  </p>
                ) : (
                  <p className="text-sm text-muted-foreground">{t('documents.missing')}</p>
                )}
                {document?.reviewNote && <p className="mt-1 text-sm text-destructive">{t('documents.note', { note: document.reviewNote })}</p>}
              </div>

              {mayChange && (
                <div className="shrink-0">
                  <input
                    ref={(element) => {
                      inputs.current[type] = element
                    }}
                    type="file"
                    accept={DOCUMENT_TYPES.join(',')}
                    className="sr-only"
                    aria-label={t('documents.fileLabel', { type: label })}
                    onChange={(event) => {
                      const picked = event.target.files?.[0]
                      event.target.value = ''
                      pick(type, picked)
                    }}
                  />
                  <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => inputs.current[type]?.click()}>
                    <Upload aria-hidden="true" />
                    {busy ? tCommon('actions.saving') : document ? t('documents.replace') : t('documents.upload')}
                    <span className="sr-only">: {label}</span>
                  </Button>
                </div>
              )}
            </li>
          )
        })}
      </ul>
    </div>
  )
}
