import { Download } from 'lucide-react'
import { useId, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/surfaces'

export const PALETTE = ['#0E7C86', '#F59E0B', '#7C3AED', '#EF4444', '#10B981', '#3B82F6', '#EC4899', '#64748B']

/** Saves the chart's picture as a PNG, so it can go into a slide or a report. */
function savePng(svg: SVGSVGElement, name: string) {
  const clone = svg.cloneNode(true) as SVGSVGElement
  const { width, height } = svg.viewBox.baseVal
  clone.setAttribute('xmlns', 'http://www.w3.org/2000/svg')
  clone.setAttribute('width', String(width * 2))
  clone.setAttribute('height', String(height * 2))
  const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(clone)], { type: 'image/svg+xml' }))
  const image = new Image()
  image.onload = () => {
    const canvas = document.createElement('canvas')
    canvas.width = width * 2
    canvas.height = height * 2
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.fillStyle = '#ffffff'
    ctx.fillRect(0, 0, canvas.width, canvas.height)
    ctx.drawImage(image, 0, 0)
    URL.revokeObjectURL(url)
    canvas.toBlob((png) => {
      if (!png) return
      const link = document.createElement('a')
      link.href = URL.createObjectURL(png)
      link.download = `${name}.png`
      link.click()
    })
  }
  image.src = url
}

export function ChartCard({ title, empty, children, fileName }: { title: string; empty?: boolean; children: (ref: React.RefObject<SVGSVGElement>) => React.ReactNode; fileName: string }) {
  const { t } = useTranslation()
  const ref = useRef<SVGSVGElement>(null)
  const id = useId()
  return (
    <Card className="p-4">
      <div className="mb-2 flex items-center justify-between gap-2">
        <h3 id={id} className="font-heading text-base font-semibold">{title}</h3>
        {!empty && <Button size="sm" variant="ghost" onClick={() => ref.current && savePng(ref.current, fileName)}><Download className="h-4 w-4" aria-hidden="true" />{t('reports.analytics.png')}</Button>}
      </div>
      {empty ? <p className="py-8 text-center text-sm text-muted-foreground">{t('reports.analytics.noData')}</p> : children(ref as React.RefObject<SVGSVGElement>)}
    </Card>
  )
}

const W = 520
const H = 220
const PAD = { l: 44, r: 12, t: 12, b: 28 }

function axis(max: number) {
  const top = Math.max(1, max)
  return [0, 0.25, 0.5, 0.75, 1].map((f) => ({ f, v: top * f }))
}

