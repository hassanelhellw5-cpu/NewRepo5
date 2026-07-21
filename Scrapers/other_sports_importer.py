"""Basic normalized-feed importer for non-football sports NOT already covered
by multisport_selenium_importer.py, plus the shared helper functions that
script imports (_post_json, _load_stream_overrides, _attach_stream_overrides).

Why this file exists at all:
  multisport_selenium_importer.py already covers tennis, basketball, and
  Formula 1 via a single Selenium session (ATP/WTA/Flashscore/Sofascore/
  formula1.com/Motorsport.com). This file does NOT re-scrape those — that
  would just duplicate work and double-post the same events.

  What's left uncovered is ice_hockey (listed in appsettings.json under
  OtherSports:TheSportsDbSports but never actually scraped anywhere). This
  file imports that with a lightweight ESPN scoreboard JSON endpoint first,
  then falls back to plain requests + BeautifulSoup (no Selenium needed).

  It also hosts the 3 helper functions multisport_selenium_importer.py
  imports, so that script stops crashing with ModuleNotFoundError.

Posts to:
  - /api/other-sports/import/events   (ice hockey events)

Config (all optional, sensible defaults):
  ICE_HOCKEY_API_URL               - ESPN scoreboard JSON endpoint
  ICE_HOCKEY_SOURCE_URL            - page to scrape (default: ESPN NHL scoreboard)
  ICE_HOCKEY_ENABLED               - "true"/"false" (default: true)
  OTHER_SPORTS_STREAM_OVERRIDES_FILE - path to a JSON file with manual stream
                                        overrides (default: other_sports_stream_overrides.json)
  IMPORT_API_KEY                   - must match ImportApiKey / X-Import-Key
                                        expected by OtherSportsController.cs
  API_DOMAIN                       - base URL of the API (default: http://qemma.runasp.net)
"""

from __future__ import annotations

import json
import os
import re
import sys
from datetime import datetime, timezone
from typing import Any
from urllib.parse import urljoin

import requests
from bs4 import BeautifulSoup

# ---------------------------------------------------------------------------
# General config
# ---------------------------------------------------------------------------
API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
IMPORT_API_KEY = os.getenv("IMPORT_API_KEY", "").strip()
TIMEOUT = int(os.getenv("OTHER_SPORTS_TIMEOUT_SECONDS", "20"))
MAX_ITEMS_PER_SOURCE = int(os.getenv("OTHER_SPORTS_MAX_ITEMS_PER_SOURCE", "20"))

STREAM_OVERRIDES_FILE = os.getenv(
    "OTHER_SPORTS_STREAM_OVERRIDES_FILE",
    os.path.join(os.path.dirname(os.path.abspath(__file__)), "other_sports_stream_overrides.json"),
)

DEFAULT_HEADERS = {
    "User-Agent": os.getenv(
        "OTHER_SPORTS_USER_AGENT",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36",
    ),
    "Accept-Language": "ar,en;q=0.8",
}

ICE_HOCKEY_ENABLED = os.getenv("ICE_HOCKEY_ENABLED", "true").strip().lower() != "false"
ICE_HOCKEY_SOURCE_URL = os.getenv("ICE_HOCKEY_SOURCE_URL", "https://www.espn.com/nhl/scoreboard").strip()
ICE_HOCKEY_SOURCE_NAME = os.getenv("ICE_HOCKEY_SOURCE_NAME", "ESPN NHL").strip()
ICE_HOCKEY_API_URL = os.getenv(
    "ICE_HOCKEY_API_URL",
    "https://site.api.espn.com/apis/site/v2/sports/hockey/nhl/scoreboard",
).strip()

BASKETBALL_ENABLED = os.getenv("BASKETBALL_ENABLED", "true").strip().lower() != "false"
BASKETBALL_API_URL = os.getenv(
    "BASKETBALL_API_URL",
    "https://site.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard",
).strip()
BASKETBALL_SOURCE_NAME = os.getenv("BASKETBALL_SOURCE_NAME", "ESPN NBA").strip()

