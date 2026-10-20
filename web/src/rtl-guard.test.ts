import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'

function sourceFiles(directory: string): string[] {
  return readdirSync(directory).flatMap((name) => {
    const path = join(directory, name)
    if (statSync(path).isDirectory()) return name === 'test' ? [] : sourceFiles(path)
    return /\.(tsx|css)$/.test(name) && !name.includes('.test.') ? [path] : []
  })
}

// Arabic is a first-class language: layouts must use logical (start/end) properties so they mirror automatically.
const physical =
  /(?:^|[\s"'`:])(?:-?(?:ml|mr|pl|pr)-|text-(?:left|right)\b|(?:left|right)-\d|rounded-(?:l|r|tl|tr|bl|br)(?:-|\b)|border-(?:l|r)(?:-|\b)|float-(?:left|right))/
const cssPhysical = /(?:margin|padding|border)-(?:left|right)\s*:|text-align\s*:\s*(?:left|right)|(?<![\w-])(?:left|right)\s*:\s*\d/

describe('right-to-left safety', () => {
  const files = sourceFiles('src')

  it('scans the real source files', () => {
    expect(files.length).toBeGreaterThan(20)
  })

  it('uses no left/right-only utility classes', () => {
    const offenders = files.filter((file) => physical.test(readFileSync(file, 'utf8')))
    expect(offenders).toEqual([])
  })

  it('uses no left/right-only CSS properties', () => {
    const offenders = files.filter((file) => file.endsWith('.css')).filter((file) => cssPhysical.test(readFileSync(file, 'utf8')))
    expect(offenders).toEqual([])
  })

  it('flips directional icons for Arabic', () => {
    const detail = readFileSync('src/modules/students/StudentDetailPage.tsx', 'utf8')
    const list = readFileSync('src/modules/students/StudentsPage.tsx', 'utf8')
    expect(detail).toContain('ArrowLeft className="h-4 w-4 rtl:rotate-180"')
    expect(list).toContain('ChevronRight className="h-4 w-4 rtl:rotate-180"')
  })
})
