import { Pencil, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/shared/ui/button'

interface RowActionsProps {
  /** Who the row is about, so a screen reader hears "Edit: Rina Putri" and not just "Edit". */
  name: string
  onEdit: () => void
  onDelete: () => void
}

export function RowActions({ name, onEdit, onDelete }: RowActionsProps) {
  const { t } = useTranslation()

  return (
    <div className="flex justify-end gap-1">
      <Button variant="ghost" size="sm" onClick={onEdit}>
        <Pencil aria-hidden="true" />
        <span className="sr-only md:not-sr-only">{t('actions.edit')}</span>
        <span className="sr-only">: {name}</span>
      </Button>
      <Button variant="ghost" size="sm" onClick={onDelete}>
        <Trash2 aria-hidden="true" />
        <span className="sr-only md:not-sr-only">{t('actions.delete')}</span>
        <span className="sr-only">: {name}</span>
      </Button>
    </div>
  )
}
