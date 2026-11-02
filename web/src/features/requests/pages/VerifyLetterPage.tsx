import { CheckCircle2, XCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { formatDate } from '@/shared/lib/format'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { ErrorNotice, PageHeader } from '@/shared/ui/page-parts'
import { Skeleton } from '@/shared/ui/skeleton'
import { useVerifyLetter } from '../api/requests'
import '../i18n'

/** The page the QR code of a letter opens: anybody holding the letter checks that the school really issued it. No login. */
export function VerifyLetterPage() {
  const { t } = useTranslation('requests')
  const { code = '' } = useParams()
  const { data, isPending, isError, refetch } = useVerifyLetter(code)

  return (
    <div className="mx-auto flex min-w-0 max-w-xl flex-col gap-6">
      <PageHeader title={t('verify.title')} />
      {isPending ? (
        <div role="status" aria-busy="true">
          <Skeleton className="h-40" />
          <span className="sr-only">{t('verify.checking')}</span>
        </div>
      ) : isError ? (
        <ErrorNotice message={t('verify.error')} onRetry={() => void refetch()} />
      ) : data.valid ? (
        <Card className="min-w-0 border-emerald-300">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <CheckCircle2 aria-hidden="true" className="size-6 shrink-0 text-emerald-600" />
              {t('verify.validTitle')}
            </CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">{t('verify.validBody')}</p>
            <dl className="grid gap-3 [&>*]:min-w-0">
              <div>
                <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t('verify.number')}</dt>
                <dd className="break-words font-medium">{data.letterNumber}</dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t('verify.student')}</dt>
                <dd className="break-words font-medium">{data.studentName}</dd>
              </div>
              {data.issuedOn && (
                <div>
                  <dt className="text-xs uppercase tracking-wide text-muted-foreground">{t('verify.issued')}</dt>
                  <dd className="font-medium">{formatDate(data.issuedOn)}</dd>
                </div>
              )}
            </dl>
          </CardContent>
        </Card>
      ) : (
        <Card className="min-w-0 border-destructive/40">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <XCircle aria-hidden="true" className="size-6 shrink-0 text-destructive" />
              {t('verify.invalidTitle')}
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm text-muted-foreground">{t('verify.invalidBody')}</p>
          </CardContent>
        </Card>
      )}
    </div>
  )
}
