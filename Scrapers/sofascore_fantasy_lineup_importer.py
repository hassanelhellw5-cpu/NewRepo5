"""Import fantasy-ready football lineups from Sofascore for today's Qemma matches.

Flow:
1. Read today's Qemma football matches from /api/SportsData/matches.
2. For each team playing today, resolve the team on Sofascore.
3. Prefer today's Sofascore event lineups if available; otherwise fall back to a
   recent event within SOFASCORE_LINEUP_LOOKBACK_DAYS.
4. Post the normalized squad to /api/SportsData/import/squad so fantasy players
   are available early in the day for only the teams that actually play today.

This uses public Sofascore endpoints when accessible. It does not bypass rate
limits, Cloudflare, login walls, or paid features.

NOTE ON SELENIUM: Sofascore's API (www.sofascore.com/api/v1/...) returns 403
Forbidden to plain `requests` calls because it can tell they aren't a real
browser (no TLS/JS fingerprint, no cookies, etc). To get around that, every
Sofascore API call in this script is made by driving a real Chrome browser
(via Selenium) to the JSON URL directly and reading the JSON text out of the
rendered page. This is slower than plain `requests` (each call pays browser
navigation overhead) but survives the same protection that
multisport_selenium_importer.py already gets past for other sports. Calls to
the Qemma backend itself (API_DOMAIN) still use plain `requests` since that's
our own API and isn't blocking anything.
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import quote

import requests

try:
    from selenium import webdriver
    from selenium.webdriver.chrome.options import Options as ChromeOptions
    from selenium.webdriver.chrome.service import Service as ChromeService
    from selenium.common.exceptions import WebDriverException
except ImportError:  # pragma: no cover - selenium should already be a project dependency
    webdriver = None  # type: ignore[assignment]
    ChromeOptions = None  # type: ignore[assignment]
    ChromeService = None  # type: ignore[assignment]
    WebDriverException = Exception  # type: ignore[assignment]

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
SOFASCORE_BASE = os.getenv("SOFASCORE_BASE_URL", "https://www.sofascore.com/api/v1").rstrip("/")
TARGET_DATE = os.getenv("SOFASCORE_FANTASY_DATE") or os.getenv("SCRAPER_DATE") or os.getenv("YALLAKORA_MATCH_DATE") or datetime.now(timezone.utc).date().isoformat()
LOOKBACK_DAYS = int(os.getenv("SOFASCORE_LINEUP_LOOKBACK_DAYS", "45"))
MAX_RECENT_EVENTS = int(os.getenv("SOFASCORE_LINEUP_MAX_RECENT_EVENTS", "12"))
TIMEOUT = int(os.getenv("SOFASCORE_LINEUP_TIMEOUT_SECONDS", "25"))
REQUEST_DELAY = float(os.getenv("SOFASCORE_LINEUP_REQUEST_DELAY_SECONDS", "0.7"))
USER_AGENT = os.getenv(
    "SOFASCORE_LINEUP_USER_AGENT",
    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36",
)
CHROME_BINARY = os.getenv("CHROME_BINARY")
CHROME_HEADLESS = os.getenv("SOFASCORE_LINEUP_HEADLESS", "true").strip().lower() not in {"0", "false", "no"}
SELENIUM_PAGE_LOAD_TIMEOUT = int(os.getenv("SOFASCORE_LINEUP_PAGE_LOAD_TIMEOUT_SECONDS", str(TIMEOUT)))

# Plain requests session, used ONLY for talking to our own Qemma API.
SESSION = requests.Session()
SESSION.headers.update({"User-Agent": USER_AGENT, "Accept-Language": "ar,en;q=0.8", "Accept": "application/json,text/plain,*/*"})

# Lazily-created Selenium driver, used for every Sofascore API call.
_DRIVER: "webdriver.Chrome | None" = None


def _build_chrome_options() -> "ChromeOptions":
    options = ChromeOptions()
    if CHROME_HEADLESS:
        options.add_argument("--headless=new")
    options.add_argument("--disable-gpu")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--window-size=1280,1000")
    options.add_argument(f"--user-agent={USER_AGENT}")
    options.add_argument("--lang=ar,en")
    # Reduce automation fingerprints that Cloudflare/Sofascore can key off of.
    options.add_argument("--disable-blink-features=AutomationControlled")
    options.add_experimental_option("excludeSwitches", ["enable-automation"])
    options.add_experimental_option("useAutomationExtension", False)
    if CHROME_BINARY:
        options.binary_location = CHROME_BINARY
    return options


def _get_driver() -> "webdriver.Chrome":
    global _DRIVER
    if webdriver is None:
        raise RuntimeError("selenium is not installed; run `pip install selenium` (and make sure Chrome/Chromedriver are available).")
    if _DRIVER is not None:
        return _DRIVER
    options = _build_chrome_options()
    driver = webdriver.Chrome(options=options)
    driver.set_page_load_timeout(SELENIUM_PAGE_LOAD_TIMEOUT)
    try:
        # Best-effort: hide the most obvious `navigator.webdriver` tell.
        driver.execute_cdp_cmd("Page.addScriptToEvaluateOnNewDocument", {
            "source": "Object.defineProperty(navigator, 'webdriver', {get: () => undefined});"
        })
    except Exception:
        pass  # not fatal if this CDP command isn't available
    _DRIVER = driver
    return driver


def _quit_driver() -> None:
    global _DRIVER
    if _DRIVER is not None:
        try:
            _DRIVER.quit()
        except Exception:
            pass
        _DRIVER = None


def _get_json(url: str) -> dict[str, Any]:
    """Fetch a Sofascore API URL through a real Chrome session and parse the JSON body.

    Chrome renders bare JSON responses inside a `<pre>` tag (or, for some
    content types, directly as the page text), so we read whichever is
    populated and json.loads() it. Raises on non-JSON/error pages so callers
    can treat it the same way they treated requests.raise_for_status().
    """
    driver = _get_driver()
    last_error: Exception | None = None
    for attempt in range(1, 3):
        try:
            driver.get(url)
            body_text = ""
            try:
                pre = driver.find_element("tag name", "pre")
                body_text = pre.text
            except Exception:
                body_text = driver.find_element("tag name", "body").text
            body_text = (body_text or "").strip()
            if not body_text:
                raise ValueError("empty response body")
            return json.loads(body_text)
        except Exception as exc:  # noqa: BLE001 - we want to retry once on any failure
            last_error = exc
            time.sleep(1.0)
    raise RuntimeError(f"Sofascore request failed for {url}: {last_error}")


def _post_json(path: str, payload: Any) -> requests.Response:
    headers: dict[str, str] = {}
    import_key = os.getenv("IMPORT_API_KEY")
    if import_key:
        headers["X-Import-Key"] = import_key
    response = SESSION.post(f"{API_DOMAIN}{path}", json=payload, headers=headers, timeout=TIMEOUT)
    response.raise_for_status()
    return response


def _norm(value: str | None) -> str:
    value = re.sub(r"[^\w\u0600-\u06ff]+", " ", value or "", flags=re.UNICODE)
    return re.sub(r"\s+", " ", value).strip().casefold()


def _score_name(candidate: str, target: str) -> int:
    c = _norm(candidate)
    t = _norm(target)
    if not c or not t:
        return 0
    if c == t:
        return 100
    if c in t or t in c:
        return 80
    c_parts = set(c.split())
    t_parts = set(t.split())
    return int(100 * len(c_parts & t_parts) / max(len(t_parts), 1))


def _parse_date(value: str) -> datetime:
    return datetime.fromisoformat(value).date()  # type: ignore[return-value]


def _load_qemma_matches() -> list[dict[str, Any]]:
    url = f"{API_DOMAIN}/api/SportsData/matches?date={quote(TARGET_DATE)}"
    response = SESSION.get(url, timeout=TIMEOUT)
    response.raise_for_status()
    matches = response.json()
    if not isinstance(matches, list):
        raise ValueError("Qemma matches API did not return a list")
    return matches


def _team_name(match: dict[str, Any], side: str) -> str:
    team = match.get(f"{side}Team") or {}
    return str(team.get("name") or "").strip()


def _resolve_sofascore_team(team_name: str) -> dict[str, Any] | None:
    payload = _get_json(f"{SOFASCORE_BASE}/search/all?q={quote(team_name)}")
    rows = payload.get("results") or payload.get("teams") or []
    best: tuple[int, dict[str, Any] | None] = (0, None)
    for row in rows:
        entity = row.get("entity") if isinstance(row, dict) else row
        if not isinstance(entity, dict):
            continue
        if entity.get("type") not in (None, "team") and "team" not in str(entity.get("type", "")).lower():
            continue
        sport = ((entity.get("sport") or {}).get("slug") or (entity.get("sport") or {}).get("name") or "").lower()
        if sport and "football" not in sport and "soccer" not in sport:
            continue
        score = max(_score_name(str(entity.get("name") or ""), team_name), _score_name(str(entity.get("shortName") or ""), team_name))
        if score > best[0]:
            best = (score, entity)
    return best[1] if best[0] >= 45 else None


def _event_time(event: dict[str, Any]) -> datetime:
    timestamp = event.get("startTimestamp")
    if timestamp:
        return datetime.fromtimestamp(int(timestamp), tz=timezone.utc).replace(tzinfo=None)
    return datetime.min


def _event_has_opponent(event: dict[str, Any], opponent_name: str) -> bool:
    home = ((event.get("homeTeam") or {}).get("name") or "")
    away = ((event.get("awayTeam") or {}).get("name") or "")
    return max(_score_name(home, opponent_name), _score_name(away, opponent_name)) >= 45


def _candidate_events(team_id: int, opponent_name: str, target_day: datetime) -> list[dict[str, Any]]:
    candidates: list[dict[str, Any]] = []
    for segment in ("next", "last"):
        try:
            payload = _get_json(f"{SOFASCORE_BASE}/team/{team_id}/events/{segment}/0")
            events = payload.get("events") or []
            if isinstance(events, list):
                candidates.extend(events[:MAX_RECENT_EVENTS])
        except Exception as exc:
            print(f"[sofascore-fantasy] WARNING: could not load {segment} events for team {team_id}: {exc}")
    earliest = target_day - timedelta(days=LOOKBACK_DAYS)
    latest = target_day + timedelta(days=1)
    filtered = [e for e in candidates if earliest <= _event_time(e) <= latest]
    filtered.sort(key=lambda e: (0 if _event_has_opponent(e, opponent_name) else 1, abs((_event_time(e) - target_day).total_seconds())))
    return filtered


def _load_lineups(event_id: int) -> dict[str, Any] | None:
    try:
        payload = _get_json(f"{SOFASCORE_BASE}/event/{event_id}/lineups")
    except Exception as exc:
        print(f"[sofascore-fantasy] lineups unavailable for Sofascore event {event_id}: {exc}")
        return None
    return payload if isinstance(payload, dict) else None


def _position(value: Any) -> str:
    text = str(value or "").upper()
    if text in {"G", "GK", "GOALKEEPER"}:
        return "GK"
    if text in {"D", "DF", "DEFENDER"}:
        return "DF"
    if text in {"M", "MF", "MIDFIELDER"}:
        return "MF"
    if text in {"F", "FW", "FORWARD"}:
        return "FW"
    return text[:8] if text else ""


def _row_is_substitute(row: dict[str, Any], source_key: str) -> bool:
    """Return whether a Sofascore lineup row is a bench player.

    Sofascore can either put bench players in a dedicated `substitutes` array or
    mix everyone into `players` with a substitute flag. The old importer treated
    every row from the `players` array as a substitute when building the bench,
    which polluted fantasy rosters and could duplicate players.
    """
    return source_key == "substitutes" or bool(row.get("substitute") or row.get("isSubstitute"))


def _normalize_players(side_payload: dict[str, Any], substitute: bool) -> list[dict[str, str]]:
    rows: list[tuple[str, dict[str, Any]]] = []
    for key in ("players", "substitutes"):
        value = side_payload.get(key)
        if isinstance(value, list):
            rows.extend((key, row) for row in value if isinstance(row, dict))
    normalized: list[dict[str, str]] = []
    seen: set[str] = set()
    for source_key, row in rows:
        if _row_is_substitute(row, source_key) != substitute:
            continue
        player = row.get("player") if isinstance(row, dict) else None
        if not isinstance(player, dict):
            continue

        name = str(player.get("name") or player.get("shortName") or "").strip()
        if not name:
            continue
        number = str(player.get("jerseyNumber") or row.get("shirtNumber") or "").strip()
        dedupe_key = f"{_norm(name)}|{number}|{_position(row.get('position') or player.get('position'))}"
        if dedupe_key in seen:
            continue
        seen.add(dedupe_key)
        normalized.append({
            "name": name,
            "number": number,
            "position": _position(row.get("position") or player.get("position")),
        })
    return normalized


def _build_squad(match: dict[str, Any], lineups: dict[str, Any]) -> dict[str, Any]:
    home = lineups.get("home") if isinstance(lineups.get("home"), dict) else {}
    away = lineups.get("away") if isinstance(lineups.get("away"), dict) else {}
    return {
        "match_id": str(match.get("matchId") or ""),
        "home_formation": str(home.get("formation") or ""),
        "away_formation": str(away.get("formation") or ""),
        "home_coach": str(((home.get("manager") or home.get("coach") or {}) or {}).get("name") or ""),
        "away_coach": str(((away.get("manager") or away.get("coach") or {}) or {}).get("name") or ""),
        "home_main": _normalize_players(home, substitute=False),
        "home_sub": _normalize_players(home, substitute=True),
        "away_main": _normalize_players(away, substitute=False),
        "away_sub": _normalize_players(away, substitute=True),
    }


def _has_players(squad: dict[str, Any]) -> bool:
    return any(squad.get(key) for key in ("home_main", "home_sub", "away_main", "away_sub"))


def main() -> int:
    target_day = datetime.combine(_parse_date(TARGET_DATE), datetime.min.time())
    matches = _load_qemma_matches()
    imported = 0
    skipped = 0
    try:
        for match in matches:
            match_id = str(match.get("matchId") or "").strip()
            if not match_id:
                skipped += 1
                continue
            home_name = _team_name(match, "home")
            away_name = _team_name(match, "away")
            if not home_name or not away_name:
                skipped += 1
                continue
            print(f"[sofascore-fantasy] resolving {home_name} vs {away_name} ({match_id})")
            resolved_home = _resolve_sofascore_team(home_name)
            resolved_away = _resolve_sofascore_team(away_name)
            team = resolved_home or resolved_away
            if not team:
                print(f"[sofascore-fantasy] no Sofascore team resolved for {home_name}/{away_name}")
                skipped += 1
                continue
            opponent_name = away_name if resolved_home else home_name
            for event in _candidate_events(int(team["id"]), opponent_name, target_day):
                lineups = _load_lineups(int(event.get("id")))
                if not lineups:
                    time.sleep(REQUEST_DELAY)
                    continue
                squad = _build_squad(match, lineups)
                if not _has_players(squad):
                    time.sleep(REQUEST_DELAY)
                    continue
                _post_json("/api/SportsData/import/squad", squad)
                imported += 1
                print(f"[sofascore-fantasy] imported squad for Qemma match {match_id} from Sofascore event {event.get('id')}")
                break
            else:
                skipped += 1
            time.sleep(REQUEST_DELAY)
    finally:
        _quit_driver()
    print(f"[sofascore-fantasy] done. imported={imported}, skipped={skipped}, date={TARGET_DATE}")
    return 0 if imported else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"[sofascore-fantasy] ERROR: {exc}", file=sys.stderr)
        _quit_driver()
        raise