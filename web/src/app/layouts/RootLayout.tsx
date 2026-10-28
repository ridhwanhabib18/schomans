import { Outlet } from 'react-router'
import { Toaster } from '@/shared/ui/toaster'

/** Wraps every route. The toasts live here, inside the router, because clicking one navigates to its page. */
export function RootLayout() {
  return (
    <>
      <Outlet />
      <Toaster />
    </>
  )
}
