"""Backfill mini-fantasy player rosters from recent YallaKora lineups.

For the teams playing on the target day, this script looks back over recent
YallaKora match-center dates, finds previous matches involving those teams,
imports those matches, then imports their announced squads. The API then upserts
those squad players into the Players table.

Environment variables:
  API_DOMAIN: API base URL, defaults to http://qemma.runasp.net
  FANTASY_ROSTER_DATE: target fantasy day, defaults to today (YYYY-MM-DD)
  FANTASY_ROSTER_BACKFILL_DAYS: days to look back, defaults to 60
  FANTASY_ROSTER_MATCHES_PER_TEAM: previous matches to import per team, defaults to 6
"""

from __future__ import annotations

import os
import time
from collections import defaultdict
from datetime import datetime, timedelta

from selenium.common.exceptions import WebDriverException

from yallakora_engine import YallaKoraEngine

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net")
BACKFILL_DAYS = int(os.getenv("FANTASY_ROSTER_BACKFILL_DAYS", "60"))
MATCHES_PER_TEAM = int(os.getenv("FANTASY_ROSTER_MATCHES_PER_TEAM", "6"))


def _parse_date(value: str | None):
    if not value:
        return datetime.now().date()
    value = value.strip()
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%m/%d/%Y"):
        try:
            return datetime.strptime(value, fmt).date()
        except ValueError:
            continue
    return datetime.fromisoformat(value).date()


def _team_key(value: str | None) -> str:
    return (value or "").strip().casefold()


def _match_has_target_team(match: dict, target_team_keys: set[str]) -> bool:
    return _team_key(match.get("home_team")) in target_team_keys or _team_key(match.get("away_team")) in target_team_keys


def _filter_tournaments_for_target_teams(tournaments: list[dict], target_team_keys: set[str]) -> list[dict]:
    filtered = []
    for tournament in tournaments:
        matches = [m for m in tournament.get("matches", []) if _match_has_target_team(m, target_team_keys)]
        if matches:
            copy = dict(tournament)
            copy["matches"] = matches
            filtered.append(copy)
    return filtered


def main():
    target_day = _parse_date(os.getenv("FANTASY_ROSTER_DATE") or os.getenv("SCRAPER_DATE") or os.getenv("YALLAKORA_MATCH_DATE"))
    engine = YallaKoraEngine(api_base_url=API_DOMAIN)

    today_tournaments = engine.get_matches(target_date=target_day.isoformat())
    if not today_tournaments:
        print(f"[fantasy-roster] No matches found for {target_day}.")
        return 0

    target_team_keys = {
        _team_key(team)
        for tournament in today_tournaments
        for match in tournament.get("matches", [])
        for team in (match.get("home_team"), match.get("away_team"))
        if _team_key(team)
    }
    print(f"[fantasy-roster] Target teams for {target_day}: {len(target_team_keys)}")
    if not target_team_keys:
        return 0

    imported_counts: dict[str, int] = defaultdict(int)
    driver = None
    try:
        try:
            driver = YallaKoraEngine.create_selenium_driver(headless=True)
        except WebDriverException as exc:
            print(f"[fantasy-roster] Selenium unavailable; cannot import historical squads: {exc}")
            return 0

        for offset in range(1, BACKFILL_DAYS + 1):
            if all(imported_counts[team] >= MATCHES_PER_TEAM for team in target_team_keys):
                break

            day = target_day - timedelta(days=offset)
            tournaments = engine.get_matches(target_date=day.isoformat())
            target_tournaments = _filter_tournaments_for_target_teams(tournaments, target_team_keys)
            if not target_tournaments:
                continue

            # Import the previous match rows first; squad import needs match_id to exist.
            engine.send_to_api("matches", target_tournaments)

            for tournament in target_tournaments:
                for match in tournament.get("matches", []):
                    teams = [_team_key(match.get("home_team")), _team_key(match.get("away_team"))]
                    if not any(imported_counts[team] < MATCHES_PER_TEAM for team in teams if team in target_team_keys):
                        continue

                    href = match.get("match_href")
                    match_id = match.get("match_id")
                    if not href or not match_id:
                        continue

                    print(f"[fantasy-roster] Importing squad from {day}: {match.get('home_team')} vs {match.get('away_team')}")
                    squad = engine.get_match_squad_selenium(driver, href, match_id)
                    has_players = any(squad.get(key) for key in ("home_main", "home_sub", "away_main", "away_sub"))
                    if not has_players:
                        continue

                    engine.send_to_api("squad", squad)
                    for team in teams:
                        if team in target_team_keys:
                            imported_counts[team] += 1
                    time.sleep(engine.request_delay)
    finally:
        if driver is not None:
            driver.quit()

    print(f"[fantasy-roster] Done. Imported recent squad matches for {len(imported_counts)} team(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
