import { createBrowserRouter } from 'react-router-dom'
import { UnderConstructionPage } from './UnderConstructionPage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <UnderConstructionPage />,
  },
])