TENNIS_ENABLED = os.getenv("TENNIS_ENABLED", "true").strip().lower() != "false"
SCORES365_ENABLED = os.getenv("SCORES365_ENABLED", "true").strip().lower() != "false"
SCORES365_API_URL = os.getenv("SCORES365_API_URL", "https://webws.365scores.com/web/games/allscores/").strip()
# 365Scores currently uses numeric sport ids in its public web payloads. These
# envs are deliberately configurable in case ids change.
SCORES365_BASKETBALL_SPORT_ID = os.getenv("SCORES365_BASKETBALL_SPORT_ID", "2").strip()
SCORES365_TENNIS_SPORT_ID = os.getenv("SCORES365_TENNIS_SPORT_ID", "3").strip()
SCORES365_LANG_ID = os.getenv("SCORES365_LANG_ID", "27").strip()  # Arabic

FORMULA1_ENABLED = os.getenv("FORMULA1_ENABLED", "true").strip().lower() != "false"
FORMULA1_SEASON = os.getenv("FORMULA1_SEASON", str(_utc_now().year) if "_utc_now" in globals() else str(datetime.now().year)).strip()
FORMULA1_SCHEDULE_API_URL = os.getenv("FORMULA1_SCHEDULE_API_URL", f"https://api.jolpi.ca/ergast/f1/{FORMULA1_SEASON}/races/").strip()
FORMULA1_RESULTS_API_URL = os.getenv("FORMULA1_RESULTS_API_URL", f"https://api.jolpi.ca/ergast/f1/{FORMULA1_SEASON}/results/").strip()
FORMULA1_DRIVER_STANDINGS_API_URL = os.getenv("FORMULA1_DRIVER_STANDINGS_API_URL", f"https://api.jolpi.ca/ergast/f1/{FORMULA1_SEASON}/driverstandings/").strip()
FORMULA1_CONSTRUCTOR_STANDINGS_API_URL = os.getenv("FORMULA1_CONSTRUCTOR_STANDINGS_API_URL", f"https://api.jolpi.ca/ergast/f1/{FORMULA1_SEASON}/constructorstandings/").strip()


# ---------------------------------------------------------------------------
# Shared helpers — imported by multisport_selenium_importer.py
# ---------------------------------------------------------------------------
def _post_json(path: str, payload: list[dict[str, Any]] | dict[str, Any]) -> bool:
    """POST a JSON payload to the Qemma API. Returns True on success (2xx),
    False otherwise. Never raises — a failed post here should not crash the
    whole scraping pipeline (run_all.py treats non-zero exit as a warning,
    not a hard stop)."""
    if not payload:
        print(f"[other_sports] skip POST {path}: empty payload")
        return True

    url = urljoin(API_DOMAIN + "/", path.lstrip("/"))
    headers = {"Content-Type": "application/json"}
    if IMPORT_API_KEY:
        headers["X-Import-Key"] = IMPORT_API_KEY

    try:
        response = requests.post(url, json=payload, headers=headers, timeout=TIMEOUT)
        if response.status_code >= 400:
            print(f"[other_sports] POST {path} failed: {response.status_code} {response.text[:300]}", file=sys.stderr)
            return False
        print(f"[other_sports] POST {path}: {response.status_code} ({len(payload) if isinstance(payload, list) else 1} item(s))")
        return True
    except Exception as exc:
        print(f"[other_sports] POST {path} error: {exc}", file=sys.stderr)
        return False


def _load_stream_overrides() -> list[dict[str, Any]]:
    """Load manual stream overrides from a local JSON file. Format expected:

    [
      {
        "sportKey": "formula1",
        "externalId": "motorsport-f1-2026-...",   // optional, exact match
        "titleContains": "Abu Dhabi Grand Prix",  // optional, case-insensitive substring match
        "streams": [
          {"source": "Backup Stream", "streamUrl": "...", "m3U8Url": "...", "statusMessage": "manual override"}
        ]
      }
    ]

    Missing file or invalid JSON just means "no overrides" — this never
    raises, since overrides are optional and shouldn't break imports.
    """
    if not STREAM_OVERRIDES_FILE or not os.path.exists(STREAM_OVERRIDES_FILE):
        return []
    try:
        with open(STREAM_OVERRIDES_FILE, "r", encoding="utf-8") as handle:
            data = json.load(handle)
        return data if isinstance(data, list) else []
    except Exception as exc:
        print(f"[other_sports] WARNING: could not read stream overrides file: {exc}", file=sys.stderr)
        return []


