import { useEffect, useState } from 'react'
import { applyTheme, getInitialTheme, type Theme } from '../lib/theme'

export function useTheme() {
  const [theme, setTheme] = useState<Theme>(() => {
    const current = document.documentElement.dataset.theme
    return current === 'light' || current === 'dark' ? current : getInitialTheme()
  })

  useEffect(() => {
    applyTheme(theme)
  }, [theme])

  function toggleTheme() {
    setTheme((previous) => (previous === 'dark' ? 'light' : 'dark'))
  }

  return { theme, toggleTheme }
}
