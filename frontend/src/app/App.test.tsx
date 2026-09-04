import { render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from './App'

describe('App', () => {
  beforeEach(() => {
    // No cookie/session in a test environment — stub fetch so the silent
    // refresh on mount fails fast and deterministically instead of trying
    // (and hanging on) a real network call.
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new Error('network unavailable in test env')))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('redirects an unauthenticated visitor to the login page', async () => {
    render(<App />)

    expect(
      await screen.findByRole('heading', { name: /fleet & delivery management system/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /sign in/i })).toBeInTheDocument()
  })
})
