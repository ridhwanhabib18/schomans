import { FileText, Paperclip, X } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import type { AssignmentDto, NewAttachment, TeachingOptionDto } from '@/api/generated/model'
import { fieldError, isConcurrencyConflict, parseProblem, type Problem } from '@/shared/lib/problem'
import { isoToWibLocal, wibLocalToIso } from '@/shared/lib/time'
import { Alert } from '@/shared/ui/alert'
import { Button } from '@/shared/ui/button'
import { Checkbox, Input, Select, Textarea } from '@/shared/ui/form-controls'
import { FormField } from '@/shared/ui/form-field'
import { selectShowToast, useToastStore } from '@/stores/useToastStore'
import { useCreateAssignment, useUpdateAssignment, useUploadAttachment } from '../api/assignments'
import { ATTACHMENT_TYPES, checkFile, MAX_ATTACHMENTS } from '../files'
import '../i18n'

interface AssignmentFormProps {
  /** The saved assignment, or undefined for a new one. */
  assignment: AssignmentDto | undefined
  options: TeachingOptionDto[]
  onReload: () => void
}

/** A day from now at 23:59 school time: a sensible first due date. */
function defaultDue(): string {
  const tomorrow = isoToWibLocal(new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString())
  return `${tomorrow.slice(0, 10)}T23:59`
}

/**
 * Give or change an assignment. The subject and its classrooms come from the teacher's timetable. Attachments are
 * uploaded to storage the moment they are picked, so saving only sends their keys.
 */
