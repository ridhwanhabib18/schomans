import QRCode from 'qrcode'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { QrCodeDto } from '@/api/generated/model'
import { cn } from '@/shared/lib/utils'
import { checkInUrl, secondsUntil } from '../checkIn'
import '../i18n'

/**
 * The QR code of an open session, with a countdown to the next code. It encodes the check-in address, so any camera
 * app opens SchoMans and checks the student in.
 */
export function QrPanel({ qr, large }: { qr: QrCodeDto; large?: boolean }) {
  const { t } = useTranslation('attendance')
  const url = checkInUrl(qr.token)
  const [image, setImage] = useState<string>()
  const [seconds, setSeconds] = useState(() => secondsUntil(qr.nextRotationAt))

  useEffect(() => {
    let cancelled = false
    void QRCode.toDataURL(url, { width: 640, margin: 1, errorCorrectionLevel: 'M' }).then((data) => !cancelled && setImage(data))
    return () => {
      cancelled = true
    }
  }, [url])

  useEffect(() => {
    setSeconds(secondsUntil(qr.nextRotationAt))
    const timer = setInterval(() => setSeconds(secondsUntil(qr.nextRotationAt)), 1000)
    return () => clearInterval(timer)
  }, [qr.nextRotationAt])

  return (
    <figure className="flex flex-col items-center gap-3">
      <div className={cn('aspect-square w-full rounded-xl bg-white p-3', large ? 'max-w-[min(80vh,80vw)]' : 'max-w-sm')}>
        {image && <img src={image} alt={t('session.qrAlt')} data-checkin-url={url} className="size-full [image-rendering:pixelated]" />}
      </div>
      <figcaption className="flex flex-col items-center gap-1 text-center text-sm">
        <span aria-live="off" className="font-medium tabular-nums">
          {seconds > 0 ? t('session.nextCode', { seconds }) : t('session.newCodeSoon')}
        </span>
        {!large && <span className="max-w-sm text-muted-foreground">{t('session.qrHint')}</span>}
      </figcaption>
    </figure>
  )
}