def _attach_stream_overrides(event: dict[str, Any], overrides: list[dict[str, Any]]) -> dict[str, Any]:
    """Return a copy of `event` with any matching manual override streams
    appended. Matching priority: exact externalId match, then sportKey +
    case-insensitive title substring match. An event can match at most one
    override entry (first match wins)."""
    if not overrides:
        return event

    event_copy = dict(event)
    sport_key = str(event_copy.get("sportKey") or "").strip().lower()
    external_id = str(event_copy.get("externalId") or "").strip()
    title_lower = str(event_copy.get("title") or "").strip().lower()

    for override in overrides:
        override_sport = str(override.get("sportKey") or "").strip().lower()
        if override_sport and override_sport != sport_key:
            continue

        override_external_id = str(override.get("externalId") or "").strip()
        title_contains = str(override.get("titleContains") or "").strip().lower()

        matched = False
        if override_external_id and override_external_id == external_id:
            matched = True
        elif title_contains and title_contains in title_lower:
            matched = True

        if not matched:
            continue

        new_streams = override.get("streams") or []
        if not isinstance(new_streams, list):
            continue

        existing_streams = list(event_copy.get("streams") or [])
        existing_urls = {s.get("streamUrl") for s in existing_streams if isinstance(s, dict)}
        for stream in new_streams:
            if isinstance(stream, dict) and stream.get("streamUrl") not in existing_urls:
                existing_streams.append(stream)
        event_copy["streams"] = existing_streams
        break  # first matching override wins

    return event_copy


# ---------------------------------------------------------------------------
# Small local helpers (kept independent from multisport_selenium_importer.py
# on purpose — no cross-import back the other way).
# ---------------------------------------------------------------------------
def _compact(value: str | None) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def _slug(value: str) -> str:
    normalized = re.sub(r"[^\w\u0600-\u06ff]+", "-", value.lower(), flags=re.UNICODE).strip("-")
    return normalized[:90] or "item"


def _utc_now() -> datetime:
    return datetime.now(timezone.utc).replace(tzinfo=None, microsecond=0)


def _parse_api_datetime(value: str | None) -> datetime:
    if not value:
        return _utc_now()
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
        return parsed.astimezone(timezone.utc).replace(tzinfo=None, microsecond=0)
    except ValueError:
        return _utc_now()


def _event_status(text: str) -> str:
    lower = text.lower()
    if any(word in lower for word in ["live", "مباشر", "جارية", "period", "ot", "so"]):
        return "Live"
    if any(word in lower for word in ["final", "ft", "انتهت"]):
        return "Completed"
    return "Scheduled"


def _split_competitors(title: str) -> list[dict[str, str]]:
    separators = [" vs ", " v ", " @ ", " - ", "–", " ضد "]
    for sep in separators:
        if sep in title:
            parts = [_compact(p) for p in title.split(sep, 1)]
            if len(parts) == 2 and all(parts):
                return [
                    {"name": parts[0], "teamName": parts[0], "country": "", "role": "home"},
                    {"name": parts[1], "teamName": parts[1], "country": "", "role": "away"},
                ]
    return []



def _target_day() -> datetime:
    value = os.getenv("SCRAPER_DATE") or os.getenv("YALLAKORA_MATCH_DATE")
    if value:
        try:
            return datetime.fromisoformat(value.strip()).replace(tzinfo=None)
        except ValueError:
            pass
    return _utc_now()


