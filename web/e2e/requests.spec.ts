import { expect as baseExpect, test, type Page } from '@playwright/test'
import { loginAs, unique } from './support/helpers.ts'

// Phase 5 exit criterion: the approval flows pass end to end. A parent asks for a leave and for a letter; the right person
// decides at every step of the chain (homeroom teacher, admin staff, principal); the parent sees the result and downloads the
// letter from the real storage. The demo accounts take the parts: the demo parent has two children in Class A, whose homeroom
// teacher is the demo teacher.
const expect = baseExpect.configure({ timeout: 20_000 })

const dayFromNow = (days: number) => new Date(Date.now() + days * 86_400_000).toISOString().slice(0, 10)

/** The link to a request in a list: lists do not show the reason, so a request is found by its address. */
const linkTo = (page: Page, id: string) => page.locator(`a[href="/requests/${id}"]`)

/** The parent fills in the form for the first child and sends it; the page of the new request opens and its id is returned. */
async function send(page: Page, kind: 'Student leave' | 'Certificate letter', reason: string): Promise<string> {
  await page.getByRole('link', { name: 'Requests', exact: true }).first().click()
  await page.getByRole('link', { name: 'New request' }).click()
  await page.getByLabel('What do you ask for?').selectOption({ label: kind })
  if (kind === 'Student leave') {
    await page.getByLabel('First day').fill(dayFromNow(1))
    await page.getByLabel('Last day').fill(dayFromNow(2))
    await page.getByLabel('Reason').fill(reason)
  } else {
    await page.getByLabel('What is the letter for?').fill(reason)
  }

  await page.getByRole('button', { name: 'Send the request' }).click()
  await expect(page.getByText('Request sent').first()).toBeVisible()
  await expect(page).toHaveURL(/\/requests\/[0-9a-f-]{36}$/)
  return page.url().split('/').pop() as string
}

/** The approver finds the request in the inbox, opens it and decides. */
async function decide(page: Page, id: string, how: 'Approve' | 'Reject', note?: string) {
  await page.getByRole('link', { name: 'Approvals', exact: true }).first().click()
  await linkTo(page, id).click()
  await expect(page.getByText('This request waits for your decision.')).toBeVisible()
  await page.getByRole('button', { name: how, exact: true }).click()
  const dialog = page.getByRole('alertdialog')
  if (note) {
    await dialog.getByLabel('Note').fill(note)
  }

  await dialog.getByRole('button', { name: how, exact: true }).click()
  await expect(page.getByText('Your decision is saved').first()).toBeVisible()
}

test('a leave goes to the homeroom teacher and then the principal, and the parent sees it approved', async ({ browser }) => {
  test.setTimeout(240_000)
  const reason = `E2E fever ${unique()}`
  const contexts = await Promise.all([browser.newContext(), browser.newContext(), browser.newContext()])

  try {
    const [parent, teacher, principal] = await Promise.all(contexts.map((context) => context.newPage()))
    await loginAs(parent, 'Parent')
    await loginAs(teacher, 'Teacher')
    await loginAs(principal, 'Principal')

    const id = await send(parent, 'Student leave', reason)
    await expect(parent.getByText(reason).first()).toBeVisible()
    await expect(parent.getByText('Waiting now').first()).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/requests-parent-leave-sent.png', fullPage: true })

    // Step 1: the homeroom teacher of the class. The principal has nothing to decide yet.
    await principal.getByRole('link', { name: 'Approvals', exact: true }).first().click()
    await expect(linkTo(principal, id)).toHaveCount(0)
    await decide(teacher, id, 'Approve', 'Get well soon')

    // Step 2: now it is the principal's turn, and the dashboard counts it.
    await principal.getByRole('link', { name: 'Dashboard', exact: true }).first().click()
    await expect(principal.getByText('Requests awaiting approval').first()).toBeVisible()
    await decide(principal, id, 'Approve')
    await principal.screenshot({ path: 'test-results/screens/requests-principal-decided.png', fullPage: true })

    await parent.getByRole('link', { name: 'Requests', exact: true }).first().click()
    const row = parent.getByRole('row').filter({ has: linkTo(parent, id) })
    await expect(row.getByText('Approved')).toBeVisible()
    await linkTo(parent, id).click()
    await expect(parent.getByText('Get well soon')).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/requests-parent-leave-approved.png', fullPage: true })
  } finally {
    await Promise.all(contexts.map((context) => context.close()))
  }
})

