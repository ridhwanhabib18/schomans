import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/shared/ui/dialog'
import { Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import '../i18n'

interface NoteDialogProps {
  title: string
  description: string
  label: string
  confirmLabel: string
  pending: boolean
  onConfirm: (note: string) => void
  onClose: () => void
}

/** Asks for a note before something is refused: the applicant reads it, so it cannot be empty. Rendered only while open. */
export function NoteDialog({ title, description, label, confirmLabel, pending, onConfirm, onClose }: NoteDialogProps) {
  const { t: tCommon } = useTranslation()
  const [note, setNote] = useState('')

  function submit(event: FormEvent) {
    event.preventDefault()
    if (note.trim() !== '') {
      onConfirm(note.trim())
    }
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <form onSubmit={submit} noValidate className="flex flex-col gap-4">
          <FormField label={label} required>
            <Textarea value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />
          </FormField>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              {tCommon('actions.cancel')}
            </Button>
            <Button type="submit" className="bg-destructive text-destructive-foreground" disabled={pending || note.trim() === ''}>
              {confirmLabel}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
