// Hand-written until `npm run api:generate` is run against the API with the Phase 4 endpoints; then these can be replaced by
// the generated models. The shapes mirror the DTOs of the API.

export const GRADE_COMPONENTS = ['Assignment', 'Quiz', 'Midterm', 'Final'] as const
export type GradeComponent = (typeof GRADE_COMPONENTS)[number]

/** Assignment scores come from the scored assignments; the teacher types the other three. */
export const ENTERABLE_COMPONENTS = ['Quiz', 'Midterm', 'Final'] as const
export type EnterableComponent = (typeof ENTERABLE_COMPONENTS)[number]

export type AchievementStatus = 'Draft' | 'Accepted'

export interface GradeWeightItem {
  component: GradeComponent
  weightPercent: number
}

export interface GradeWeights {
  subjectId: string
  weights: GradeWeightItem[]
  /** True while the subject has no weights of its own and the default applies. */
  isDefault: boolean
}

export interface ComponentScore {
  component: GradeComponent
  average: number | null
  count: number
}

export interface GradeBookRow {
  studentId: string
  studentName: string
  nisn: string
  components: ComponentScore[]
  finalGrade: number | null
  band: string | null
  isPassing: boolean | null
  description: string | null
  /** Draft until the teacher accepts it; only accepted texts reach the report card. */
  descriptionStatus: AchievementStatus | null
  descriptionIsAiDrafted: boolean
}

export interface GradeBook {
  classroomId: string
  subjectId: string
  termId: string
  minimumPassingScore: number
  isPublished: boolean
  weights: GradeWeights
  rows: GradeBookRow[]
}

export interface GradeBookParams {
  classroomId: string
  subjectId: string
  termId: string
  /** Show only the scores linked to this learning objective in the component columns. */
  learningObjectiveId?: string
}

export interface EnterGradeCommand {
  studentId: string
  subjectId: string
  termId: string
  component: EnterableComponent
  score: number
  learningObjectiveId?: string
}

export interface WriteDescriptionCommand {
  studentId: string
  subjectId: string
  termId: string
  body: string
}

export interface PublicationCommand {
  classroomId: string
  termId: string
}

export interface GeneratedReportCards {
  classroomId: string
  termId: string
  count: number
  generatedAt: string
}

export interface GradeBand {
  id: string
  label: string
  minScore: number
}

export interface GradeBandInput {
  label: string
  minScore: number
}

export interface LearningObjective {
  id: string
  subjectId: string
  code: string
  description: string
}

export interface LearningObjectiveInput {
  code: string
  description: string
}

export interface DescriptionCommand {
  studentId: string
  subjectId: string
  termId: string
}

export interface AchievementDescription {
  studentId: string
  subjectId: string
  termId: string
  body: string
  status: AchievementStatus
  isAiDrafted: boolean
}

export interface DraftedDescriptions {
  drafted: number
  alreadyHadText: number
  noScores: number
  failed: number
  /** The error code when the run stopped early (daily limit or monthly budget). */
  stoppedBy: string | null
}