test('a rejection needs a note and ends the request', async ({ browser }) => {
  test.setTimeout(180_000)
  const reason = `E2E trip ${unique()}`
  const contexts = await Promise.all([browser.newContext(), browser.newContext()])

  try {
    const [parent, teacher] = await Promise.all(contexts.map((context) => context.newPage()))
    await loginAs(parent, 'Parent')
    await loginAs(teacher, 'Teacher')
    const id = await send(parent, 'Student leave', reason)

    await teacher.getByRole('link', { name: 'Approvals', exact: true }).first().click()
    await linkTo(teacher, id).click()
    await teacher.getByRole('button', { name: 'Reject', exact: true }).click()
    const dialog = teacher.getByRole('alertdialog')
    await dialog.getByRole('button', { name: 'Reject', exact: true }).click()
    await expect(dialog.getByText('Give the reason for the rejection.')).toBeVisible()
    await dialog.getByLabel('Note').fill('The exams are that week.')
    await dialog.getByRole('button', { name: 'Reject', exact: true }).click()
    await expect(teacher.getByText('Your decision is saved').first()).toBeVisible()

    await parent.getByRole('link', { name: 'Requests', exact: true }).first().click()
    const row = parent.getByRole('row').filter({ has: linkTo(parent, id) })
    await expect(row.getByText('Rejected')).toBeVisible()
    await linkTo(parent, id).click()
    await expect(parent.getByText('The exams are that week.')).toBeVisible()
    await expect(parent.getByRole('button', { name: 'Withdraw the request' })).toHaveCount(0)
  } finally {
    await Promise.all(contexts.map((context) => context.close()))
  }
})

test('a certificate letter is approved by the admin staff and the principal and downloaded from the storage', async ({ browser }) => {
  test.setTimeout(240_000)
  const reason = `E2E scholarship ${unique()}`
  const contexts = await Promise.all([browser.newContext(), browser.newContext(), browser.newContext()])

  try {
    const [parent, admin, principal] = await Promise.all(contexts.map((context) => context.newPage()))
    await loginAs(parent, 'Parent')
    await loginAs(admin, 'Admin staff')
    await loginAs(principal, 'Principal')

    const id = await send(parent, 'Certificate letter', reason)
    await decide(admin, id, 'Approve')
    await decide(principal, id, 'Approve')

    // The letter has a number and a PDF in the private bucket, reached through a signed address.
    await parent.getByRole('link', { name: 'Requests', exact: true }).first().click()
    await linkTo(parent, id).click()
    await expect(parent.getByText(/Letter number \d{3,}\/SK\/SCHOMANS\/(I|II|III|IV|V|VI|VII|VIII|IX|X|XI|XII)\/\d{4}/)).toBeVisible()
    await parent.screenshot({ path: 'test-results/screens/requests-letter-approved.png', fullPage: true })

    const pdfRequest = contexts[0].waitForEvent('request', (request) => /\/schomans-private\/letters\/.+\.pdf\?/.test(request.url()))
    await parent.getByRole('button', { name: 'Download letter (PDF)' }).click()
    expect((await pdfRequest).url()).toContain('X-Amz-Signature=')
    for (const page of contexts[0].pages()) {
      if (page !== parent) {
        await page.close()
      }
    }
  } finally {
    await Promise.all(contexts.map((context) => context.close()))
  }
})

test('anybody can check a letter by its code, and an unknown code is not valid', async ({ page }) => {
  await page.goto('/verify/0123456789abcdef0123')

  await expect(page.getByRole('heading', { name: 'No letter with this code' })).toBeVisible()
  await page.screenshot({ path: 'test-results/screens/requests-verify-unknown.png' })
})

test('the principal reads the approval chains and only the admin staff change them', async ({ browser }) => {
  const contexts = await Promise.all([browser.newContext(), browser.newContext()])

  try {
    const [admin, principal] = await Promise.all(contexts.map((context) => context.newPage()))
    await loginAs(admin, 'Admin staff')
    await loginAs(principal, 'Principal')

    await admin.getByRole('link', { name: 'Approval chains', exact: true }).first().click()
    await expect(admin.getByRole('heading', { level: 1, name: 'Approval chains' })).toBeVisible()
    await expect(admin.getByRole('button', { name: 'Save the chain' }).first()).toBeVisible()
    await admin.screenshot({ path: 'test-results/screens/requests-chains-admin.png', fullPage: true })

    await principal.getByRole('link', { name: 'Approval chains', exact: true }).first().click()
    await expect(principal.getByText('You can read the chains; only the admin staff change them.')).toBeVisible()
    await expect(principal.getByRole('button', { name: 'Save the chain' })).toHaveCount(0)
  } finally {
    await Promise.all(contexts.map((context) => context.close()))
  }
})
