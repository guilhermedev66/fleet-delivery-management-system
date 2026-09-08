import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// vite.config.ts doesn't set test.globals, so testing-library's automatic
// afterEach(cleanup) never registers — without this, multiple render() calls
// in the same test file stack up in the jsdom document instead of each test
// getting a fresh tree.
afterEach(() => {
  cleanup()
})