def _scrape_espn_scoreboard(api_url: str, sport_key: str, source_name: str) -> list[dict[str, Any]]:
    if not api_url:
        return []

    params = {"dates": _target_day().strftime("%Y%m%d")}
    try:
        response = requests.get(api_url, headers=DEFAULT_HEADERS, params=params, timeout=TIMEOUT)
        response.raise_for_status()
        data = response.json()
    except Exception as exc:
        print(f"[other_sports] {sport_key} ESPN API: fetch failed: {exc}", file=sys.stderr)
        return []

    league_name = source_name
    leagues = data.get("leagues")
    if isinstance(leagues, list) and leagues and isinstance(leagues[0], dict):
        league_name = _compact(leagues[0].get("name") or source_name)

    events: list[dict[str, Any]] = []
    for item in data.get("events") or []:
        competitions = item.get("competitions") if isinstance(item, dict) else None
        competition = competitions[0] if isinstance(competitions, list) and competitions else {}
        competitors = competition.get("competitors") if isinstance(competition, dict) else []
        participants: list[dict[str, str]] = []
        names: list[str] = []
        results: list[dict[str, Any]] = []
        for competitor in competitors if isinstance(competitors, list) else []:
            team = competitor.get("team") if isinstance(competitor, dict) else {}
            name = _compact(team.get("displayName") or team.get("name") or competitor.get("displayName"))
            if not name:
                continue
            role = _compact(competitor.get("homeAway"))
            names.append(name)
            participants.append({"name": name, "teamName": name, "country": "", "role": role})
            score = competitor.get("score")
            if score not in (None, ""):
                results.append({"participantName": name, "score": str(score), "resultText": str(score)})

        title = _compact(item.get("name") or item.get("shortName") or (" vs ".join(names) if len(names) >= 2 else ""))
        if not title:
            continue

        event_date = _parse_api_datetime(item.get("date") or competition.get("date"))
        status_info = item.get("status") if isinstance(item.get("status"), dict) else {}
        status_type = status_info.get("type") if isinstance(status_info.get("type"), dict) else {}
        status = _event_status(_compact(status_type.get("description") or status_type.get("name") or status_info.get("displayClock")))
        if status_type.get("completed") is True:
            status = "Completed"

        external_id_value = _compact(item.get("id"))
        external_id = f"espn-{sport_key}-{external_id_value}" if external_id_value else f"espn-{sport_key}-{_slug(title)}-{event_date.strftime('%Y%m%d%H%M')}"
        link = api_url
        links = item.get("links") if isinstance(item.get("links"), list) else []
        if links and isinstance(links[0], dict):
            link = links[0].get("href") or link

        venue_info = competition.get("venue") if isinstance(competition.get("venue"), dict) else {}
        events.append({
            "sportKey": sport_key,
            "externalId": external_id,
            "title": title,
            "competitionName": league_name,
            "eventDate": event_date.isoformat() + "Z",
            "status": status,
            "time": event_date.strftime("%H:%M UTC"),
            "venue": _compact(venue_info.get("fullName")),
            "country": "",
            "source": source_name,
            "sourceUrl": link,
            "participants": participants,
            "results": results,
            "streams": [],
        })
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break

    print(f"[other_sports] {sport_key} ESPN API: parsed {len(events)} events")
    return events


