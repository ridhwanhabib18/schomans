import { CheckCircle2, XCircle } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router'
import type { ScanResultDto } from '@/api/generated/model'
import { parseProblem } from '@/shared/lib/problem'
import { buttonVariants } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { useScan, useScanSettings } from '../api/attendance'
import '../i18n'

type Outcome = { kind: 'done'; result: ScanResultDto } | { kind: 'failed'; message: string }

function currentPosition(): Promise<GeolocationPosition> {
  return new Promise((resolve, reject) =>
    navigator.geolocation
      ? navigator.geolocation.getCurrentPosition(resolve, reject, { enableHighAccuracy: true, timeout: 10_000 })
      : reject(new Error('no geolocation')),
  )
}

/**
 * Where a scanned QR code leads: /attend?t=CODE. A phone's own camera app opens it; the page checks the student in
 * straight away (with the location first, when the school requires it) and shows the result.
 */
export function CheckInPage() {
  const [params] = useSearchParams()
  const token = params.get('t')

  // Keyed by the code: a different code is a new check-in with its own state, even on the same page.
  return <CheckIn key={token ?? ''} token={token} />
}

function CheckIn({ token }: { token: string | null }) {
  const { t } = useTranslation('attendance')
  const { t: tCommon } = useTranslation()
  const settings = useScanSettings()
  const scan = useScan()
  const [locating, setLocating] = useState(false)
  const [outcome, setOutcome] = useState<Outcome>()
  const started = useRef(false)

  useEffect(() => {
    if (!token || !settings.data || started.current) {
      return
    }

    // React may run an effect twice in development; a check-in must be sent once.
    started.current = true
    const fail = (error: unknown) => {
      const problem = parseProblem(error)
      setOutcome({ kind: 'failed', message: t(`errors.${problem.code ?? 'unknown'}`, { defaultValue: problem.title ?? tCommon('errors.generic') }) })
    }

    async function checkIn() {
      let coordinates: { latitude: number; longitude: number } | undefined
      if (settings.data!.geofenceEnabled) {
        setLocating(true)
        try {
          const position = await currentPosition()
          coordinates = { latitude: position.coords.latitude, longitude: position.coords.longitude }
        } catch {
          setOutcome({ kind: 'failed', message: t('checkIn.locationDenied') })
          return
        } finally {
          setLocating(false)
        }
      }

      // mutateAsync, not mutate with callbacks: React may unmount and remount this page's effects once in development,
      // and callbacks passed to mutate are dropped when that happens.
      scan.mutateAsync({ token: token!, ...coordinates }).then((result) => setOutcome({ kind: 'done', result }), fail)
    }

    void checkIn()
  }, [token, settings.data, scan, t, tCommon])

  return (
    <div className="mx-auto flex w-full max-w-md flex-col items-center gap-5 py-8 text-center">
      <h1 className="text-2xl font-semibold">{t('checkIn.title')}</h1>

      {!token ? (
        <p role="alert">{t('checkIn.missingCode')}</p>
      ) : !outcome ? (
        <div role="status" aria-busy="true" className="flex w-full flex-col items-center gap-3">
          <Skeleton className="size-16 rounded-full" />
          <p>{locating ? t('checkIn.locating') : t('checkIn.working')}</p>
        </div>
      ) : outcome.kind === 'done' ? (
        <div role="status" className="flex flex-col items-center gap-3">
          <CheckCircle2 aria-hidden="true" className="size-16 text-emerald-600" />
          <p className="text-xl font-semibold">{outcome.result.alreadyRecorded ? t('checkIn.already') : t('checkIn.success')}</p>
          <p className="text-muted-foreground">
            {t('checkIn.recorded', {
              subject: outcome.result.subjectName,
              classroom: outcome.result.classroomName,
              status: t(`status.${outcome.result.status ?? 'Present'}`),
            })}
          </p>
        </div>
      ) : (
        <div role="alert" className="flex flex-col items-center gap-3">
          <XCircle aria-hidden="true" className="size-16 text-destructive" />
          <p className="text-xl font-semibold">{t('checkIn.failed')}</p>
          <p className="text-muted-foreground">{outcome.message}</p>
        </div>
      )}

      <div className="flex flex-wrap justify-center gap-2">
        <Link to="/attendance/scan" className={buttonVariants({ variant: 'outline' })}>
          {t('checkIn.scanAgain')}
        </Link>
        <Link to="/attendance/me" className={buttonVariants({ variant: 'ghost' })}>
          {t('checkIn.toAttendance')}
        </Link>
      </div>
    </div>
  )
}
