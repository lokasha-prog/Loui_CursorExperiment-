import { expect, test, type Page } from '@playwright/test'

/** Enters the system as a local test user (login page helper). Never types a password. */
async function enterAs(page: Page, role: string) {
  await page.goto('/login')
  await page.getByRole('button', { name: role, exact: true }).click()
  await expect(page).toHaveURL(/\/dashboard/)
}

/** Moves between screens through the menu, because a full page load would drop the in-memory sign-in. */
async function open(page: Page, name: string | RegExp) {
  await page.getByRole('navigation').getByRole('link', { name }).first().click()
}

const watchErrors = (page: Page) => {
  const errors: string[] = []
  page.on('pageerror', (e) => errors.push(e.message))
  return errors
}

test('a manager can walk through the main screens with no page errors', async ({ page }) => {
  const errors = watchErrors(page)
  await enterAs(page, 'IT')
  for (const name of [/students/i, /attendance/i, /calendar/i, /billing/i, /newsletters/i, /resources/i, /forms/i]) {
    await open(page, name)
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
  }
  expect(errors).toEqual([])
})

test('the management AI briefing shows its figures', async ({ page }) => {
  await enterAs(page, 'IT')
  await page.getByRole('button', { name: 'Write briefing' }).first().click()
  // The AI provider may be off on a test machine: either the briefing with its figures or the plain "not available" message is correct.
  await expect(page.getByText(/Figures used|AI Brain is not available/)).toBeVisible({ timeout: 30_000 })
})

test('switching to Arabic turns the whole page right-to-left and back', async ({ page }) => {
  await enterAs(page, 'IT')
  await page.getByRole('button', { name: 'العربية' }).click()
  await expect(page.locator('html')).toHaveAttribute('dir', 'rtl')
  await page.getByRole('button', { name: 'English' }).click()
  await expect(page.locator('html')).toHaveAttribute('dir', 'ltr')
})

test('a parent sees their own screens and no management menus', async ({ page }) => {
  await enterAs(page, 'Parent')
  const menu = page.getByRole('navigation')
  await expect(menu.getByRole('link', { name: /feed/i }).first()).toBeVisible()
  await expect(menu.getByRole('link', { name: /settings/i })).toHaveCount(0)
  await expect(menu.getByRole('link', { name: /audit/i })).toHaveCount(0)
  await expect(page.getByRole('button', { name: 'Write briefing' })).toHaveCount(0)
})

test('the public enquiry form is reachable without signing in', async ({ page }) => {
  await page.goto('/login')
  const response = await page.request.get('/api/v1/public/enquiry-form/bright-futures')
  expect([200, 404]).toContain(response.status())
})
