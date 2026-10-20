import { useEffect, useState } from 'react'
import { api } from '@/api/client'
import { Skeleton } from '@/components/ui/surfaces'

/** Shows a photo or video that needs the signed-in token, by fetching it and displaying the downloaded copy. */
export function ProtectedMedia({ id, contentType, alt }: { id: string; contentType: string; alt: string }) {
  const [url, setUrl] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    let revoked = false
    let created: string | null = null
    api
      .blob(`/media/${id}/content`)
      .then((blob) => {
        if (revoked) return
        created = URL.createObjectURL(blob)
        setUrl(created)
      })
      .catch(() => setFailed(true))
    return () => {
      revoked = true
      if (created) URL.revokeObjectURL(created)
    }
  }, [id])

  if (failed) return <div className="flex h-40 items-center justify-center rounded-md bg-slate-100 text-xs text-muted-foreground" role="img" aria-label={alt} />
  if (!url) return <Skeleton className="h-40 w-full" />
  return contentType.startsWith('video/') ? (
    <video src={url} controls className="max-h-80 w-full rounded-md bg-black" aria-label={alt} />
  ) : (
    <img src={url} alt={alt} className="max-h-80 w-full rounded-md object-cover" />
  )
}