export function LineChart({ points, label, svgRef, format = (v: number) => String(Math.round(v)) }: { points: { label: string; value: number }[]; label: string; svgRef: React.RefObject<SVGSVGElement>; format?: (v: number) => string }) {
  const max = Math.max(1, ...points.map((p) => p.value))
  const x = (i: number) => PAD.l + (points.length === 1 ? (W - PAD.l - PAD.r) / 2 : (i * (W - PAD.l - PAD.r)) / (points.length - 1))
  const y = (v: number) => H - PAD.b - (v / max) * (H - PAD.t - PAD.b)
  const path = points.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(p.value).toFixed(1)}`).join(' ')
  const every = Math.max(1, Math.ceil(points.length / 8))
  return (
    <svg ref={svgRef} viewBox={`0 0 ${W} ${H}`} role="img" aria-label={label} className="h-auto w-full" fontFamily="Arial, sans-serif" fontSize="10">
      <rect width={W} height={H} fill="#ffffff" />
      {axis(max).map((a) => (<g key={a.f}><line x1={PAD.l} x2={W - PAD.r} y1={y(a.v)} y2={y(a.v)} stroke="#e2e8f0" /><text x={PAD.l - 6} y={y(a.v) + 3} textAnchor="end" fill="#64748b">{format(a.v)}</text></g>))}
      <path d={path} fill="none" stroke={PALETTE[0]} strokeWidth="2.5" strokeLinejoin="round" />
      {points.map((p, i) => (<g key={p.label}><circle cx={x(i)} cy={y(p.value)} r="3" fill={PALETTE[0]}><title>{`${p.label}: ${format(p.value)}`}</title></circle>{i % every === 0 && <text x={x(i)} y={H - 10} textAnchor="middle" fill="#64748b">{p.label.length > 7 ? p.label.slice(5) : p.label}</text>}</g>))}
    </svg>
  )
}

export function BarChart({ categories, series, label, svgRef, format = (v: number) => String(Math.round(v)) }: {
  categories: string[]
  series: { name: string; values: number[] }[]
  label: string
  svgRef: React.RefObject<SVGSVGElement>
  format?: (v: number) => string
}) {
  const max = Math.max(1, ...series.flatMap((s) => s.values))
  const groupW = (W - PAD.l - PAD.r) / Math.max(1, categories.length)
  const barW = Math.min(26, (groupW * 0.7) / series.length)
  const y = (v: number) => H - PAD.b - (v / max) * (H - PAD.t - PAD.b - 14)
  const every = Math.max(1, Math.ceil(categories.length / 8))
  return (
    <>
      <svg ref={svgRef} viewBox={`0 0 ${W} ${H + 18}`} role="img" aria-label={label} className="h-auto w-full" fontFamily="Arial, sans-serif" fontSize="10">
        <rect width={W} height={H + 18} fill="#ffffff" />
        {axis(max).map((a) => (<g key={a.f}><line x1={PAD.l} x2={W - PAD.r} y1={y(a.v)} y2={y(a.v)} stroke="#e2e8f0" /><text x={PAD.l - 6} y={y(a.v) + 3} textAnchor="end" fill="#64748b">{format(a.v)}</text></g>))}
        {categories.map((c, i) => (
          <g key={c}>
            {series.map((s, k) => (<rect key={s.name} x={PAD.l + i * groupW + (groupW - barW * series.length) / 2 + k * barW} y={y(s.values[i])} width={barW - 2} height={Math.max(0, H - PAD.b - y(s.values[i]))} fill={PALETTE[k % PALETTE.length]} rx="2"><title>{`${c} · ${s.name}: ${format(s.values[i])}`}</title></rect>))}
            {i % every === 0 && <text x={PAD.l + i * groupW + groupW / 2} y={H - 12} textAnchor="middle" fill="#64748b">{c.length > 7 ? c.slice(2) : c}</text>}
          </g>
        ))}
        {series.map((s, k) => (<g key={s.name} transform={`translate(${PAD.l + k * 120},${H + 4})`}><rect width="10" height="10" fill={PALETTE[k % PALETTE.length]} rx="2" /><text x="14" y="9" fill="#334155">{s.name}</text></g>))}
      </svg>
      <table className="sr-only"><caption>{label}</caption><thead><tr><th>{label}</th>{series.map((s) => <th key={s.name}>{s.name}</th>)}</tr></thead><tbody>{categories.map((c, i) => <tr key={c}><td>{c}</td>{series.map((s) => <td key={s.name}>{s.values[i]}</td>)}</tr>)}</tbody></table>
    </>
  )
}

export function DonutChart({ slices, label, svgRef }: { slices: { label: string; value: number }[]; label: string; svgRef: React.RefObject<SVGSVGElement> }) {
  const total = slices.reduce((a, s) => a + s.value, 0) || 1
  const R = 70
  const r = 42
  let angle = -Math.PI / 2
  return (
    <svg ref={svgRef} viewBox="0 0 520 190" role="img" aria-label={label} className="h-auto w-full" fontFamily="Arial, sans-serif" fontSize="11">
      <rect width="520" height="190" fill="#ffffff" />
      <g transform="translate(100,95)">
        {slices.map((s, i) => {
          const a0 = angle
          const a1 = angle + (s.value / total) * Math.PI * 2
          angle = a1
          const large = a1 - a0 > Math.PI ? 1 : 0
          const full = slices.length === 1
          const p = (rad: number, a: number) => `${(rad * Math.cos(a)).toFixed(2)},${(rad * Math.sin(a)).toFixed(2)}`
          const d = full
            ? `M0,${-R} A${R},${R} 0 1,1 0,${R} A${R},${R} 0 1,1 0,${-R} M0,${-r} A${r},${r} 0 1,0 0,${r} A${r},${r} 0 1,0 0,${-r}Z`
            : `M${p(R, a0)} A${R},${R} 0 ${large},1 ${p(R, a1)} L${p(r, a1)} A${r},${r} 0 ${large},0 ${p(r, a0)}Z`
          return <path key={s.label} d={d} fill={PALETTE[i % PALETTE.length]} fillRule="evenodd"><title>{`${s.label}: ${s.value}`}</title></path>
        })}
        <text textAnchor="middle" y="5" fontSize="16" fontWeight="bold" fill="#0f172a">{slices.reduce((a, s) => a + s.value, 0)}</text>
      </g>
      {slices.slice(0, 8).map((s, i) => (
        <g key={s.label} transform={`translate(220,${22 + i * 20})`}>
          <rect width="11" height="11" rx="2" fill={PALETTE[i % PALETTE.length]} />
          <text x="18" y="10" fill="#334155">{s.label} · {s.value} ({Math.round((s.value / total) * 100)}%)</text>
        </g>
      ))}
    </svg>
  )
}
