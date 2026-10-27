import { Download } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { ImportStudentsSummary } from '@/api/generated/model'
import { parseProblem } from '@/shared/lib/problem'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { FormField } from '@/shared/ui/form-field'
import { Input } from '@/shared/ui/form-controls'
import { useDownloadImportTemplate } from '../api/importTemplate'
import { useImportStudents } from '../api/masterdata'
import '../i18n'

/** Import students from an .xlsx file. Rows that fail are listed with the reason; the others are imported anyway. */
export function ImportStudentsDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation('masterdata')
  const { t: tCommon } = useTranslation()
  const importStudents = useImportStudents()
  const template = useDownloadImportTemplate()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [result, setResult] = useState<ImportStudentsSummary>()
  const [fileProblem, setFileProblem] = useState<string>()
  const [error, setError] = useState<string>()

  function start() {
    setError(undefined)
    setFileProblem(undefined)

    if (!file) {
      setError(t('import.noFile'))
      return
    }

    if (!file.name.toLowerCase().endsWith('.xlsx')) {
      setError(t('import.wrongExtension'))
      return
    }

    importStudents.mutate(file, {
      onSuccess: setResult,
      onError: (e) => {
        const problem = parseProblem(e)
        // The server explains what is wrong with the file (missing columns, too many rows) in its title.
        setFileProblem(problem.title ?? tCommon('errors.generic'))
      },
    })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{result ? t('import.done') : t('import.title')}</DialogTitle>
          {!result && <DialogDescription>{t('import.description')}</DialogDescription>}
        </DialogHeader>

        {result ? (
          <div className="flex flex-col gap-4">
            <p role="status" className="text-sm">
              {t('import.summary', { created: result.created, total: result.totalRows })}{' '}
              {result.failed.length === 0 && result.totalRows > 0 && t('import.allGood')}
            </p>

            {result.failed.length > 0 && (
              <div className="flex flex-col gap-2">
                <h3 className="font-medium">{t('import.failedTitle', { count: result.failed.length })}</h3>
                <p className="text-sm text-muted-foreground">{t('import.failedHint')}</p>
                <div className="max-h-64 overflow-auto rounded-lg border border-border">
                  <table className="w-full text-left text-sm">
                    <caption className="sr-only">{t('import.failedCaption')}</caption>
                    <thead className="bg-muted text-xs uppercase text-muted-foreground">
                      <tr>
                        <th scope="col" className="w-16 px-3 py-2">{t('import.columns.row')}</th>
                        <th scope="col" className="px-3 py-2">{t('import.columns.reason')}</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-border">
                      {result.failed.map((failure) => (
                        <tr key={failure.row}>
                          <td className="px-3 py-2 font-mono">{failure.row}</td>
                          <td className="px-3 py-2">{failure.message}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => { setResult(undefined); setFile(null) }}>
                {t('import.again')}
              </Button>
              <Button type="button" onClick={onClose}>
                {t('import.close')}
              </Button>
            </DialogFooter>
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            <div>
              <Button type="button" variant="outline" size="sm" disabled={template.isPending} onClick={() => template.mutate()}>
                <Download aria-hidden="true" />
                {t('import.downloadTemplate')}
              </Button>
            </div>

            <FormField label={t('import.choose')} hint={t('import.chooseHint')}>
              <Input
                ref={input}
                type="file"
                accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                className="h-auto py-2"
                onChange={(event) => setFile(event.target.files?.[0] ?? null)}
              />
            </FormField>

            {error && <Alert tone="error" message={error} />}
            {fileProblem && <Alert tone="error" message={`${t('import.fileError')} ${fileProblem}`} />}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                {tCommon('actions.cancel')}
              </Button>
              <Button type="button" disabled={importStudents.isPending} onClick={start}>
                {importStudents.isPending ? t('import.importing') : t('import.start')}
              </Button>
            </DialogFooter>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}