def _scrape_365scores(sport_key: str, sport_id: str) -> list[dict[str, Any]]:
    if not SCORES365_ENABLED or not SCORES365_API_URL or not sport_id:
        return []
    day = _target_day().strftime("%Y-%m-%d")
    params = {
        "appTypeId": "5",
        "langId": SCORES365_LANG_ID,
        "timezoneName": "UTC",
        "sports": sport_id,
        "startDate": day,
        "endDate": day,
        "withTop": "true",
    }
    try:
        response = requests.get(SCORES365_API_URL, headers=DEFAULT_HEADERS, params=params, timeout=TIMEOUT)
        response.raise_for_status()
        data = response.json()
    except Exception as exc:
        print(f"[other_sports] {sport_key} 365Scores API: fetch failed: {exc}", file=sys.stderr)
        return []

    games = data.get("games") or data.get("Games") or []
    competitions = {str(c.get("id") or c.get("Id")): c for c in (data.get("competitions") or data.get("Competitions") or []) if isinstance(c, dict)}
    competitors_by_id = {str(c.get("id") or c.get("Id")): c for c in (data.get("competitors") or data.get("Competitors") or []) if isinstance(c, dict)}
    events: list[dict[str, Any]] = []
    for game in games if isinstance(games, list) else []:
        if not isinstance(game, dict):
            continue
        home = game.get("homeCompetitor") or game.get("homeCompetitorId") or game.get("HomeCompetitorId")
        away = game.get("awayCompetitor") or game.get("awayCompetitorId") or game.get("AwayCompetitorId")
        def name_of(value):
            obj = value if isinstance(value, dict) else competitors_by_id.get(str(value), {})
            return _compact(obj.get("name") or obj.get("Name") or obj.get("shortName") or obj.get("ShortName"))
        home_name, away_name = name_of(home), name_of(away)
        title = _compact(game.get("name") or game.get("Name") or (f"{home_name} vs {away_name}" if home_name and away_name else ""))
        if not title:
            continue
        raw_date = game.get("startTime") or game.get("StartTime") or game.get("startDate") or game.get("StartDate") or game.get("date")
        event_date = _parse_api_datetime(str(raw_date) if raw_date else None)
        comp_obj = competitions.get(str(game.get("competitionId") or game.get("CompetitionId")), {})
        competition = _compact(comp_obj.get("name") or comp_obj.get("Name") or "365Scores")
        status_text = _compact(str(game.get("statusText") or game.get("StatusText") or game.get("gameStatusText") or game.get("GameStatusText") or ""))
        status = _event_status(status_text) if status_text else ("Scheduled" if event_date > _utc_now() else "Completed")
        participants = []
        for nm, role in ((home_name, "home"), (away_name, "away")):
            if nm:
                participants.append({"name": nm, "teamName": nm, "country": "", "role": role})
        results = []
        for nm, score in ((home_name, game.get("homeScore") or game.get("HomeScore")), (away_name, game.get("awayScore") or game.get("AwayScore"))):
            if nm and score not in (None, ""):
                results.append({"participantName": nm, "score": str(score), "resultText": str(score)})
        external_id = _compact(str(game.get("id") or game.get("Id") or ""))
        events.append({
            "sportKey": sport_key, "externalId": f"365scores-{sport_key}-{external_id}" if external_id else f"365scores-{sport_key}-{_slug(title)}-{event_date:%Y%m%d%H%M}",
            "title": title, "competitionName": competition, "eventDate": event_date.isoformat() + "Z",
            "status": status, "time": event_date.strftime("%H:%M UTC"), "venue": "", "country": "",
            "source": "365Scores", "sourceUrl": "https://www.365scores.com/ar",
            "participants": participants, "results": results, "streams": [],
        })
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break
    print(f"[other_sports] {sport_key} 365Scores API: parsed {len(events)} events")
    return events


# ---------------------------------------------------------------------------
# Formula 1 import (Jolpica/Ergast-compatible JSON, date-aware schedule + results)
# ---------------------------------------------------------------------------
def _fetch_json(url: str) -> dict[str, Any]:
    response = requests.get(url, headers=DEFAULT_HEADERS, timeout=TIMEOUT)
    response.raise_for_status()
    return response.json()


def _f1_races_from_payload(data: dict[str, Any]) -> list[dict[str, Any]]:
    return (((data.get("MRData") or {}).get("RaceTable") or {}).get("Races") or [])


