import { defineConfig } from 'orval'

// Generates the typed API client from the OpenAPI document of the running API (Development or Demo environment).
// Run the API, then: npm run api:generate
export default defineConfig({
  schomans: {
    input: { target: process.env.OPENAPI_URL ?? 'http://localhost:5080/openapi/v1.json' },
    output: {
      target: 'src/api/generated/schomans.ts',
      schemas: 'src/api/generated/model',
      client: 'axios-functions',
      mode: 'single',
      clean: true,
      override: {
        mutator: { path: 'src/shared/lib/http.ts', name: 'customInstance' },
      },
    },
  },
})
