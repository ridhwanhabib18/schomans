import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDate } from '@/shared/lib/format'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { Skeleton } from '@/shared/ui/skeleton'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateNote, useDeleteNote, useTeacherNotes } from '../api/progress'
import { NOTE_MAX_LENGTH } from '../api/types'
import { progressErrorText } from '../errors'
import '../i18n'

/** The notes teachers keep about one student, newest first, with a form for a new one. Only the author can delete a note. */
export function NotesDialog({ studentId, studentName, onClose }: { studentId: string; studentName: string; onClose: () => void }) {
  const { t } = useTranslation('progress')
  const { t: tCommon } = useTranslation()
  const showToast = useToastStore(selectShowToast)
  const notes = useTeacherNotes(studentId)
  const create = useCreateNote()
  const remove = useDeleteNote()
  const [body, setBody] = useState('')
  const [error, setError] = useState<string>()

  const fail = (e: unknown) => setError(progressErrorText(e, t, tCommon('errors.generic')))

  function add() {
    setError(undefined)
    create.mutate(
      { studentId, body: body.trim() },
      {
        onSuccess: () => {
          setBody('')
          showToast({ kind: 'success', title: t('notes.added') })
        },
        onError: fail,
      },
    )
  }

  function drop(id: string) {
    setError(undefined)
    remove.mutate(id, { onSuccess: () => showToast({ kind: 'success', title: t('notes.deleted') }), onError: fail })
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-xl">
        <DialogHeader>
          <DialogTitle>{t('notes.dialogTitle', { name: studentName })}</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          {error && <Alert tone="error" message={error} />}
          <form
            noValidate
            className="flex flex-col gap-3"
            onSubmit={(event) => {
              event.preventDefault()
              add()
            }}
          >
            <FormField label={t('notes.label')}>
              <Textarea rows={3} maxLength={NOTE_MAX_LENGTH} placeholder={t('notes.placeholder')} value={body} onChange={(event) => setBody(event.target.value)} />
            </FormField>
            <div>
              <Button type="submit" disabled={create.isPending || body.trim() === ''}>
                {t('notes.add')}
              </Button>
            </div>
          </form>

          {notes.isPending ? (
            <Skeleton className="h-16 w-full" />
          ) : notes.isError ? (
            <Alert tone="error" message={t('notes.error')} />
          ) : notes.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('notes.empty')}</p>
          ) : (
            <ul className="flex max-h-72 flex-col gap-3 overflow-y-auto">
              {notes.data.items.map((note) => (
                <li key={note.id} className="flex items-start justify-between gap-3 rounded-md border border-border p-3 text-sm">
                  <div className="min-w-0">
                    <p className="whitespace-pre-line">{note.body}</p>
                    <p className="mt-1 text-xs text-muted-foreground">{t('notes.by', { name: note.authorName, date: formatDate(note.date) })}</p>
                  </div>
                  {note.isMine && (
                    <Button variant="ghost" size="icon" disabled={remove.isPending} onClick={() => drop(note.id)}>
                      <Trash2 aria-hidden="true" />
                      <span className="sr-only">{t('notes.delete')}</span>
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {tCommon('actions.close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
