import tseslint from 'typescript-eslint'

// Architecture rules from CLAUDE.md, enforced by the linter:
//  - features never import from each other (shared code goes to src/shared)
//  - axios is only used in src/shared/lib and in features/*/api; components go through TanStack Query hooks
const features = ['auth', 'dashboard', 'landing', 'masterdata', 'news', 'announcements', 'notifications']

const otherFeatures = (feature) =>
  features.filter((f) => f !== feature).map((f) => `@/features/${f}`)

export default tseslint.config(
  {
    ignores: ['dist', 'coverage', 'playwright-report', 'test-results', 'src/api/generated', 'node_modules'],
  },
  ...tseslint.configs.recommended,
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/shared/lib/**', 'src/features/*/api/**', 'src/test/**', 'src/**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'axios',
              message: 'Do not call axios from components. Use a TanStack Query hook in features/*/api.',
            },
          ],
        },
      ],
    },
  },
  ...features.map((feature) => ({
    files: [`src/features/${feature}/**/*.{ts,tsx}`],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: otherFeatures(feature).map((group) => ({
            group: [group, `${group}/*`],
            message: 'Features never import from each other. Move shared code to src/shared.',
          })),
          ...(feature === 'auth' ? {} : { paths: [{ name: 'axios', message: 'Use a TanStack Query hook in features/*/api.' }] }),
        },
      ],
    },
  })),
  {
    // axios is allowed inside a feature's api folder
    files: ['src/features/*/api/**/*.{ts,tsx}'],
    rules: { 'no-restricted-imports': 'off' },
  },
)
