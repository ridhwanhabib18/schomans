import { LayoutDashboard } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'
import '../i18n'

/** Empty state of the dashboard. The analytics sections (F-09) will replace it as modules arrive. */
export function EmptyDashboard({ roleLabel }: { roleLabel: string }) {
  const { t } = useTranslation('dashboard')

  return (
    <Card>
      <CardHeader className="items-center text-center">
        <LayoutDashboard aria-hidden="true" className="size-10 text-muted-foreground" />
        <CardTitle>{t('empty.title')}</CardTitle>
        <CardDescription>{t('empty.body', { role: roleLabel.toLowerCase() })}</CardDescription>
      </CardHeader>
      <CardContent />
    </Card>
  )
}
