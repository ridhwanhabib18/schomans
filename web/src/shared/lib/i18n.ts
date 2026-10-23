import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import common from '@/locales/en/common.json'

// English is the default and only locale for now. Indonesian can be added later as `id` by adding
// locales/id/*.json and registering the bundles, with no change to the components.
void i18n.use(initReactI18next).init({
  lng: 'en',
  fallbackLng: 'en',
  defaultNS: 'common',
  ns: ['common'],
  resources: { en: { common } },
  interpolation: { escapeValue: false }, // React already escapes
})

/** Registers the texts of one feature (`locales/en/<feature>.json`) as its own i18next namespace. */
export function addFeatureBundle(namespace: string, bundle: Record<string, unknown>) {
  i18n.addResourceBundle('en', namespace, bundle, true, true)
}

export default i18n
