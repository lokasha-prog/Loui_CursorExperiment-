"""
Security smoke test for a LOCAL development system. Not a penetration test; it is the first pass we run so the outside testers do not waste time on basics.

  1. Every endpoint in the API description is called with no sign-in. Only the endpoints that are meant to be public may answer.
  2. Every read endpoint is called as a parent (the least trusted signed-in role). The ones that answer 200 are listed for a human to confirm.
  3. Odd input (quote characters, very long text, wrong types) must never give a 500.
  4. Security headers on the API and the website.

Usage:  python tools/security-smoke.py [http://localhost:5178]
"""
import json
import re
import sys
import time
import urllib.error
import urllib.request

API = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5178"
GUID = "00000000-0000-0000-0000-000000000001"
PUBLIC_PREFIXES = ("/api/v1/public/", "/api/v1/auth/", "/api/v1/dev/", "/health", "/api/v1/calendar/feed/", "/api/v1/newsletters/track", "/openapi", "/api/v1/forms/public")


def call(method, path, token=None, body=None, headers=None):
    request = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode() if body is not None else None)
    request.add_header("Content-Type", "application/json")
    request.add_header("X-Requested-With", "XMLHttpRequest")
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    for key, value in (headers or {}).items():
        request.add_header(key, value)
    for _ in range(8):
        try:
            with urllib.request.urlopen(request, timeout=30) as reply:
                return reply.status, dict(reply.headers)
        except urllib.error.HTTPError as error:
            if error.code == 429:
                time.sleep(10)
                continue
            return error.code, dict(error.headers)
        except Exception:
            return 0, {}
    return 429, {}


def login(role):
    request = urllib.request.Request(API + "/api/v1/dev/quick-login", method="POST", data=json.dumps({"role": role}).encode())
    request.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(request, timeout=30) as reply:
        return json.load(reply)["data"]["access_token"]


spec = json.load(urllib.request.urlopen(API + "/openapi/v1.json", timeout=30))
operations = [(method.upper(), path) for path, item in spec["paths"].items() for method in item if method in ("get", "post", "put", "delete", "patch")]
fill = lambda path: re.sub(r"\{[^}]+\}", GUID, path)
problems = []

print(f"{len(operations)} operations found.")
print("1. No sign-in")
open_ones = []
for method, path in operations:
    url = fill(path)
    if url.startswith(PUBLIC_PREFIXES):
        continue
    status, _ = call(method, url, body={} if method in ("POST", "PUT", "PATCH") else None)
    if status != 401:
        open_ones.append((method, url, status))
for item in open_ones:
    print("   NOT PROTECTED:", *item)
problems += open_ones
print(f"   {len(operations) - len(open_ones)} of {len(operations)} answered 401 or are public by design.")

print("2. Read endpoints as a parent (200 = parent can read; confirm each is meant for parents)")
parent = login("parent")
reads = []
for method, path in operations:
    if method != "GET" or path.startswith(PUBLIC_PREFIXES):
        continue
    status, _ = call("GET", fill(path), token=parent)
    if status == 200:
        reads.append(path)
for path in reads:
    print("   200:", path)

print("3. Odd input")
it = login("it")
odd = ["?q=%27%22%3B--", "?limit=999999", "?cursor=%00%00", "?status=" + "A" * 3000, "?from=not-a-date"]
for method, path in operations:
    if method != "GET" or "{" in path or path.startswith(PUBLIC_PREFIXES):
        continue
    for suffix in odd:
        status, _ = call("GET", path + suffix, token=it)
        if status >= 500:
            problems.append((method, path + suffix[:20], status))
            print("   SERVER ERROR:", method, path + suffix[:20], status)
print("   done")

print("4. Headers")
for label, url in (("api", API + "/api/v1/dev/test-users"), ("web", "http://localhost:5173/")):
    try:
        with urllib.request.urlopen(url, timeout=10) as reply:
            headers = {k.lower(): v for k, v in reply.headers.items()}
    except urllib.error.HTTPError as error:
        headers = {k.lower(): v for k, v in error.headers.items()}
    except Exception:
        print(f"   {label}: not reachable (skipped)")
        continue
    for name in ("x-content-type-options", "x-frame-options", "referrer-policy", "content-security-policy", "strict-transport-security"):
        print(f"   {label} {name}: {headers.get(name, 'MISSING')}")

print("RESULT:", "problems found" if problems else "no problems found", len(problems))
sys.exit(1 if problems else 0)
