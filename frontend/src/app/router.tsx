import { createBrowserRouter } from 'react-router-dom'
import { PlaceholderPage } from '../components/PlaceholderPage'
import { DashboardPage } from '../features/dashboard/DashboardPage'
import { LoginPage } from '../features/auth/LoginPage'
import { DispatchBoardPage } from '../features/dispatch/DispatchBoardPage'
import { CreateShipmentPage } from '../features/shipments/CreateShipmentPage'
import { ShipmentDetailPage } from '../features/shipments/ShipmentDetailPage'
import { ShipmentsListPage } from '../features/shipments/ShipmentsListPage'
import { RegisterVehiclePage } from '../features/vehicles/RegisterVehiclePage'
import { VehiclesListPage } from '../features/vehicles/VehiclesListPage'
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
          { path: 'shipments', element: <ShipmentsListPage /> },
          { path: 'shipments/new', element: <CreateShipmentPage /> },
          { path: 'shipments/:id', element: <ShipmentDetailPage /> },
          { path: 'dispatch', element: <DispatchBoardPage /> },
          { path: 'routes', element: <PlaceholderPage title="Routes" /> },
          { path: 'drivers', element: <PlaceholderPage title="Drivers" /> },
          { path: 'vehicles', element: <VehiclesListPage /> },
          { path: 'vehicles/new', element: <RegisterVehiclePage /> },
          { path: 'tracking', element: <PlaceholderPage title="Tracking" /> },
          { path: 'incidents', element: <PlaceholderPage title="Incidents" /> },
          { path: 'reports', element: <PlaceholderPage title="Reports" /> },
          { path: 'settings', element: <PlaceholderPage title="Settings" /> },
        ],
      },
    ],
  },
])
