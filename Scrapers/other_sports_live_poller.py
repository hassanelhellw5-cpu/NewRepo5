"""Poll other-sports sources continuously and push near-live updates.

This is intentionally polling, not true push streaming. Run it from a scheduler
or a long-running process while events are live:

  API_DOMAIN=http://localhost:5000 LIVE_UPDATE_INTERVAL_SECONDS=30 \
    python Scrapers/other_sports_live_poller.py

It refreshes the normalized other-sports feed using other_sports_importer.py,
then reads /api/other-sports/events/live and posts one de-duplicated heartbeat
update per live event to /api/other-sports/events/{id}/live-updates. Real
point/goal/card granularity still depends on the upstream source payload.
"""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
import time
from datetime import datetime, timezone
from typing import Any
from urllib.parse import urljoin

import requests

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
IMPORT_API_KEY = os.getenv("IMPORT_API_KEY", "").strip()
INTERVAL_SECONDS = int(os.getenv("LIVE_UPDATE_INTERVAL_SECONDS", "30"))
RUN_ONCE = os.getenv("LIVE_UPDATE_ONCE", "0").strip().lower() in {"1", "true", "yes"}
SPORT_KEY = os.getenv("OTHER_SPORTS_LIVE_SPORT_KEY", "").strip().lower()
TIMEOUT = int(os.getenv("OTHER_SPORTS_TIMEOUT_SECONDS", "20"))


def _headers() -> dict[str, str]:
    headers = {"Content-Type": "application/json"}
    if IMPORT_API_KEY:
        headers["X-Import-Key"] = IMPORT_API_KEY
    return headers


def _get_json(path: str) -> Any:
    response = requests.get(urljoin(API_DOMAIN + "/", path.lstrip("/")), timeout=TIMEOUT)
    response.raise_for_status()
    return response.json()


def _post_json(path: str, payload: Any) -> bool:
    response = requests.post(urljoin(API_DOMAIN + "/", path.lstrip("/")), json=payload, headers=_headers(), timeout=TIMEOUT)
    if response.status_code >= 400:
        print(f"[other-sports-live] POST {path} failed: {response.status_code} {response.text[:300]}", file=sys.stderr)
        return False
    return True


def _signature(event: dict[str, Any]) -> str:
    relevant = {
        "status": event.get("status"),
        "results": event.get("results") or [],
        "metadataJson": event.get("metadataJson") or "{}",
    }
    return hashlib.sha1(json.dumps(relevant, ensure_ascii=False, sort_keys=True, default=str).encode("utf-8")).hexdigest()


def run_once() -> int:
    env = os.environ.copy()
    env["SCRAPER_DATE"] = datetime.now(timezone.utc).date().isoformat()
    completed = subprocess.run([sys.executable, os.path.join(os.path.dirname(__file__), "other_sports_importer.py")], env=env)
    live_path = "/api/other-sports/events/live" + (f"?sportKey={SPORT_KEY}" if SPORT_KEY else "")
    live_events = _get_json(live_path)
    posted = 0
    for event in live_events if isinstance(live_events, list) else []:
        event_id = event.get("id") or event.get("Id")
        if not event_id:
            continue
        sig = _signature(event)
        title = event.get("title") or event.get("Title") or "Live update"
        results = event.get("results") or []
        score_text = " - ".join(str(r.get("score") or r.get("Score") or r.get("resultText") or "") for r in results if isinstance(r, dict)).strip(" -")
        update = {
            "externalId": f"poll-{event_id}-{sig}",
            "minuteOrLap": event.get("time") or event.get("Time") or "",
            "type": "poll-scoreboard",
            "title": str(title),
            "detail": score_text or str(event.get("status") or event.get("Status") or "Live"),
            "publishedAt": datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z"),
        }
        if _post_json(f"/api/other-sports/events/{event_id}/live-updates", [update]):
            posted += 1
    print(json.dumps({"importExitCode": completed.returncode, "liveEvents": len(live_events) if isinstance(live_events, list) else 0, "updatesPosted": posted}, ensure_ascii=False))
    return 0 if completed.returncode == 0 else completed.returncode


def main() -> int:
    exit_code = 0
    while True:
        try:
            exit_code = run_once()
        except Exception as exc:
            exit_code = 1
            print(f"[other-sports-live] failed: {exc}", file=sys.stderr)
        if RUN_ONCE:
            return exit_code
        time.sleep(INTERVAL_SECONDS)


if __name__ == "__main__":
    raise SystemExit(main())
