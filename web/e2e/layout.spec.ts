import { expect, test } from '@playwright/test'

// Runs in both projects: the desktop layout has a sidebar, the phone layout has a bottom navigation.
test('the app shell adapts to the screen size', async ({ page, isMobile }) => {
  await page.goto('/login')
  await page.getByRole('button', { name: /^Parent/ }).click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Parent dashboard')

  const navigations = page.getByRole('navigation', { name: 'Main navigation' })
  if (isMobile) {
    await expect(page.locator('aside')).toBeHidden()
    await expect(navigations.last()).toBeVisible()
    const box = await navigations.last().boundingBox()
    expect(box?.y).toBeGreaterThan(600) // pinned to the bottom of a 844 px screen
  } else {
    await expect(page.locator('aside')).toBeVisible()
    await expect(navigations.first()).toBeVisible()
  }

  // No horizontal scrolling at any size (PRD: usable from 360 px).
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
  await page.screenshot({ path: `test-results/screens/layout-${isMobile ? 'mobile' : 'desktop'}.png`, fullPage: true })
})

test('the theme can be switched to dark and back', async ({ page }) => {
  await page.goto('/login')
  await page.getByRole('button', { name: /^Student/ }).click()
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Student dashboard')

  const html = page.locator('html')
  const toggle = page.getByRole('button', { name: /^Theme:/ })
  // system -> light -> dark
  await toggle.click()
  await toggle.click()
  await expect(html).toHaveClass(/dark/)
  await toggle.click() // back to system, follows the emulated light color scheme
})
