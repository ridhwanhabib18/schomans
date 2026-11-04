import { cleanup, configure } from '@testing-library/react'
import { afterEach } from 'vitest'

// The suite runs 60+ jsdom files in parallel. The default wait of findBy and waitFor (1 s) is too tight when the machine is busy: a slow
// render then fails a test that is right. A wait that really never ends still fails, only later.
configure({ asyncUtilTimeout: 5000 })

// Testing Library only unmounts automatically when test globals are on; they are off here.
afterEach(() => {
  cleanup()
})
