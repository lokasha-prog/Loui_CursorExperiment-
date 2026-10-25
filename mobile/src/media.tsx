import { useEffect, useState } from 'react'
import { Image, Platform, type ImageStyle } from 'react-native'
import { mediaSource } from './api'

/** A stored photo. Phones send the sign-in header with the image request; the browser preview cannot, so it fetches the photo first. */
export function StoredPhoto({ id, label, style }: { id: string; label: string; style: ImageStyle }) {
  const source = mediaSource(id)
  const [webUri, setWebUri] = useState<string | null>(null)

  useEffect(() => {
    if (Platform.OS !== 'web') return
    let url: string | null = null
    let cancelled = false
    fetch(source.uri, { headers: source.headers })
      .then((r) => (r.ok ? r.blob() : null))
      .then((blob) => {
        if (!blob || cancelled) return
        url = URL.createObjectURL(blob)
        setWebUri(url)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
      if (url) URL.revokeObjectURL(url)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  if (Platform.OS === 'web') return webUri ? <Image accessibilityLabel={label} source={{ uri: webUri }} style={style} resizeMode="cover" /> : null
  return <Image accessibilityLabel={label} source={source} style={style} resizeMode="cover" />
}
