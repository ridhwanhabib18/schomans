import { ImagePlus, Trash2 } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CoverImageError, useCoverUpload, type CoverModule } from '@/shared/hooks/useCoverUpload'
import { COVER_IMAGE_TYPES } from '@/shared/lib/upload'
import { Button } from './button'

interface CoverImageFieldProps {
  module: CoverModule
  /** The saved record. Without it the field explains that the record must be saved first. */
  entityId: string | undefined
  /** The address of the current image, if any. */
  imageUrl: string | null | undefined
  /** Called with the key of a freshly uploaded image. The form saves it with the record. */
  onUploaded: (key: string) => void
  onRemove: () => void
  disabled?: boolean
}

/** Cover image of a news article or announcement: pick a file, it goes straight to storage, the key is saved with the record. */
export function CoverImageField({ module, entityId, imageUrl, onUploaded, onRemove, disabled }: CoverImageFieldProps) {
  const { t } = useTranslation()
  const inputId = useId()
  const input = useRef<HTMLInputElement>(null)
  const upload = useCoverUpload(module, entityId)
  const [preview, setPreview] = useState<string | null>(null)

  // A local preview of the picked file; it is released when replaced or when the field goes away.
  useEffect(() => () => {
    if (preview) {
      URL.revokeObjectURL(preview)
    }
  }, [preview])

  const shown = preview ?? imageUrl
  const refusal = upload.error instanceof CoverImageError ? upload.error.reason : null

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-medium">{t('cover.label')}</span>
      {shown ? (
        <img src={shown} alt={t('cover.preview')} className="max-h-48 w-full max-w-md rounded-lg border border-border object-cover" />
      ) : (
        <p className="text-sm text-muted-foreground">{entityId ? t('cover.none') : t('cover.saveFirst')}</p>
      )}
      <div className="flex flex-wrap gap-2">
        <input
          ref={input}
          id={inputId}
          type="file"
          accept={COVER_IMAGE_TYPES.join(',')}
          className="sr-only"
          aria-label={t('cover.choose')}
          disabled={disabled || !entityId || upload.isPending}
          onChange={(event) => {
            const file = event.target.files?.[0]
            event.target.value = '' // picking the same file again must fire a change
            if (!file) {
              return
            }

            upload.mutate(file, {
              onSuccess: (key) => {
                setPreview(URL.createObjectURL(file))
                onUploaded(key)
              },
            })
          }}
        />
        <Button type="button" variant="outline" size="sm" disabled={disabled || !entityId || upload.isPending} onClick={() => input.current?.click()}>
          <ImagePlus aria-hidden="true" />
          {upload.isPending ? t('cover.uploading') : shown ? t('cover.replace') : t('cover.choose')}
        </Button>
        {shown && (
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={disabled || upload.isPending}
            onClick={() => {
              setPreview(null)
              onRemove()
            }}
          >
            <Trash2 aria-hidden="true" />
            {t('cover.remove')}
          </Button>
        )}
      </div>
      <p className="text-xs text-muted-foreground">{t('cover.rules')}</p>
      {upload.isError && (
        <p role="alert" className="text-sm text-destructive">
          {refusal === 'type' ? t('cover.wrongType') : refusal === 'size' ? t('cover.wrongSize') : t('cover.failed')}
        </p>
      )}
    </div>
  )
}
