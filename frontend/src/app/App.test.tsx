import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { App } from './App'

describe('App', () => {
  it('renders the app shell without crashing', () => {
    render(<App />)

    expect(
      screen.getByRole('heading', { name: /fleet & delivery management system/i }),
    ).toBeInTheDocument()
  })
})
