import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { SILENT_CALLBACK_PATH, getUserManager } from '@/features/auth/api/authService'
import { AppProviders } from '@/app/providers'
import { createAppRouter } from '@/app/router'
import '@/shared/lib/i18n'
import './index.css'

if (window.location.pathname === SILENT_CALLBACK_PATH) {
  // This page is loaded in a hidden iframe by the silent session check: hand the result to the parent and stop.
  void getUserManager().signinSilentCallback()
} else {
  const root = document.getElementById('root')
  if (!root) {
    throw new Error('The #root element is missing from index.html.')
  }

  createRoot(root).render(
    <StrictMode>
      <AppProviders>
        <RouterProvider router={createAppRouter()} />
      </AppProviders>
    </StrictMode>,
  )
}
