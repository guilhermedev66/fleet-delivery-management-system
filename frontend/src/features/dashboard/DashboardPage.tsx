import { useAuthStore } from '../auth/authStore'
import { useLogout } from '../auth/hooks'

export function DashboardPage() {
  const user = useAuthStore((state) => state.user)
  const logout = useLogout()

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-xl font-semibold">Dashboard</h1>

      {user && (
        <div className="rounded-lg border border-[var(--color-border)] bg-[var(--color-bg-subtle)] p-4">
          <p className="text-sm text-[var(--color-text-muted)]">Signed in as</p>
          <p className="text-lg font-medium">{user.fullName}</p>
          <p className="text-sm text-[var(--color-text-muted)]">
            {user.email} · {user.role}
          </p>
        </div>
      )}

      <p className="text-sm text-[var(--color-text-muted)]">
        Dashboard metrics will land once the Reporting module and its API exist — no fabricated
        numbers here in the meantime.
      </p>

      <button
        type="button"
        onClick={() => logout.mutate()}
        disabled={logout.isPending}
        className="w-fit rounded-md border border-[var(--color-border)] px-4 py-2 text-sm font-medium hover:bg-[var(--color-bg-subtle)] disabled:opacity-50"
      >
        {logout.isPending ? 'Logging out…' : 'Log out'}
      </button>
    </div>
  )
}
