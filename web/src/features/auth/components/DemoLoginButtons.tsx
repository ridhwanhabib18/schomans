import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { APP_ROLES } from '@/shared/lib/roles'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import { useDemoLogin } from '../api/useAuth'
import '../i18n'

/** One-click demo login for each of the six roles. Rendered in demo mode only (see LoginPage). */
export function DemoLoginButtons({ returnTo }: { returnTo: string }) {
  const { t } = useTranslation('auth')
  const navigate = useNavigate()
  const demoLogin = useDemoLogin()

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('demo.title')}</CardTitle>
        <CardDescription>{t('demo.description')}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <ul className="grid gap-2 sm:grid-cols-2">
          {APP_ROLES.map((role) => (
            <li key={role}>
              <Button
                variant="outline"
                className="h-auto w-full flex-col items-start gap-0.5 whitespace-normal py-3 text-left"
                disabled={demoLogin.isPending}
                onClick={() => demoLogin.mutate(role, { onSuccess: () => void navigate(returnTo, { replace: true }) })}
              >
                <span className="font-semibold">{t(`roles.${role}`, { ns: 'common' })}</span>
                <span className="text-xs font-normal text-muted-foreground">{t(`demo.roles.${role}`)}</span>
              </Button>
            </li>
          ))}
        </ul>
        {demoLogin.isError && (
          <p role="alert" className="text-sm text-destructive">
            {t('demo.failed')}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
