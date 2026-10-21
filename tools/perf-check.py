"""Measures how fast the main screens answer when several people use the system at once (development helper).

Signs in through the local test-user helper, then asks each screen's data many times in parallel and prints the median,
95th percentile and slowest answers. The pilot gate is: 95th percentile under 2.5 seconds.

Usage:  python tools/perf-check.py [--users 10] [--requests 100] [--api http://localhost:5178]
"""
import argparse
import json
import random
import statistics
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor

PATHS = [
    "/api/v1/dashboard/attendance-summary",
    "/api/v1/attendance/ratio/live",
    "/api/v1/attendance?limit=25",
    "/api/v1/students?limit=25",
    "/api/v1/staff?limit=25",
    "/api/v1/activities?limit=25",
    "/api/v1/messages/threads",
    "/api/v1/notifications/unread-count",
    "/api/v1/attendance/report/monthly?month={month}",
    "/api/v1/dashboard/billing-snapshot",
    "/api/v1/invoices?limit=25",
    "/api/v1/enquiries?limit=25",
    "/api/v1/enquiries/summary",
    "/api/v1/calendar/events?from={first}&to={last}",
    "/api/v1/calendar/agenda?from={first}&to={last}",
    "/api/v1/forms?limit=25",
    "/api/v1/reports/analytics?from={first}&to={last}",
    "/api/v1/newsletters?limit=25",
    "/api/v1/resources/documents?limit=25",
]


def call(api, path, token, think=0.0):
    if think:
        time.sleep(random.uniform(0, think))
    request = urllib.request.Request(api + path, headers={"Authorization": f"Bearer {token}"})
    started = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            response.read()
            status = response.status
    except Exception as error:  # a failed answer counts as slow and is reported
        status = getattr(error, "code", 0)
    return time.perf_counter() - started, status


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--api", default="http://localhost:5178")
    parser.add_argument("--users", type=int, default=10)
    parser.add_argument("--requests", type=int, default=100)
    parser.add_argument("--limit", type=float, default=2.5)
    parser.add_argument("--think", type=float, default=0.0, help="each caller waits a random 0..N seconds before asking, like a person")
    args = parser.parse_args()

    login = urllib.request.Request(
        args.api + "/api/v1/dev/quick-login", data=json.dumps({"role": "it"}).encode(), headers={"Content-Type": "application/json"}
    )
    token = json.load(urllib.request.urlopen(login, timeout=30))["data"]["access_token"]
    month = time.strftime("%Y-%m")
    first, last = month + "-01", month + "-28"

    failed_gate = False
    print(f"{'screen data':58} {'p50':>7} {'p95':>7} {'p99':>7} {'max':>7}  errors")
    for template in PATHS:
        path = template.format(month=month, first=first, last=last)
        with ThreadPoolExecutor(max_workers=args.users) as pool:
            results = list(pool.map(lambda _: call(args.api, path, token, args.think), range(args.requests)))
        times = sorted(t for t, _ in results)
        errors = sum(1 for _, s in results if s != 200)
        p95 = times[int(len(times) * 0.95) - 1]
        p99 = times[min(len(times) - 1, int(len(times) * 0.99))]
        failed_gate |= p95 > args.limit or errors > 0
        print(f"{template[:58]:58} {statistics.median(times):6.3f}s {p95:6.3f}s {p99:6.3f}s {times[-1]:6.3f}s  {errors}")
    print("\nGATE:", "FAILED" if failed_gate else f"passed (95th percentile under {args.limit}s, no errors)")


if __name__ == "__main__":
    main()
