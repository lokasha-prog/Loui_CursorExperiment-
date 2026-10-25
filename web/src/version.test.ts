import fs from 'node:fs'
import path from 'node:path'
import { describe, expect, it } from 'vitest'

// ADR-0009: nms/VERSION is the one product version; every copy must match it and the changelog must describe it.
const root = path.resolve(__dirname, '../..')
const read = (file: string) => fs.readFileSync(path.join(root, file), 'utf8')
const version = read('VERSION').trim()

describe('product version', () => {
  it('is MAJOR.MINOR.PATCH', () => {
    expect(version).toMatch(/^\d+\.\d+\.\d+$/)
  })

  it.each([
    ['web/package.json', () => JSON.parse(read('web/package.json')).version],
    ['mobile/package.json', () => JSON.parse(read('mobile/package.json')).version],
    ['mobile/app.json', () => JSON.parse(read('mobile/app.json')).expo.version],
    ['website/index.html', () => /<meta name="version" content="([^"]+)"/.exec(read('website/index.html'))?.[1]],
  ])('is the same in %s', (_file, found) => {
    expect(found()).toBe(version)
  })

  it('has release notes in the changelog', () => {
    expect(read('CHANGELOG.md')).toContain(`## [${version}]`)
  })

  it('is what the web app shows', () => {
    expect(__APP_VERSION__).toBe(version)
  })
})
