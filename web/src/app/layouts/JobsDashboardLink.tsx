import { Cog } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { config } from '@/shared/lib/config'
import { useAuthStore } from '@/stores/useAuthStore'

/**
 * Opens the Hangfire dashboard of the API (admin staff only). The dashboard is a server page, so the token goes in the
 * address once; the API moves it into a cookie for the dashboard path and drops it from the address.
 */
export function JobsDashboardLink() {
  const { t } = useTranslation()

  function open() {
    const token = useAuthStore.getState().accessToken
    if (token) {
      window.open(`${config.apiUrl}/hangfire?access_token=${encodeURIComponent(token)}`, '_blank', 'noopener,noreferrer')
    }
  }

  return (
    <button
      type="button"
      onClick={open}
      className="flex items-center gap-3 rounded-md px-3 py-2 text-left text-sm font-medium text-muted-foreground hover:bg-muted"
    >
      <Cog aria-hidden="true" className="size-4" />
      {t('nav.jobs')}
    </button>
  )
}