def _scrape_formula1() -> list[dict[str, Any]]:
    target = _target_day().date()
    races_by_round: dict[str, dict[str, Any]] = {}
    try:
        for race in _f1_races_from_payload(_fetch_json(FORMULA1_SCHEDULE_API_URL)):
            if isinstance(race, dict):
                races_by_round[str(race.get("round") or "")] = race
    except Exception as exc:
        print(f"[other_sports] formula1 schedule API: fetch failed: {exc}", file=sys.stderr)

    results_by_round: dict[str, list[dict[str, Any]]] = {}
    try:
        for race in _f1_races_from_payload(_fetch_json(FORMULA1_RESULTS_API_URL)):
            if isinstance(race, dict):
                results_by_round[str(race.get("round") or "")] = race.get("Results") or []
                races_by_round.setdefault(str(race.get("round") or ""), race)
    except Exception as exc:
        print(f"[other_sports] formula1 results API: fetch failed: {exc}", file=sys.stderr)

    driver_standings = []
    constructor_standings = []
    try:
        lists = (((_fetch_json(FORMULA1_DRIVER_STANDINGS_API_URL).get("MRData") or {}).get("StandingsTable") or {}).get("StandingsLists") or [])
        driver_standings = (lists[0].get("DriverStandings") or []) if lists else []
    except Exception as exc:
        print(f"[other_sports] formula1 driver standings API: fetch failed: {exc}", file=sys.stderr)
    try:
        lists = (((_fetch_json(FORMULA1_CONSTRUCTOR_STANDINGS_API_URL).get("MRData") or {}).get("StandingsTable") or {}).get("StandingsLists") or [])
        constructor_standings = (lists[0].get("ConstructorStandings") or []) if lists else []
    except Exception as exc:
        print(f"[other_sports] formula1 constructor standings API: fetch failed: {exc}", file=sys.stderr)

    events: list[dict[str, Any]] = []
    for round_id, race in races_by_round.items():
        raw_date = f"{race.get('date')}T{race.get('time') or '12:00:00Z'}"
        event_date = _parse_api_datetime(raw_date)
        if event_date.date() != target and not results_by_round.get(round_id):
            continue
        circuit = race.get("Circuit") or {}
        location = circuit.get("Location") or {}
        title = _compact(race.get("raceName") or f"Formula 1 Round {round_id}")
        results = []
        participants = []
        for result in results_by_round.get(round_id, []):
            driver = result.get("Driver") or {}
            constructor = result.get("Constructor") or {}
            name = _compact(" ".join([driver.get("givenName") or "", driver.get("familyName") or ""]))
            participants.append({"name": name, "teamName": _compact(constructor.get("name")), "country": _compact(driver.get("nationality")), "role": "driver", "metadataJson": json.dumps({"driverId": driver.get("driverId"), "code": driver.get("code"), "constructorId": constructor.get("constructorId")}, ensure_ascii=False)})
            results.append({"participantName": name, "rank": int(result.get("position") or 0) or None, "score": str(result.get("points") or ""), "resultText": _compact(result.get("status") or ""), "metadataJson": json.dumps({"grid": result.get("grid"), "laps": result.get("laps"), "time": result.get("Time"), "fastestLap": result.get("FastestLap")}, ensure_ascii=False)})
        metadata = {"round": round_id, "season": race.get("season"), "driverStandings": driver_standings[:20], "constructorStandings": constructor_standings[:20]}
        events.append({"sportKey": "formula1", "externalId": f"jolpica-f1-{FORMULA1_SEASON}-{round_id}", "title": title, "competitionName": "Formula 1", "eventDate": event_date.isoformat() + "Z", "status": "Completed" if results else ("Live" if event_date.date() == _utc_now().date() else "Scheduled"), "time": event_date.strftime("%H:%M UTC"), "venue": _compact(circuit.get("circuitName")), "country": _compact(location.get("country")), "source": "Jolpica F1", "sourceUrl": race.get("url") or FORMULA1_SCHEDULE_API_URL, "participants": participants, "results": results, "streams": [], "liveUpdates": [], "metadataJson": json.dumps(metadata, ensure_ascii=False)})
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break
    print(f"[other_sports] formula1 Jolpica API: parsed {len(events)} events")
    return events

