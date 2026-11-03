import { Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { formatDate } from '@/shared/lib/format'
import { useMyExtractions, type ApplicantForm } from '../api/admissions'
import { suggestionsFor, type Suggestion } from '../extraction'
import { MismatchList } from './MismatchList'
import '../i18n'

interface ExtractionSuggestionsProps {
  form: ApplicantForm
  onUse: (suggestions: Suggestion[]) => void
}

/**
 * What the AI read from the applicant's documents, offered as suggestions for the form. Nothing is filled in by itself: the
 * applicant uses a suggestion, sees it highlighted in the form, and still has to check it and save.
 */
export function ExtractionSuggestions({ form, onUse }: ExtractionSuggestionsProps) {
  const { t } = useTranslation('admissions')
  const extractions = useMyExtractions()

  if (!extractions.data || extractions.data.documents.length === 0) {
    return null
  }

  const suggestions = suggestionsFor(form, extractions.data)
  const show = (suggestion: Suggestion) => (suggestion.field === 'dateOfBirth' ? formatDate(suggestion.value) : suggestion.value)

  return (
    <div className="flex min-w-0 flex-col gap-3 rounded-lg border border-primary/40 bg-primary/5 p-3">
      <div className="flex items-center gap-2 font-medium">
        <Sparkles aria-hidden="true" className="size-4 text-primary" />
        {t('extraction.title')}
      </div>
      <p className="text-sm text-muted-foreground">{t('extraction.body')}</p>

      {suggestions.length === 0 ? (
        <p className="text-sm">{t('extraction.none')}</p>
      ) : (
        <>
          <ul className="flex flex-col gap-2">
            {suggestions.map((suggestion) => (
              <li key={suggestion.field} className="flex flex-wrap items-center justify-between gap-2 text-sm">
                <span className="min-w-0">
                  <span className="text-muted-foreground">{t(`form.${suggestion.field}`)}: </span>
                  <span className="break-words font-medium">{show(suggestion)}</span>
                  <span className="text-muted-foreground"> ({t('extraction.from', { document: t(`docTypes.${suggestion.source}`) })})</span>
                </span>
                <Button type="button" variant="outline" size="sm" onClick={() => onUse([suggestion])}>
                  {t('extraction.use')}
                  <span className="sr-only">: {t(`form.${suggestion.field}`)}</span>
                </Button>
              </li>
            ))}
          </ul>
          {suggestions.length > 1 && (
            <div>
              <Button type="button" size="sm" onClick={() => onUse(suggestions)}>
                {t('extraction.useAll')}
              </Button>
            </div>
          )}
        </>
      )}

      {extractions.data.mismatches.length > 0 && (
        <div className="flex flex-col gap-2">
          <Alert tone="info" message={`${t('extraction.mismatchTitle')}. ${t('extraction.mismatchBody')}`} />
          <MismatchList mismatches={extractions.data.mismatches} />
        </div>
      )}
    </div>
  )
}
