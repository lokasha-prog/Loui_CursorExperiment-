import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { makeMe, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import ReleaseNotesPage from './ReleaseNotesPage'
import { parseChangelog, releases } from './releases'

beforeEach(async () => {
  await setLanguage('en')
  signIn(makeMe({ roles: ['parent'], permissions: ['students.read'] }))
})

afterEach(() => signOut())

describe('release notes', () => {
  it('reads versions, labels, summaries and wrapped items from the changelog', () => {
    const parsed = parseChangelog(['# Changelog', 'intro', '## [1.1.0] - 2026-11-02', '', '- New page', '  that wraps', '## [0.3.0] - Sprint 3 (pre-release)', '', 'Summary line.', '- Feed'].join('\n'))

    expect(parsed).toEqual([
      { version: '1.1.0', label: '2026-11-02', preRelease: false, summary: '', changes: ['New page that wraps'] },
      { version: '0.3.0', label: 'Sprint 3', preRelease: true, summary: 'Summary line.', changes: ['Feed'] },
    ])
  })

  it('has the running version at the top of the real changelog', () => {
    expect(releases[0].version).toBe(__APP_VERSION__)
  })

  it('shows every version to anyone signed in, marks the current one and opens the earlier history on request', async () => {
    // Inside <main>, as in the app shell, so the accessibility check sees the page as it really is.
    renderWithProviders(<main><ReleaseNotesPage /></main>)

    expect(screen.getByRole('heading', { level: 1, name: 'Release notes' })).toBeInTheDocument()
    const current = screen.getByRole('article', { name: `Version ${__APP_VERSION__}` })
    expect(within(current).getByText('You are here')).toBeInTheDocument()
    expect(screen.queryByRole('article', { name: 'Version 0.1.0' })).not.toBeInTheDocument()

    const toggle = screen.getByRole('button', { name: /Show \d+ earlier versions/ })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    await userEvent.click(toggle)
    expect(toggle).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByRole('article', { name: 'Version 0.1.0' })).toBeInTheDocument()

    const results = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(results.violations.map((v) => v.id)).toEqual([])
  })

  it('is translated in Arabic and says the notes themselves are in English', async () => {
    await setLanguage('ar')
    renderWithProviders(<ReleaseNotesPage />)

    expect(screen.getByRole('heading', { level: 1, name: 'ملاحظات الإصدار' })).toBeInTheDocument()
    expect(screen.getByText('ملاحظات الإصدارات مكتوبة باللغة الإنجليزية.')).toBeInTheDocument()
  })
})
