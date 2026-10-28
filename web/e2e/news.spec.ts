import { expect, test } from '@playwright/test'
import { adminApi, loginAs, tinyPng, unique } from './support/helpers.ts'

// Phase 2 exit criterion: news is written in the editor, its cover image goes to storage (SeaweedFS, with CORS),
// and a guest reads the published article with its own title and meta tags.
test('an admin writes, illustrates and publishes an article, and a guest reads it', async ({ page, browser, request }) => {
  const title = `E2E open house ${unique()}`
  const description = 'Visit our school on Saturday and meet the teachers.'
  let articleId: string | undefined

  try {
    await loginAs(page, 'Admin staff')
    await page.getByRole('link', { name: 'Manage news' }).first().click()
    await page.getByRole('link', { name: 'New article' }).click()

    // The cover image needs a saved draft first: the field says so.
    await expect(page.getByText('Save the draft first, then you can add a cover image.')).toBeVisible()

    await page.getByLabel(/^Title/).fill(title)
    await page.getByLabel('Description for search engines').fill(description)
    const editor = page.getByRole('textbox', { name: 'Article text' })
    await editor.click()
    await page.keyboard.type('Welcome ')
    await page.getByRole('button', { name: 'Bold' }).click()
    await page.keyboard.type('everybody')
    await page.getByRole('button', { name: 'Save draft' }).click()

    // Saving moves to the edit page of the new draft.
    await expect(page).toHaveURL(/\/manage\/news\/[0-9a-f-]{36}$/)
    articleId = /\/manage\/news\/([0-9a-f-]{36})$/.exec(page.url())?.[1]
    const slug = (await page.getByText(/^Address: \/news\//).textContent())?.replace('Address: /news/', '').trim()
    expect(slug).toBeTruthy()

    // A guest cannot read a draft.
    const guest = await browser.newContext()
    const guestPage = await guest.newPage()
    await guestPage.goto(`/news/${slug}`)
    await expect(guestPage.getByText('This article does not exist')).toBeVisible()

    // The cover image goes straight to storage through a presigned URL.
    await page.getByLabel('Choose an image').setInputFiles({ name: 'cover.png', mimeType: 'image/png', buffer: tinyPng })
    await expect(page.getByAltText('Cover image preview')).toBeVisible()
    await page.getByRole('button', { name: 'Save changes' }).click()
    await expect(page.getByRole('button', { name: 'Save changes' })).toBeDisabled()
    await page.screenshot({ path: 'test-results/screens/news-editor.png', fullPage: true })

    await page.getByRole('button', { name: 'Publish' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Publish' }).click()
    await expect(page.getByRole('button', { name: 'Unpublish' })).toBeVisible()

    // The guest now reads it, with its own title and meta tags, and the cover image loads from storage.
    await guestPage.reload()
    await expect(guestPage.getByRole('heading', { level: 1, name: title })).toBeVisible()
    await expect(guestPage.locator('article strong')).toHaveText('everybody')
    await expect(guestPage).toHaveTitle(`${title} · SchoMans`)
    await expect(guestPage.locator('meta[name="description"]')).toHaveAttribute('content', description)
    await expect(guestPage.locator('meta[property="og:image"]')).toHaveAttribute('content', /^http/)
    const cover = guestPage.locator('article img').first()
    await expect(cover).toBeVisible()
    expect(await cover.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth > 0)).toBe(true)
    await guestPage.screenshot({ path: 'test-results/screens/news-public-article.png', fullPage: true })

    // It is on the landing page and in the public list.
    await guestPage.goto('/')
    await expect(guestPage.getByRole('link', { name: title })).toBeVisible()
    await guestPage.screenshot({ path: 'test-results/screens/landing-with-news.png', fullPage: true })
    await guestPage.goto('/news')
    await expect(guestPage.getByRole('link', { name: title })).toBeVisible()

    // Unpublishing takes it off the site again.
    await page.getByRole('button', { name: 'Unpublish' }).click()
    await page.getByRole('alertdialog').getByRole('button', { name: 'Unpublish' }).click()
    await expect(page.getByRole('button', { name: 'Publish' })).toBeVisible()
    await guestPage.goto(`/news/${slug}`)
    await expect(guestPage.getByText('This article does not exist')).toBeVisible()

    await guest.close()
  } finally {
    if (articleId) {
      await (await adminApi(request)).del(`/api/v1/news/${articleId}`)
    }
  }
})

test('a teacher cannot manage news, and the page says why', async ({ page }) => {
  await loginAs(page, 'Teacher')

  await expect(page.getByRole('link', { name: 'Manage news' })).toHaveCount(0)
  // Navigating inside the app keeps the in-memory session.
  await page.evaluate(() => {
    window.history.pushState({}, '', '/manage/news')
    window.dispatchEvent(new PopStateEvent('popstate'))
  })
  await expect(page.getByRole('alert')).toContainText('You do not have access to this page')
})
