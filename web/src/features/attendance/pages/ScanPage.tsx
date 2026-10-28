import QrScanner from 'qr-scanner'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { PageHeader } from '@/shared/ui/page-parts'
import { tokenFromScan } from '../checkIn'
import '../i18n'

type CameraState = 'starting' | 'scanning' | 'no-camera' | 'denied'

/**
 * The in-app scanner. The camera reads QR codes until it finds a SchoMans attendance code, then the check-in page
 * takes over. A phone without a usable camera gets the way out the PRD names: the camera app, or the teacher.
 */
export function ScanPage() {
  const { t } = useTranslation('attendance')
  const navigate = useNavigate()
  const video = useRef<HTMLVideoElement>(null)
  const [camera, setCamera] = useState<CameraState>('starting')
  const [wrongCode, setWrongCode] = useState(false)

  useEffect(() => {
    const element = video.current
    if (!element) {
      return
    }

    const scanner = new QrScanner(
      element,
      (result) => {
        const token = tokenFromScan(result.data)
        if (token) {
          scanner.stop()
          void navigate(`/attend?t=${encodeURIComponent(token)}`)
        } else {
          setWrongCode(true)
        }
      },
      { preferredCamera: 'environment', highlightScanRegion: true, returnDetailedScanResult: true },
    )

    let cancelled = false
    QrScanner.hasCamera()
      .then((hasCamera) => {
        if (!hasCamera) {
          setCamera('no-camera')
          return
        }

        return scanner.start().then(() => !cancelled && setCamera('scanning'))
      })
      .catch(() => !cancelled && setCamera('denied'))

    return () => {
      cancelled = true
      scanner.stop()
      scanner.destroy()
    }
  }, [navigate])

  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-4">
      <PageHeader title={t('scan.title')} description={t('scan.description')} />

      {camera === 'no-camera' && <Alert tone="info" message={t('scan.noCamera')} />}
      {camera === 'denied' && <Alert tone="error" message={t('scan.cameraDenied')} />}
      {wrongCode && (
        <Alert
          tone="error"
          message={t('scan.notACode')}
          action={
            <Button variant="outline" size="sm" onClick={() => setWrongCode(false)}>
              {t('scan.again')}
            </Button>
          }
        />
      )}

      <div className="relative aspect-square w-full overflow-hidden rounded-xl bg-black">
        {/* The scanner draws the live camera picture into this element. */}
        <video ref={video} muted playsInline className="size-full object-cover" aria-label={t('scan.title')} />
        {camera === 'starting' && <p className="absolute inset-0 grid place-items-center text-sm text-white">{t('scan.cameraStarting')}</p>}
      </div>
    </div>
  )
}
