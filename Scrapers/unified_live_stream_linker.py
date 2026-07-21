"""Link playable HLS streams to football matches and selected other-sports events.

Football keeps using the existing channel matching/import flow. Other sports use
rules that map an event (sport/competition/title/external id) to a channel name,
then reuse the same M3U/override matching, validation, and proxy URL builder.
"""

from __future__ import annotations

import json
import os
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import requests

from live_stream_scraper import LiveStreamScraper

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
IMPORT_API_KEY = os.getenv("IMPORT_API_KEY", "")
RULES_FILE = Path(os.getenv("OTHER_SPORTS_LIVE_RULES_FILE", str(Path(__file__).with_name("other_sports_live_rules.json"))))
SPORT_KEYS = [s.strip().lower() for s in os.getenv("AUTO_LIVE_OTHER_SPORTS", "formula1,tennis,basketball").split(",") if s.strip()]
MAX_STREAMS_PER_EVENT = int(os.getenv("MAX_STREAM_SERVERS_PER_OTHER_SPORT_EVENT", "3"))


def _load_rules() -> list[dict[str, Any]]:
    if not RULES_FILE.exists():
        example = RULES_FILE.with_name("other_sports_live_rules.example.json")
        print(f"[unified-live] rules file not found: {RULES_FILE}. Copy {example} to enable other-sports auto linking.")
        return []
    data = json.loads(RULES_FILE.read_text(encoding="utf-8"))
    if not isinstance(data, list):
        raise ValueError("OTHER_SPORTS_LIVE_RULES_FILE must contain a list of rules.")
    return data


def _matches_rule(event: dict[str, Any], rule: dict[str, Any]) -> bool:
    sport_key = str(rule.get("sportKey") or "").strip().lower()
    if sport_key and sport_key != str(event.get("sportKey") or "").strip().lower():
        return False
    competition = str(rule.get("competitionName") or "").strip().lower()
    if competition and competition not in str(event.get("competitionName") or "").strip().lower():
        return False
    title_contains = str(rule.get("titleContains") or "").strip().lower()
    if title_contains and title_contains not in str(event.get("title") or "").strip().lower():
        return False
    external_id = str(rule.get("externalId") or "").strip().lower()
    if external_id and external_id != str(event.get("externalId") or "").strip().lower():
        return False
    return True


def _is_closed(event: dict[str, Any]) -> bool:
    status = str(event.get("status") or "").strip().lower()
    return any(token in status for token in ("completed", "finished", "ended", "ft", "انتهت", "انتهى"))


def _get_other_sport_events() -> list[dict[str, Any]]:
    day = os.getenv("OTHER_SPORTS_LIVE_DATE", datetime.now(timezone.utc).date().isoformat())
    events: list[dict[str, Any]] = []
    for sport_key in SPORT_KEYS:
        response = requests.get(f"{API_DOMAIN}/api/other-sports/events", params={"date": day, "sportKey": sport_key}, timeout=20)
        response.raise_for_status()
        rows = response.json()
        if isinstance(rows, list):
            events.extend(rows)
    return events


def _headers() -> dict[str, str]:
    return {"X-Import-Key": IMPORT_API_KEY} if IMPORT_API_KEY else {}


def _post_other_sport_streams(event_id: int, streams: list[dict[str, Any]]) -> None:
    payload = [
        {
            "source": stream.get("source") or "Unified live linker",
            "streamUrl": stream.get("stream_url") or stream.get("m3u8_url") or "",
            "m3U8Url": stream.get("m3u8_url") or stream.get("stream_url") or "",
            "statusMessage": stream.get("status_message") or "auto-linked live stream",
        }
        for stream in streams
    ]
    response = requests.post(f"{API_DOMAIN}/api/other-sports/events/{event_id}/streams", json=payload, headers=_headers(), timeout=30)
    response.raise_for_status()


def link_other_sports(scraper: LiveStreamScraper) -> list[dict[str, Any]]:
    rules = _load_rules()
    if not rules:
        return []

    events = _get_other_sport_events()
    channels = scraper.parse_overrides() + scraper.parse_m3u()
    linked: list[dict[str, Any]] = []

    for event in events:
        event_id = event.get("id")
        if not event_id:
            continue
        if _is_closed(event):
            _post_other_sport_streams(int(event_id), [])
            linked.append({"eventId": event_id, "title": event.get("title"), "streams": 0, "status": "closed"})
            continue

        rule = next((r for r in rules if _matches_rule(event, r)), None)
        if not rule:
            continue

        channel_name = str(rule.get("channelName") or rule.get("channel") or "").strip()
        if not channel_name:
            continue

        candidates = scraper.find_channel_candidates(channel_name, channels)
        streams: list[dict[str, Any]] = []
        last_message = "no candidate checked"
        for candidate in candidates:
            if len(streams) >= MAX_STREAMS_PER_EVENT:
                break
            stream_url = candidate.get("url") or candidate.get("m3U8Url") or candidate.get("m3u8Url")
            candidate_headers = candidate.get("headers") or {}
            is_valid, last_message = scraper.validate_stream_url(stream_url, candidate_headers)
            if not is_valid:
                continue
            proxied_url = scraper.build_proxy_url(stream_url, candidate_headers)
            streams.append({
                "source": candidate.get("source") or candidate.get("name") or "Unified live linker",
                "stream_url": proxied_url,
                "m3u8_url": proxied_url,
                "status_message": f"matched channel '{channel_name}'",
            })

        if not streams:
            streams = [{"source": "LIVE_OFFLINE", "stream_url": "", "m3u8_url": "", "status_message": f"No playable stream for '{channel_name}'. Last check: {last_message}"}]

        _post_other_sport_streams(int(event_id), streams)
        linked.append({"eventId": event_id, "title": event.get("title"), "channel": channel_name, "streams": len([s for s in streams if s.get("m3u8_url")])})

    return linked


def main() -> int:
    scraper = LiveStreamScraper(api_base_url=API_DOMAIN)

    football_streams = scraper.get_live_matches()
    if football_streams:
        scraper.send_to_api(football_streams)

    other_sports = link_other_sports(scraper)
    print(json.dumps({"footballStreams": len(football_streams), "otherSports": other_sports}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"[unified-live] ERROR: {exc}", file=sys.stderr)
        raise