# ---------------------------------------------------------------------------
# Ice hockey import (ESPN JSON first, HTML fallback; no Selenium)
# ---------------------------------------------------------------------------
def _scrape_ice_hockey() -> list[dict[str, Any]]:
    api_events = _scrape_ice_hockey_api()
    if api_events:
        return api_events

    try:
        response = requests.get(ICE_HOCKEY_SOURCE_URL, headers=DEFAULT_HEADERS, timeout=TIMEOUT)
        response.raise_for_status()
    except Exception as exc:
        print(f"[other_sports] ice_hockey: fetch failed: {exc}", file=sys.stderr)
        return []

    soup = BeautifulSoup(response.text, "lxml")
    candidates = soup.select(
        "article, [class*='Scoreboard'], [class*='scoreboard'], [class*='game'], [class*='match']"
    )

    events: list[dict[str, Any]] = []
    seen: set[str] = set()
    for idx, node in enumerate(candidates):
        text = _compact(node.get_text(" "))
        if len(text) < 8 or len(text) > 450:
            continue

        team_nodes = node.select("[class*='team'], [class*='Team']")
        title = ""
        if len(team_nodes) >= 2:
            names = [_compact(t.get_text(" ")) for t in team_nodes[:2]]
            names = [n for n in names if n]
            if len(names) == 2:
                title = f"{names[0]} vs {names[1]}"
        if not title:
            title_node = node.select_one("h1,h2,h3,[class*='title'],[class*='name']")
            title = _compact(title_node.get_text(" ")) if title_node else ""
        if len(title) < 5 or len(title) > 140:
            continue

        link = node.select_one("a[href]")
        href = urljoin(ICE_HOCKEY_SOURCE_URL, link.get("href")) if link else ICE_HOCKEY_SOURCE_URL

        event_date = _utc_now()
        external_id = f"other-ice_hockey-{_slug(title)}-{event_date.strftime('%Y%m%d')}-{idx}"
        if external_id in seen:
            continue
        seen.add(external_id)

        events.append(
            {
                "sportKey": "ice_hockey",
                "externalId": external_id,
                "title": title,
                "competitionName": ICE_HOCKEY_SOURCE_NAME,
                "eventDate": event_date.isoformat() + "Z",
                "status": _event_status(text),
                "time": event_date.strftime("%H:%M UTC"),
                "venue": "",
                "country": "",
                "source": ICE_HOCKEY_SOURCE_NAME,
                "sourceUrl": href,
                "participants": _split_competitors(title),
                "results": [],
                "streams": [],
            }
        )
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break

    print(f"[other_sports] ice_hockey: parsed {len(events)} events")
    return events


