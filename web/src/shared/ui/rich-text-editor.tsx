import { EditorContent, useEditor, type Editor } from '@tiptap/react'
import StarterKit from '@tiptap/starter-kit'
import { Bold, Heading2, Heading3, Italic, Link2, List, ListOrdered, Quote, Redo2, Underline as UnderlineIcon, Undo2 } from 'lucide-react'
import { useEffect, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/shared/lib/utils'

interface RichTextEditorProps {
  /** The HTML of the text. Empty string for nothing. */
  value: string
  onChange: (html: string) => void
  /** Accessible name of the editing area. */
  label: string
  invalid?: boolean
  disabled?: boolean
}

function ToolbarButton({ label, active, disabled, onClick, children }: { label: string; active?: boolean; disabled?: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      title={label}
      aria-label={label}
      aria-pressed={active}
      disabled={disabled}
      onClick={onClick}
      className={cn('rounded p-1.5 hover:bg-muted disabled:opacity-40', active && 'bg-muted text-primary')}
    >
      {children}
    </button>
  )
}

function Toolbar({ editor }: { editor: Editor }) {
  const { t } = useTranslation()

  function setLink() {
    const previous = (editor.getAttributes('link') as { href?: string }).href ?? ''
    const url = window.prompt(t('editor.linkPrompt'), previous)
    if (url === null) {
      return
    }

    if (url.trim() === '') {
      editor.chain().focus().extendMarkRange('link').unsetLink().run()
    } else {
      editor.chain().focus().extendMarkRange('link').setLink({ href: url.trim() }).run()
    }
  }

  return (
    <div role="toolbar" aria-label={t('editor.toolbar')} className="flex flex-wrap items-center gap-0.5 border-b border-border p-1">
      <ToolbarButton label={t('editor.bold')} active={editor.isActive('bold')} onClick={() => editor.chain().focus().toggleBold().run()}>
        <Bold aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.italic')} active={editor.isActive('italic')} onClick={() => editor.chain().focus().toggleItalic().run()}>
        <Italic aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.underline')} active={editor.isActive('underline')} onClick={() => editor.chain().focus().toggleUnderline().run()}>
        <UnderlineIcon aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.heading2')} active={editor.isActive('heading', { level: 2 })} onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}>
        <Heading2 aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.heading3')} active={editor.isActive('heading', { level: 3 })} onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}>
        <Heading3 aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.bulletList')} active={editor.isActive('bulletList')} onClick={() => editor.chain().focus().toggleBulletList().run()}>
        <List aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.orderedList')} active={editor.isActive('orderedList')} onClick={() => editor.chain().focus().toggleOrderedList().run()}>
        <ListOrdered aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.quote')} active={editor.isActive('blockquote')} onClick={() => editor.chain().focus().toggleBlockquote().run()}>
        <Quote aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.link')} active={editor.isActive('link')} onClick={setLink}>
        <Link2 aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <span aria-hidden="true" className="mx-1 h-5 w-px bg-border" />
      <ToolbarButton label={t('editor.undo')} disabled={!editor.can().undo()} onClick={() => editor.chain().focus().undo().run()}>
        <Undo2 aria-hidden="true" className="size-4" />
      </ToolbarButton>
      <ToolbarButton label={t('editor.redo')} disabled={!editor.can().redo()} onClick={() => editor.chain().focus().redo().run()}>
        <Redo2 aria-hidden="true" className="size-4" />
      </ToolbarButton>
    </div>
  )
}

/**
 * The rich-text editor for news and announcements (Tiptap). It produces HTML; the API sanitizes it before storing,
 * so what the editor sends is never trusted.
 */
export function RichTextEditor({ value, onChange, label, invalid, disabled }: RichTextEditorProps) {
  const editor = useEditor({
    extensions: [StarterKit.configure({ link: { openOnClick: false, autolink: true } })],
    content: value,
    editable: !disabled,
    // The toolbar shows which formats are on at the cursor; without this it only re-renders when the text changes.
    shouldRerenderOnTransaction: true,
    editorProps: {
      attributes: { role: 'textbox', 'aria-multiline': 'true', 'aria-label': label, class: 'rich-text min-h-48 px-3 py-2 focus:outline-none' },
    },
    // An empty editor reports "<p></p>"; the form should see an empty string.
    onUpdate: ({ editor: current }) => onChange(current.isEmpty ? '' : current.getHTML()),
  })

  // The text can change from outside (the record finished loading).
  useEffect(() => {
    if (editor && value !== (editor.isEmpty ? '' : editor.getHTML())) {
      editor.commands.setContent(value, { emitUpdate: false })
    }
  }, [editor, value])

  useEffect(() => {
    editor?.setEditable(!disabled)
  }, [editor, disabled])

  return (
    <div className={cn('overflow-hidden rounded-md border bg-card', invalid ? 'border-destructive' : 'border-border')}>
      {editor && !disabled && <Toolbar editor={editor} />}
      <EditorContent editor={editor} />
    </div>
  )
}
