import { useTranslation } from 'react-i18next'
import { PublicAnnouncementsSection } from '@/features/announcements/components/PublicAnnouncementsSection'
import { LandingHero } from '@/features/landing/components/LandingHero'
import { LatestNews } from '@/features/news/components/LatestNews'
import { useDocumentMeta } from '@/shared/hooks/useDocumentMeta'
import '@/features/landing/i18n'

/** The landing page. It lives in `app` because it composes several features, which are not allowed to import each other. */
export function HomePage() {
  const { t } = useTranslation('landing')
  const { t: tCommon } = useTranslation()
  useDocumentMeta({ title: tCommon('app.tagline'), description: t('hero.body') })

  return (
    <div className="flex flex-col gap-12">
      <LandingHero />
      <LatestNews />
      <PublicAnnouncementsSection />
    </div>
  )
}
