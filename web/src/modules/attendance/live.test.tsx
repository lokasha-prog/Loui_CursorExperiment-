import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { ShortageBanner } from './ShortageBanner'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

const room = (breach: boolean) => ({
  room_id: 'r1', name: { en: 'Little Stars', ar: 'النجوم الصغيرة' }, capacity: 12, ratio_staff_to_student: 4,
  students_present: 5, staff_present: 1, staff_required: 2, is_breach: breach,
})

describe('ShortageBanner', () => {
  it('warns on every page while a room is short, naming the room and the gap', async () => {
    signIn(makeMe({ permissions: ['attendance.write'] }))
    mockApi(fetchMock, { 'GET /attendance/ratio/live': envelope([room(true)]) })
    renderWithProviders(<ShortageBanner />)

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent('Not enough staff:')
    expect(alert).toHaveTextContent('Little Stars needs 2, has 1')
  })

  it('shows nothing when every room is covered', async () => {
    signIn(makeMe({ permissions: ['attendance.write'] }))
    mockApi(fetchMock, { 'GET /attendance/ratio/live': envelope([room(false)]) })
    renderWithProviders(<ShortageBanner />)

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('speaks Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['attendance.write'] }))
    mockApi(fetchMock, { 'GET /attendance/ratio/live': envelope([room(true)]) })
    renderWithProviders(<ShortageBanner />)

    expect(await screen.findByRole('alert')).toHaveTextContent('النجوم الصغيرة تحتاج 2 والموجود 1')
  })

  it('stays out of the way for people who do not handle attendance', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['attendance.read'] }))
    mockApi(fetchMock, {})
    renderWithProviders(<ShortageBanner />)

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })
})
