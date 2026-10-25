import changelog from '../../../../CHANGELOG.md?raw'

export interface Release {
  version: string
  /** The date for a numbered release, or the sprint for a pre-release, as written in the changelog heading. */
  label: string
  preRelease: boolean
  summary: string
  changes: string[]
}

/** Reads CHANGELOG.md ("## [x.y.z] - label" headings, "- " items) into releases, newest first as written. */
export function parseChangelog(text: string): Release[] {
  const releases: Release[] = []
  let current: Release | null = null
  for (const line of text.split(/\r?\n/)) {
    const heading = /^## \[(\d+\.\d+\.\d+)\]\s*-\s*(.+)$/.exec(line)
    if (heading) {
      const label = heading[2].trim()
      current = { version: heading[1], label: label.replace(/\s*\(pre-release\)$/, ''), preRelease: /pre-release/.test(label), summary: '', changes: [] }
      releases.push(current)
    } else if (!current || line.startsWith('#')) {
      continue
    } else if (line.startsWith('- ')) {
      current.changes.push(line.slice(2).trim())
    } else if (/^\s+\S/.test(line) && current.changes.length > 0) {
      current.changes[current.changes.length - 1] += ` ${line.trim()}`
    } else if (line.trim()) {
      current.summary = `${current.summary} ${line.trim()}`.trim()
    }
  }
  return releases
}

export const releases = parseChangelog(changelog)
