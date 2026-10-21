// Shows the phone app's web build in a browser. /api calls are passed on to the local API so no cross-site settings are needed.
import http from 'node:http'
import { readFile } from 'node:fs/promises'
import { extname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../mobile/dist/', import.meta.url))
const api = { host: 'localhost', port: 5178 }
const types = { '.css': 'text/css', '.svg': 'image/svg+xml', '.png': 'image/png', '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.ico': 'image/x-icon' }

http.createServer(async (req, res) => {
  if (req.url.startsWith('/api/')) {
    const upstream = http.request({ ...api, path: req.url, method: req.method, headers: req.headers }, (r) => { res.writeHead(r.statusCode, r.headers); r.pipe(res) })
    upstream.on('error', () => { res.writeHead(502); res.end() })
    req.pipe(upstream)
    return
  }
  const path = decodeURIComponent(req.url.split('?')[0])
  try {
    const file = await readFile(join(root, path === '/' ? 'index.html' : path))
    res.writeHead(200, { 'Content-Type': types[extname(path)] ?? 'text/html' })
    res.end(file)
  } catch {
    res.writeHead(200, { 'Content-Type': 'text/html' })
    res.end(await readFile(join(root, 'index.html')))
  }
}).listen(5181, () => console.log('Phone app preview on http://localhost:5181'))
