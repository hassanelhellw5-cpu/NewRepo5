"""Continuously refresh live match scores, details, and statistics.

PowerShell examples:
  # Run forever for today, refreshing every 60 seconds
  python Scrapers/live_match_updater.py

  # Run once for a specific day
  $env:LIVE_UPDATE_DATE="2026-07-10"; $env:LIVE_UPDATE_ONCE="1"; python Scrapers/live_match_updater.py

  # Tune refresh interval
  $env:LIVE_UPDATE_INTERVAL_SECONDS="30"; python Scrapers/live_match_updater.py
"""

from __future__ import annotations

import os
import time
from datetime import datetime

import requests

from yallakora_engine import YallaKoraEngine
API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net")
INTERVAL_SECONDS = int(os.getenv("LIVE_UPDATE_INTERVAL_SECONDS", "60"))
RUN_ONCE = os.getenv("LIVE_UPDATE_ONCE", "0").strip().lower() in {"1", "true", "yes"}


class LiveMatchUpdater:
    def __init__(self):
        self.engine = YallaKoraEngine(api_base_url=API_DOMAIN)
        self.session = requests.Session()

    def run_once(self, target_date: str | None = None):
        target_date = target_date or os.getenv("LIVE_UPDATE_DATE") or datetime.now().date().isoformat()
        print(f"[live-update] Refreshing matches/details for {target_date}...")

        tournaments = self.engine.get_matches(target_date=target_date)
        if not tournaments:
            print("[live-update] No matches returned from match center.")
            return

        # Import the fresh match-center payload first. This updates score, status, time, channel,
        # source URL, teams, and tournament links in one request.
        self.engine.send_to_api("matches", tournaments)

        refreshed_details = 0
        skipped_details = 0
        for tournament in tournaments:
            for match in tournament.get("matches", []):
                href = match.get("match_href")
                match_id = match.get("match_id")
                if not href or not match_id:
                    skipped_details += 1
                    continue

                # During live matches we always refresh details. For finished matches we also
                # refresh because stats/events can appear shortly after full time.
                if not self._should_refresh_details(match):
                    skipped_details += 1
                    continue

                details = self.engine.get_match_details(self.session, href, match_id)
                if not details.get("stats") and os.getenv("LIVE_UPDATE_ENABLE_SELENIUM_STATS", "1").strip().lower() in {"1", "true", "yes"}:
                    rendered_details = self._get_rendered_match_details(href, match_id)
                    if rendered_details.get("stats"):
                        details["stats"] = rendered_details["stats"]

                if details.get("events") or details.get("stats"):
                    self.engine.send_to_api("match-details", details)
                    refreshed_details += 1
                else:
                    skipped_details += 1

                time.sleep(self.engine.request_delay)

        print(
            f"[live-update] Done. Refreshed details for {refreshed_details} match(es), "
            f"skipped {skipped_details}."
        )

    def _get_rendered_match_details(self, href, match_id):
        if not YallaKoraEngine:
            return {"match_id": match_id, "events": [], "stats": []}
        driver = None
        try:
            driver = self.engine.create_selenium_driver(headless=True)
            return self.engine.get_match_details_selenium(driver, href, match_id)
        except Exception as exc:
            print(f"[live-update] Selenium stats fallback failed for {match_id}: {exc}")
            return {"match_id": match_id, "events": [], "stats": []}
        finally:
            if driver:
                try:
                    driver.quit()
                except Exception:
                    pass

    @staticmethod
    def _should_refresh_details(match):
        status = (match.get("status") or "").lower()
        has_score = bool(str(match.get("score_home") or "").strip()) and bool(str(match.get("score_away") or "").strip())
        return any(token in status for token in ["مباشر", "الشوط", "جارية", "live", "انته", "finished", "ft"]) or has_score


def main():
    updater = LiveMatchUpdater()
    while True:
        updater.run_once()
        if RUN_ONCE:
            break
        print(f"[live-update] Sleeping {INTERVAL_SECONDS} seconds...")
        time.sleep(INTERVAL_SECONDS)


if __name__ == "__main__":
    main()
