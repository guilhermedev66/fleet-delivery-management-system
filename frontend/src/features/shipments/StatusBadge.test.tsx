import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ShipmentStatusBadge } from './StatusBadge'

describe('ShipmentStatusBadge', () => {
  it('renders a success variant for a terminal-success status', () => {
    render(<ShipmentStatusBadge status="Delivered" />)
    const badge = screen.getByText('Delivered')
    expect(badge.className).toContain('badge-success-bg')
  })

  it('renders a danger variant for a terminal-failure/cancelled status', () => {
    render(<ShipmentStatusBadge status="Cancelled" />)
    const badge = screen.getByText('Cancelled')
    expect(badge.className).toContain('badge-danger-bg')
  })

  it('renders a neutral variant for an in-progress status', () => {
    render(<ShipmentStatusBadge status="InTransit" />)
    const badge = screen.getByText('In Transit')
    expect(badge.className).toContain('badge-neutral-bg')
  })
})
