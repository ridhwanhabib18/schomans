import { test } from '@playwright/test'
import { goInApp, roles, settle, shoot, sizes } from './support.ts'

// Visits every page a role can open, at desktop and phone size, and stores a picture of each in docs/assets/screenshots/<role>/.
// The pages come from the navigation of the app itself, so a new menu item is captured without a change here. From each list page
// the first detail page (a link to /<list>/<id>) is captured too. Only the fictional demo data is on screen.
const detailLink = (list: string) => new RegExp(`^${list.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}/[0-9a-f-]{36}$`)

test('the public pages, as a guest sees them', async ({ page }) => {
  const guest = 'guest'
  const routes = ['/', '/news', '/login']
  for (const size of ['desktop', 'mobile'] as const) {
    await page.setViewportSize(sizes[size])
    for (const route of routes) {
      await page.goto(route)
      await settle(page)
      await shoot(page, guest, route, size)
    }
  }

  // The first article of the news, found from the list.
  await page.setViewportSize(sizes.desktop)
  await page.goto('/news')
  await settle(page)
  const article = page.locator('main a[href^="/news/"]').first()
  if (await article.count()) {
    const href = (await article.getAttribute('href'))!
    for (const size of ['desktop', 'mobile'] as const) {
      await page.setViewportSize(sizes[size])
      await page.goto(href)
      await settle(page)
      await shoot(page, guest, '/news/detail', size)
    }
  }
})

for (const role of roles) {
  test(`every page of the ${role.button.toLowerCase()}`, async ({ page }) => {
    await page.goto('/login')
    await page.getByRole('button', { name: new RegExp(`^${role.button}`) }).click()
    await page.waitForURL((url) => !url.pathname.endsWith('/login'))
    await settle(page)

    // The pages in the sidebar, in the order of the menu (desktop shows the sidebar).
    await page.setViewportSize(sizes.desktop)
    const hrefs = await page.locator('aside nav a[href]').evaluateAll((links) => [...new Set(links.map((link) => link.getAttribute('href') ?? ''))].filter(Boolean))
    const routes = new Map<string, string>() // route -> the detail route found on that page, if any

    for (const route of hrefs) {
      await goInApp(page, route)
      await settle(page)
      await shoot(page, role.folder, route, 'desktop')

      const link = page.locator('main a[href]').evaluateAll((links, pattern) => {
        const re = new RegExp(pattern)
        return links.map((l) => l.getAttribute('href') ?? '').find((href) => re.test(href)) ?? ''
      }, detailLink(route).source)
      const detail = await link
      if (detail) {
        routes.set(route, detail)
        await goInApp(page, detail)
        await settle(page)
        await shoot(page, role.folder, detail, 'desktop')
      }
    }

    // The same pages at the size of a phone, with the bottom navigation.
    await page.setViewportSize(sizes.mobile)
    for (const route of hrefs) {
      await goInApp(page, route)
      await settle(page)
      await shoot(page, role.folder, route, 'mobile')
      const detail = routes.get(route)
      if (detail) {
        await goInApp(page, detail)
        await settle(page)
        await shoot(page, role.folder, detail, 'mobile')
      }
    }
  })
}
