import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { applyTheme, getInitialTheme } from './lib/theme'
import './index.css'

// Apply the stored/preferred theme before the first paint to avoid a
// light->dark flash on load.
applyTheme(getInitialTheme())

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