export function AssignmentForm({ assignment, options, onReload }: AssignmentFormProps) {
  const { t } = useTranslation('assignments')
  const { t: tCommon } = useTranslation()
  const navigate = useNavigate()
  const showToast = useToastStore(selectShowToast)
  const create = useCreateAssignment()
  const update = useUpdateAssignment()
  const upload = useUploadAttachment()
  const input = useRef<HTMLInputElement>(null)

  const subjects = [...new Map(options.map((o) => [o.subjectId, o.subjectName])).entries()]
  const [title, setTitle] = useState(assignment?.title ?? '')
  const [description, setDescription] = useState(assignment?.description ?? '')
  const [subjectId, setSubjectId] = useState(assignment?.subjectId ?? (subjects.length === 1 ? subjects[0]![0] : ''))
  const [classroomIds, setClassroomIds] = useState<string[]>(assignment?.classrooms.map((c) => c.id) ?? [])
  const [due, setDue] = useState(assignment ? isoToWibLocal(assignment.dueAt) : defaultDue())
  const [kept, setKept] = useState(assignment?.attachments ?? [])
  const [added, setAdded] = useState<NewAttachment[]>([])
  const [fileError, setFileError] = useState<string>()
  const [problem, setProblem] = useState<Problem>()

  // A saved assignment keeps its classrooms in the list even if the timetable changed since.
  const classroomChoices = [
    ...options.filter((o) => o.subjectId === subjectId).map((o) => ({ id: o.classroomId, name: o.classroomName })),
    ...(assignment?.classrooms.filter((c) => !options.some((o) => o.subjectId === subjectId && o.classroomId === c.id)) ?? []),
  ]
  const saving = create.isPending || update.isPending

  async function addFiles(files: FileList | null) {
    setFileError(undefined)
    // Counted here, not read from state: several files picked at once are handled before the state updates.
    let count = kept.length + added.length
    for (const file of Array.from(files ?? [])) {
      if (count >= MAX_ATTACHMENTS) {
        setFileError(t('form.tooMany'))
        return
      }

      const refused = checkFile(file, ATTACHMENT_TYPES)
      if (refused) {
        setFileError(t(refused === 'type' ? 'form.wrongType' : 'form.wrongSize', { name: file.name }))
        continue
      }

      try {
        const attachment = await upload.mutateAsync(file)
        count++
        setAdded((current) => [...current, attachment])
      } catch {
        setFileError(t('form.uploadFailed', { name: file.name }))
      }
    }
  }

  function save() {
    setProblem(undefined)
    const dueAt = due ? wibLocalToIso(due) : ''
    const done = (id: string, message: string) => {
      showToast({ kind: 'success', title: message })
      void navigate(`/assignments/${id}`)
    }
    const failed = (error: unknown) => setProblem(parseProblem(error))

    // mutateAsync, not mutate with callbacks: saving refreshes the assignment, which can rebuild this form, and callbacks
    // passed to mutate are dropped once the component that called it is gone.
    const saved = assignment
      ? update.mutateAsync({
          id: assignment.id,
          command: {
            id: assignment.id,
            title,
            description: description || null,
            dueAt,
            classroomIds,
            keepAttachmentIds: kept.map((a) => a.id),
            newAttachments: added,
            rowVersion: assignment.rowVersion,
          },
        })
      : create.mutateAsync({ title, description: description || null, dueAt, subjectId, classroomIds, attachments: added })

    saved.then((result) => done(result.id, assignment ? t('form.saved') : t('form.created')), failed)
  }

  const generalError = problem && Object.keys(problem.fieldErrors).length === 0 && !isConcurrencyConflict(problem)

  return (
    <form
      noValidate
      className="flex max-w-3xl flex-col gap-5"
      onSubmit={(event) => {
        event.preventDefault()
        save()
      }}
    >
      {isConcurrencyConflict(problem) && (
        <Alert
          tone="error"
          message={t('form.conflict')}
          action={
            <Button type="button" variant="outline" size="sm" onClick={onReload}>
              {t('form.reload')}
            </Button>
          }
        />
      )}
      {generalError && <Alert tone="error" message={t(`errors.${problem?.code ?? 'unknown'}`, { defaultValue: problem?.title ?? tCommon('errors.generic') })} />}

      <FormField label={t('form.title')} required error={fieldError(problem, 'title')}>
        <Input value={title} maxLength={200} onChange={(event) => setTitle(event.target.value)} />
      </FormField>

      <FormField label={t('form.subject')} required error={fieldError(problem, 'subjectId')}>
        <Select
          value={subjectId}
          disabled={assignment !== undefined}
          onChange={(event) => {
            setSubjectId(event.target.value)
            setClassroomIds([])
          }}
        >
          <option value="">{t('form.subjectPlaceholder')}</option>
          {subjects.map(([id, name]) => (
            <option key={id} value={id}>
              {name}
            </option>
          ))}
        </Select>
      </FormField>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium">
          {t('form.classrooms')}
          <span aria-hidden="true" className="text-destructive"> *</span>
        </legend>
        <p className="text-xs text-muted-foreground">{t('form.classroomsHint')}</p>
        <div className="flex flex-wrap gap-3">
          {classroomChoices.map((classroom) => (
            <label key={classroom.id} className="flex items-center gap-2 text-sm">
              <Checkbox
                checked={classroomIds.includes(classroom.id)}
                onChange={(event) =>
                  setClassroomIds((current) => (event.target.checked ? [...current, classroom.id] : current.filter((id) => id !== classroom.id)))
                }
              />
              {classroom.name}
            </label>
          ))}
        </div>
        {fieldError(problem, 'classroomIds') && (
          <p role="alert" className="text-xs text-destructive">
            {fieldError(problem, 'classroomIds')}
          </p>
        )}
      </fieldset>

      <FormField label={t('form.due')} required error={fieldError(problem, 'dueAt')}>
        <Input type="datetime-local" value={due} onChange={(event) => setDue(event.target.value)} className="max-w-64" />
      </FormField>

      <FormField label={t('form.description')} hint={t('form.descriptionHint')} error={fieldError(problem, 'description')}>
        <Textarea value={description} rows={6} maxLength={10_000} onChange={(event) => setDescription(event.target.value)} />
      </FormField>

      <div className="flex flex-col gap-2">
        <span className="text-sm font-medium">{t('form.attachments')}</span>
        <p className="text-xs text-muted-foreground">{t('form.attachmentsHint')}</p>
        <ul className="flex flex-col gap-1">
          {kept.map((file) => (
            <li key={file.id} className="flex items-center gap-2 text-sm">
              <FileText aria-hidden="true" className="size-4" />
              {file.fileName}
              <Button type="button" variant="ghost" size="sm" onClick={() => setKept((current) => current.filter((f) => f.id !== file.id))}>
                <X aria-hidden="true" />
                <span className="sr-only">{t('form.removeFile', { name: file.fileName })}</span>
              </Button>
            </li>
          ))}
          {added.map((file) => (
            <li key={file.key} className="flex items-center gap-2 text-sm">
              <FileText aria-hidden="true" className="size-4" />
              {file.fileName}
              <Button type="button" variant="ghost" size="sm" onClick={() => setAdded((current) => current.filter((f) => f.key !== file.key))}>
                <X aria-hidden="true" />
                <span className="sr-only">{t('form.removeFile', { name: file.fileName })}</span>
              </Button>
            </li>
          ))}
        </ul>
        {upload.isPending && <p role="status" className="text-sm text-muted-foreground">{t('form.uploading', { name: upload.variables?.name ?? '' })}</p>}
        {fileError && <p role="alert" className="text-sm text-destructive">{fileError}</p>}
        <input
          ref={input}
          type="file"
          multiple
          accept={ATTACHMENT_TYPES.join(',')}
          className="sr-only"
          aria-label={t('form.addFile')}
          onChange={(event) => {
            const files = event.target.files
            void addFiles(files).finally(() => {
              event.target.value = ''
            })
          }}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="self-start"
          disabled={upload.isPending || kept.length + added.length >= MAX_ATTACHMENTS}
          onClick={() => input.current?.click()}
        >
          <Paperclip aria-hidden="true" />
          {t('form.addFile')}
        </Button>
      </div>

      <Button type="submit" className="self-start" disabled={saving || upload.isPending}>
        {saving ? tCommon('actions.saving') : assignment ? t('form.save') : t('form.create')}
      </Button>
    </form>
  )
}
