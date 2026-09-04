import { createBrowserRouter } from 'react-router-dom'
import { PlaceholderPage } from '../components/PlaceholderPage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { LoginPage } from '../features/auth/LoginPage'
import { AppShell } from './AppShell'
import { ProtectedRoute } from './ProtectedRoute'

export const router = createBrowserRouter([
  {
    path: '/login',
    element: <LoginPage />,
  },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <DashboardPage /> },
          { path: 'shipments', element: <PlaceholderPage title="Shipments" /> },
          { path: 'dispatch', element: <PlaceholderPage title="Dispatch Board" /> },
          { path: 'routes', element: <PlaceholderPage title="Routes" /> },
          { path: 'drivers', element: <PlaceholderPage title="Drivers" /> },
          { path: 'vehicles', element: <PlaceholderPage title="Vehicles" /> },
          { path: 'tracking', element: <PlaceholderPage title="Tracking" /> },
          { path: 'incidents', element: <PlaceholderPage title="Incidents" /> },
          { path: 'reports', element: <PlaceholderPage title="Reports" /> },
          { path: 'settings', element: <PlaceholderPage title="Settings" /> },
        ],
      },
    ],
  },
])
