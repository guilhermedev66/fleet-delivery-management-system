import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../lib/api/client'
import * as authApi from '../../lib/api/auth'
import { LoginPage } from './LoginPage'

function renderLoginPage() {
  const queryClient = new QueryClient({
    defaultOptions: { mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/login']}>
        <LoginPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('LoginPage', () => {
  it('shows an inline error and re-enables the form when login fails', async () => {
    vi.spyOn(authApi, 'login').mockRejectedValue(new ApiError(401, 'Unauthorized'))
    const user = userEvent.setup()

    renderLoginPage()

    await user.type(screen.getByLabelText(/email/i), 'driver@example.com')
    await user.type(screen.getByLabelText(/password/i), 'wrong-password')
    await user.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/invalid email or password/i)
    expect(screen.getByRole('button', { name: /sign in/i })).not.toBeDisabled()
  })
})
