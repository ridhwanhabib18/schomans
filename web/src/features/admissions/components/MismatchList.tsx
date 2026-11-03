import { useTranslation } from 'react-i18next'
import type { ExtractionMismatch } from '../api/admissions'
import '../i18n'

/** The fields that the documents and the form do not agree on, with what each of them says. */
export function MismatchList({ mismatches }: { mismatches: ExtractionMismatch[] }) {
  const { t } = useTranslation('admissions')

  return (
    <ul className="flex flex-col gap-2">
      {mismatches.map((mismatch) => (
        <li key={mismatch.field} className="rounded-md bg-muted/50 p-2 text-sm">
          <p className="font-medium">{t(`extraction.fields.${mismatch.field}`, { defaultValue: mismatch.field })}</p>
          <ul className="mt-1 text-muted-foreground">
            {mismatch.values.map((value) => (
              <li key={value.source}>
                {value.source === 'Form' ? t('extraction.sourceForm') : t(`docTypes.${value.source}`, { defaultValue: value.source })}:{' '}
                <span className="font-medium text-foreground">{value.value}</span>
              </li>
            ))}
          </ul>
        </li>
      ))}
    </ul>
  )
}
