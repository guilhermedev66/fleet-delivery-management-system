import { useState } from 'react'
import { NavLink, Outlet } from 'react-router-dom'
import { cx } from '../lib/utils'
import { useTheme } from './useTheme'

const NAV_ITEMS: Array<{ to: string; label: string }> = [
  { to: '/', label: 'Dashboard' },
  { to: '/shipments', label: 'Shipments' },
  { to: '/dispatch', label: 'Dispatch Board' },
  { to: '/routes', label: 'Routes' },
  { to: '/drivers', label: 'Drivers' },
  { to: '/vehicles', label: 'Vehicles' },
  { to: '/tracking', label: 'Tracking' },
  { to: '/incidents', label: 'Incidents' },
  { to: '/reports', label: 'Reports' },
  { to: '/settings', label: 'Settings' },
]

function NavLinks({ onNavigate }: { onNavigate?: () => void }) {
  return (
    <nav className="flex flex-col gap-1 p-3">
      {NAV_ITEMS.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={item.to === '/'}
          onClick={onNavigate}
          className={({ isActive }) =>
            cx(
              'rounded-md px-3 py-2 text-sm font-medium transition-colors',
              isActive
                ? 'bg-[var(--color-accent)] text-[var(--color-accent-text)]'
                : 'text-[var(--color-text)] hover:bg-[var(--color-bg-subtle)]',
            )
          }
        >
          {item.label}
        </NavLink>
      ))}
    </nav>
  )
}

export function AppShell() {
  const [drawerOpen, setDrawerOpen] = useState(false)
  const { theme, toggleTheme } = useTheme()

  return (
    <div className="flex min-h-svh">
      {/* Desktop sidebar */}
      <aside className="hidden w-60 shrink-0 border-r border-[var(--color-border)] bg-[var(--color-bg-subtle)] md:block">
        <div className="border-b border-[var(--color-border)] px-4 py-4">
          <p className="text-sm font-semibold">Fleet & Delivery</p>
        </div>
        <NavLinks />
      </aside>

      {/* Mobile drawer */}
      {drawerOpen && (
        <div className="fixed inset-0 z-40 md:hidden">
          <button
            type="button"
            aria-label="Close navigation"
            className="absolute inset-0 bg-black/40"
            onClick={() => setDrawerOpen(false)}
          />
          <aside className="relative z-50 h-full w-64 bg-[var(--color-bg)] shadow-lg">
            <div className="flex items-center justify-between border-b border-[var(--color-border)] px-4 py-4">
              <p className="text-sm font-semibold">Fleet & Delivery</p>
              <button
                type="button"
                aria-label="Close navigation"
                className="text-[var(--color-text-muted)]"
                onClick={() => setDrawerOpen(false)}
              >
                ✕
              </button>
            </div>
            <NavLinks onNavigate={() => setDrawerOpen(false)} />
          </aside>
        </div>
      )}

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-[var(--color-border)] px-4 py-3">
          <button
            type="button"
            aria-label="Open navigation"
            className="rounded-md border border-[var(--color-border)] px-2 py-1 text-sm md:hidden"
            onClick={() => setDrawerOpen(true)}
          >
            ☰
          </button>
          <span className="hidden text-sm font-medium md:block">
            Fleet & Delivery Management System
          </span>
          <button
            type="button"
            onClick={toggleTheme}
            className="rounded-md border border-[var(--color-border)] px-3 py-1 text-sm hover:bg-[var(--color-bg-subtle)]"
            aria-label="Toggle color theme"
          >
            {theme === 'dark' ? 'Light mode' : 'Dark mode'}
          </button>
        </header>

        <main className="flex-1 overflow-auto p-4 md:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