def _scrape_ice_hockey_api() -> list[dict[str, Any]]:
    if not ICE_HOCKEY_API_URL:
        return []

    params: dict[str, str] = {}
    target_date = os.getenv("SCRAPER_DATE") or os.getenv("YALLAKORA_MATCH_DATE")
    if target_date:
        params["dates"] = target_date.replace("-", "")

    try:
        response = requests.get(ICE_HOCKEY_API_URL, headers=DEFAULT_HEADERS, params=params, timeout=TIMEOUT)
        response.raise_for_status()
        data = response.json()
    except Exception as exc:
        print(f"[other_sports] ice_hockey API: fetch failed: {exc}", file=sys.stderr)
        return []

    events: list[dict[str, Any]] = []
    for item in data.get("events") or []:
        competitions = item.get("competitions") if isinstance(item, dict) else None
        competition = competitions[0] if isinstance(competitions, list) and competitions else {}
        competitors = competition.get("competitors") if isinstance(competition, dict) else []
        participants: list[dict[str, str]] = []
        names: list[str] = []
        results: list[dict[str, Any]] = []
        for competitor in competitors if isinstance(competitors, list) else []:
            team = competitor.get("team") if isinstance(competitor, dict) else {}
            name = _compact(team.get("displayName") or team.get("name") or competitor.get("displayName"))
            if not name:
                continue
            role = _compact(competitor.get("homeAway"))
            names.append(name)
            participants.append({"name": name, "teamName": name, "country": "", "role": role})
            score = competitor.get("score")
            if score not in (None, ""):
                results.append({"participantName": name, "score": str(score), "type": "score"})

        title = _compact(item.get("name") or item.get("shortName") or (" vs ".join(names) if len(names) >= 2 else ""))
        if not title:
            continue

        event_date = _parse_api_datetime(item.get("date") or competition.get("date"))
        status_info = item.get("status") if isinstance(item.get("status"), dict) else {}
        status_type = status_info.get("type") if isinstance(status_info.get("type"), dict) else {}
        status = _event_status(_compact(status_type.get("description") or status_type.get("name") or status_info.get("displayClock")))
        if status_type.get("completed") is True:
            status = "Completed"

        external_id = _compact(item.get("id"))
        external_id = f"espn-nhl-{external_id}" if external_id else f"espn-nhl-{_slug(title)}-{event_date.strftime('%Y%m%d%H%M')}"
        link = ICE_HOCKEY_SOURCE_URL
        links = item.get("links") if isinstance(item.get("links"), list) else []
        if links and isinstance(links[0], dict):
            link = links[0].get("href") or link

        venue_info = competition.get("venue") if isinstance(competition.get("venue"), dict) else {}
        events.append({
            "sportKey": "ice_hockey",
            "externalId": external_id,
            "title": title,
            "competitionName": data.get("leagues", [{}])[0].get("name", ICE_HOCKEY_SOURCE_NAME) if isinstance(data.get("leagues"), list) else ICE_HOCKEY_SOURCE_NAME,
            "eventDate": event_date.isoformat() + "Z",
            "status": status,
            "time": event_date.strftime("%H:%M UTC"),
            "venue": _compact(venue_info.get("fullName")),
            "country": "",
            "source": ICE_HOCKEY_SOURCE_NAME,
            "sourceUrl": link,
            "participants": participants,
            "results": results,
            "streams": [],
        })
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break

    print(f"[other_sports] ice_hockey API: parsed {len(events)} events")
    return events


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main() -> int:
    all_events: list[dict[str, Any]] = []
    failures: dict[str, str] = {}

    if BASKETBALL_ENABLED:
        try:
            all_events.extend(_scrape_espn_scoreboard(BASKETBALL_API_URL, "basketball", BASKETBALL_SOURCE_NAME))
            all_events.extend(_scrape_365scores("basketball", SCORES365_BASKETBALL_SPORT_ID))
        except Exception as exc:
            failures["basketball"] = str(exc)
            print(f"[other_sports] WARNING: basketball failed: {exc}")
    else:
        print("[other_sports] basketball disabled via BASKETBALL_ENABLED=false")

    if TENNIS_ENABLED:
        try:
            all_events.extend(_scrape_365scores("tennis", SCORES365_TENNIS_SPORT_ID))
        except Exception as exc:
            failures["tennis"] = str(exc)
            print(f"[other_sports] WARNING: tennis failed: {exc}")
    else:
        print("[other_sports] tennis disabled via TENNIS_ENABLED=false")

    if FORMULA1_ENABLED:
        try:
            all_events.extend(_scrape_formula1())
        except Exception as exc:
            failures["formula1"] = str(exc)
            print(f"[other_sports] WARNING: formula1 failed: {exc}")
    else:
        print("[other_sports] formula1 disabled via FORMULA1_ENABLED=false")

    if ICE_HOCKEY_ENABLED:
        try:
            all_events.extend(_scrape_ice_hockey())
        except Exception as exc:
            failures["ice_hockey"] = str(exc)
            print(f"[other_sports] WARNING: ice_hockey failed: {exc}")
    else:
        print("[other_sports] ice_hockey disabled via ICE_HOCKEY_ENABLED=false")

    overrides = _load_stream_overrides()
    if overrides:
        all_events = [_attach_stream_overrides(event, overrides) for event in all_events]

    ok = _post_json("/api/other-sports/import/events", all_events)
    if not ok:
        failures["post_events"] = "failed to post events"

    summary = {"events": len(all_events), "failures": failures}
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())
