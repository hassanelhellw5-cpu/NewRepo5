"""Run the full sports scraping pipeline for one day, an explicit date range,
or an automatic "full sync" range driven by appsettings.json.

Examples (PowerShell):
  # Single specific day
  python Scrapers/run_all.py --date 2022-12-14

  # Explicit manual range
  python Scrapers/run_all.py --from-date 2022-12-01 --to-date 2022-12-31

  # Automatic full sync: today - SportsSync:FullSyncLookbackDays through
  # today + SportsSync:FullSyncLookaheadDays from appsettings.json (default 730/365 -> 1096 days)
  python Scrapers/run_all.py --full-sync

  # Env vars still work for hosting/PowerShell compatibility:
  $env:SCRAPER_DATE="2022-12-14"; python Scrapers/run_all.py

  # Normal frequent run (what the worker should call every IntervalSeconds) —
  # just today, no date args/env vars needed at all:
  python Scrapers/run_all.py

  # NEW: keep the script running forever, re-running the whole pipeline again
  # every N seconds after each run finishes (self-looping mode):
  python Scrapers/run_all.py --loop-seconds 5

  # Same thing via env var (handy for services/hosting where you can't pass CLI args):
  $env:LOOP_SECONDS="5"; python Scrapers/run_all.py

WHY THIS SPLIT EXISTS (read this before changing IntervalSeconds/profile):
  The ASP.NET worker (SportsSync:IntervalSeconds / LiveIntervalSeconds) is meant
  to call this script very frequently (every few minutes) to keep "today" (and
  near-live scores) fresh. That frequent call should stay scoped to just today
  -- it must NOT pull the full configured backfill day range every time, or you
  will hammer YallaKora / ATP / Sofascore / ESPN dozens of times per hour and
  get rate-limited or Cloudflare-blocked (exactly the kind of failures seen in
  earlier runs).

  The full configured YallaKora backfill (so a user can tap ANY date in the app and already
  find matches stored for it) is a separate, much less frequent job -- run it
  once a day (or whatever cadence you like) with --full-sync or RUN_ALL_DATE_RANGE_MODE=full_sync
  set. The C# worker should schedule TWO calls to this script:
    1. Every SportsSync:IntervalSeconds  -> normal call, no date-range env vars.
    2. Once per day (e.g. via a daily timer) -> RUN_ALL_DATE_RANGE_MODE=full_sync.

  NOTE on --loop-seconds: this is an alternative to the ASP.NET worker/Task
  Scheduler approach above, meant for cases where you just want this one
  process to keep re-running itself forever (e.g. a console left open, or a
  single scheduled task that starts it once). Very short intervals (single-
  digit seconds) hammer the same external sites on every iteration and risk
  rate-limiting/Cloudflare blocks -- prefer something closer to
  SportsSync:IntervalSeconds (minutes) unless you have a specific reason not to.

The script intentionally keeps each scraper as a separate process so a failure in
videos/streams does not hide match import output, and so the existing scripts can
keep their own environment-variable configuration. See OTHER_SPORTS_SETUP.md for
non-football tournament sources and HLS override setup.

NOTE: f1_motorsport_importer.py and selenium_multisport_importer.py have been
merged into ONE script, multisport_selenium_importer.py, which covers every
sport OTHER than football (tennis, basketball, motorsport/F1) via a single
Selenium session. Football stays fully covered by yallakora_engine.py.

IMPORTANT: multisport_selenium_importer.py's sources (ATP "current" scores,
WTA "scores", Sofascore livescore, formula1.com "results") are NOT
date-parameterized by the sites themselves -- they always show "now". Because
of that, this script is run ONCE per pipeline execution (not once per day in
a multi-day range), regardless of how many days are in the target date list.
Running it per-day would just re-scrape the same "current" data repeatedly,
waste time, and risk getting rate-limited/blocked for no benefit.
"""

from __future__ import annotations

import os
import argparse
import json
import shutil
import subprocess
import sys
import time
from datetime import datetime, timedelta
from pathlib import Path

ROOT = Path(__file__).resolve().parent
REPO_ROOT = ROOT.parent


def _load_appsettings() -> dict:
    settings: dict = {}
    for file_name in ("appsettings.json", "appsettings.Development.json"):
        path = REPO_ROOT / file_name
        if not path.exists():
            continue
        try:
            with path.open("r", encoding="utf-8-sig") as handle:
                data = json.load(handle)
            if isinstance(data, dict):
                settings = _deep_merge(settings, data)
        except Exception as exc:
            print(f"[pipeline] WARNING: could not read {file_name}: {exc}")
    return settings


def _deep_merge(base: dict, overlay: dict) -> dict:
    merged = dict(base)
    for key, value in overlay.items():
        if isinstance(value, dict) and isinstance(merged.get(key), dict):
            merged[key] = _deep_merge(merged[key], value)
        else:
            merged[key] = value
    return merged


def _setting(settings: dict, path: str, default=None):
    current = settings
    for part in path.split(":"):
        if not isinstance(current, dict) or part not in current:
            return default
        current = current[part]
    return current


def _set_env_default(env: dict[str, str], key: str, value) -> None:
    if key in env or value is None or value == "":
        return
    env[key] = str(value)


def _apply_appsettings_env(env: dict[str, str], settings: dict) -> None:
    """Mirror the ASP.NET worker's scraper environment mapping for manual
    run_all.py executions. This keeps local/hosting runs consistent when the
    connection string/API/domain/import key were configured in appsettings or
    via ASP.NET-style environment variables."""
    _set_env_default(env, "API_DOMAIN", _setting(settings, "ApiDomain"))
    _set_env_default(env, "IMPORT_API_KEY", _setting(settings, "ImportApiKey"))

    default_connection = _setting(settings, "ConnectionStrings:DefaultConnection")
    _set_env_default(env, "ConnectionStrings__DefaultConnection", default_connection)

    _set_env_default(env, "ICE_HOCKEY_ENABLED", _setting(settings, "OtherSports:IceHockeyEnabled"))
    _set_env_default(env, "FORMULA1_ENABLED", _setting(settings, "OtherSports:Formula1Enabled"))
    _set_env_default(env, "ICE_HOCKEY_SOURCE_URL", _setting(settings, "OtherSports:IceHockeySourceUrl"))
    _set_env_default(env, "ICE_HOCKEY_SOURCE_NAME", _setting(settings, "OtherSports:IceHockeySourceName"))
    overrides_file = _setting(settings, "OtherSports:StreamOverridesFile")
    if overrides_file and "OTHER_SPORTS_STREAM_OVERRIDES_FILE" not in env:
        overrides_path = Path(str(overrides_file))
        env["OTHER_SPORTS_STREAM_OVERRIDES_FILE"] = str(overrides_path if overrides_path.is_absolute() else ROOT / overrides_path)

    _set_env_default(env, "ENABLE_SELENIUM_MULTISPORT_IMPORT", _setting(settings, "SportsSync:EnableMultisportSeleniumImport"))
    _set_env_default(env, "ENABLE_SOFASCORE_FANTASY_LINEUPS", _setting(settings, "SportsSync:EnableSofascoreFantasyLineups"))
    _set_env_default(env, "ENABLE_UNIFIED_LIVE_LINKER", _setting(settings, "SportsSync:EnableUnifiedLiveLinker"))
    _set_env_default(env, "ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS", _setting(settings, "SportsSync:EnableYallaShootAppiumHighlights"))


def _has_chrome_binary(env: dict[str, str]) -> bool:
    if env.get("CHROME_BINARY") and Path(env["CHROME_BINARY"]).exists():
        return True
    return any(shutil.which(name) for name in ("google-chrome", "chrome", "chromium", "chromium-browser", "msedge"))


def _parse_date(value: str | None):
    if not value:
        return None
    value = value.strip()
    for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%m/%d/%Y"):
        try:
            return datetime.strptime(value, fmt).date()
        except ValueError:
            continue
    return datetime.fromisoformat(value).date()


def _parse_args(argv: list[str] | None = None):
    parser = argparse.ArgumentParser(description="Run the complete Sporty scraping pipeline.")
    parser.add_argument("--date", dest="scraper_date", help="Scrape one day (YYYY-MM-DD, DD/MM/YYYY, or M/D/YYYY).")
    parser.add_argument("--from-date", dest="date_from", help="Start date for an inclusive manual range.")
    parser.add_argument("--to-date", dest="date_to", help="End date for an inclusive manual range.")
    parser.add_argument("--full-sync", action="store_true", help="Scrape the configured full backfill range from appsettings.json.")
    parser.add_argument("--profile", choices=("full", "light"), help="full runs enrichments (videos/streams/fantasy); light runs core match imports only.")
    parser.add_argument(
        "--loop-seconds",
        dest="loop_seconds",
        type=float,
        default=None,
        help=(
            "If set, keep the process alive and automatically re-run the entire "
            "pipeline again this many seconds after each run finishes. Can also "
            "be set via the LOOP_SECONDS env var. Omit (default) to run once and exit, "
            "same as before."
        ),
    )
    return parser.parse_args(argv)


def _apply_cli_overrides(args) -> None:
    if args.scraper_date:
        os.environ["SCRAPER_DATE"] = args.scraper_date
    if args.date_from:
        os.environ["SCRAPER_DATE_FROM"] = args.date_from
    if args.date_to:
        os.environ["SCRAPER_DATE_TO"] = args.date_to
    if args.full_sync:
        os.environ["RUN_ALL_DATE_RANGE_MODE"] = "full_sync"
    if args.profile:
        os.environ["RUN_ALL_PROFILE"] = args.profile


def _target_dates(settings: dict):
    """Decide which day(s) to scrape, in priority order:

      1. --date / SCRAPER_DATE               -> a single explicit day.
      2. --from-date/--to-date or env FROM/TO -> an explicit manual range.
      3. --full-sync / RUN_ALL_DATE_RANGE_MODE=full_sync
                                     -> automatic range built from
                                        SportsSync:FullSyncLookbackDays /
                                        SportsSync:FullSyncLookaheadDays in
                                        appsettings.json (default 730/365, so
                                        today-730 .. today+365 = 1096 days total).
      4. (default)                  -> just today. This is what the frequent
                                        worker interval should hit, so it does
                                        NOT accidentally re-scrape the whole backfill range on
                                        every single call.
    """
    explicit = _parse_date(os.getenv("SCRAPER_DATE"))
    if explicit:
        return [explicit]

    date_from = _parse_date(os.getenv("SCRAPER_DATE_FROM"))
    date_to = _parse_date(os.getenv("SCRAPER_DATE_TO"))
    if date_from and date_to:
        if date_from > date_to:
            date_from, date_to = date_to, date_from
        return [date_from + timedelta(days=i) for i in range((date_to - date_from).days + 1)]

    range_mode = os.getenv("RUN_ALL_DATE_RANGE_MODE", "").strip().lower()
    if range_mode == "full_sync":
        lookback_days = int(os.getenv(
            "FULL_SYNC_LOOKBACK_DAYS",
            str(_setting(settings, "SportsSync:FullSyncLookbackDays", 30)),
        ))
        lookahead_days = int(os.getenv(
            "FULL_SYNC_LOOKAHEAD_DAYS",
            str(_setting(settings, "SportsSync:FullSyncLookaheadDays", 30)),
        ))
        today = datetime.now().date()
        date_list = [today + timedelta(days=offset) for offset in range(-lookback_days, lookahead_days + 1)]
        print(f"[pipeline] full_sync mode: scraping {len(date_list)} day(s) "
              f"({date_list[0].isoformat()} .. {date_list[-1].isoformat()}), "
              f"lookback={lookback_days} lookahead={lookahead_days}")
        return date_list

    return [_parse_date(os.getenv("YALLAKORA_MATCH_DATE")) or datetime.now().date()]


def _is_enabled(env: dict[str, str], name: str, default: bool) -> bool:
    value = env.get(name)
    if value is None:
        return default
    return value.strip().lower() in {"1", "true", "yes", "on"}




def _should_run_live_match_updater(env: dict[str, str], day) -> bool:
    if not _is_enabled(env, "ENABLE_LIVE_MATCH_UPDATER", True):
        return False
    today = datetime.now().date()
    days_back = int(env.get("LIVE_UPDATE_RUN_DAYS_BACK", "2"))
    days_ahead = int(env.get("LIVE_UPDATE_RUN_DAYS_AHEAD", "0"))
    return today - timedelta(days=days_back) <= day <= today + timedelta(days=days_ahead)

def _run(script: str, env: dict[str, str], args: list[str] | None = None, *, optional: bool = False):
    args = args or []
    print(f"\n[pipeline] Running {script}{(' ' + ' '.join(args)) if args else ''}...")
    script_path = ROOT / script
    if not script_path.exists():
        print(f"[pipeline] WARNING: {script} not found; {'skipping optional job' if optional else 'cannot continue this job'}.")
        return 0 if optional else 1
    result = subprocess.run([sys.executable, "-u", str(script_path), *args], env=env, check=False)
    if result.returncode != 0:
        print(f"[pipeline] WARNING: {script} exited with code {result.returncode}; continuing.")
    return 0 if optional else result.returncode


def run_once(argv: list[str] | None, args) -> int:
    """Run the pipeline exactly one time (this is everything the old main() used to do)."""
    settings = _load_appsettings()
    overall_code = 0

    target_dates = _target_dates(settings)
    multiday_run = len(target_dates) > 1

    # multisport_selenium_importer.py's sources (ATP/WTA/Sofascore/formula1.com)
    # are NOT date-parameterized -- they only ever show "current" data. Track
    # whether we've already run it once this whole pipeline execution so a
    # multi-day (e.g. full_sync) range doesn't re-run it hundreds of times for nothing.
    selenium_multisport_done = False

    for day in target_dates:
        iso_day = day.isoformat()
        print(f"\n[pipeline] ===== {iso_day} =====")
        env = os.environ.copy()
        _apply_appsettings_env(env, settings)
        env["YALLAKORA_MATCH_DATE"] = iso_day
        env["VIDEO_SCRAPER_DATE"] = iso_day
        env["SCRAPER_DATE"] = iso_day  # keep other_sports_importer._target_day() in sync per-iteration

        profile = env.get("RUN_ALL_PROFILE", "full").strip().lower()
        full_profile = profile == "full"
        print(f"[pipeline] profile={profile} (default full includes fantasy/video/live-linking jobs; set RUN_ALL_PROFILE=light or --profile light to skip them)")

        # 1) Football core import: matches, details, squads, and tournament details.
        #    Pass --date explicitly so yallakora_engine.py imports exactly this
        #    iteration day. The scraper's no-date CLI mode intentionally discovers
        #    YallaKora's visible day strip for manual runs, which we do not want
        #    repeated inside every run_all.py date-loop iteration.
        overall_code = max(overall_code, _run("yallakora_engine.py", env, ["matches", "--date", iso_day]))

        if _should_run_live_match_updater(env, day):
            live_env = env.copy()
            live_env["LIVE_UPDATE_DATE"] = iso_day
            live_env["LIVE_UPDATE_ONCE"] = "1"
            live_env.setdefault("LIVE_UPDATE_ENABLE_SELENIUM_STATS", "1" if full_profile else "0")
            overall_code = max(overall_code, _run("live_match_updater.py", live_env, optional=True))
        elif _is_enabled(env, "ENABLE_LIVE_MATCH_UPDATER", True):
            print(f"[pipeline] skipped live_match_updater.py for {iso_day}; outside live refresh window.")

        # 2) Other sports core imports (ice hockey, basketball/tennis via ESPN +
        #    365Scores). ALSO date-aware (reads SCRAPER_DATE/YALLAKORA_MATCH_DATE
        #    and passes it to the underlying APIs), so this runs once per day too.
        overall_code = max(overall_code, _run("other_sports_importer.py", env))

        # 3) Selenium multisport importer (ATP/WTA/Flashscore/Sofascore/formula1.com).
        #    NOT date-aware -- its sources only ever return "current" data regardless
        #    of what date we ask for. Run it ONCE per pipeline execution, not once
        #    per day, to avoid pointless duplicate scraping and reduce the chance of
        #    getting rate-limited/blocked across a long multi-day range.
        if _is_enabled(env, "ENABLE_SELENIUM_MULTISPORT_IMPORT", True) and not selenium_multisport_done:
            if _has_chrome_binary(env):
                overall_code = max(overall_code, _run("multisport_selenium_importer.py", env, optional=True))
            else:
                print("[pipeline] WARNING: skipped multisport_selenium_importer.py because no Chrome/Chromium binary was found. "
                      "Install Chrome/Chromium or set CHROME_BINARY to enable Selenium sports sources.")
            selenium_multisport_done = True
        elif multiday_run and _is_enabled(env, "ENABLE_SELENIUM_MULTISPORT_IMPORT", True):
            print(f"[pipeline] multisport_selenium_importer.py already ran once this execution; "
                  f"skipping for {iso_day} (its sources aren't date-scoped).")

        if not full_profile:
            print("[pipeline] light profile: skipped fantasy backfills, video scraping, and live stream linking.")
            continue

        # 4) Optional heavy enrichments. Run these only in full mode or by invoking
        #    their scripts directly. This keeps run_all.py quick for normal syncs.
        #    These ARE date-aware (each reads its own *_DATE env var per day), so
        #    they stay inside the daily loop.
        env["FANTASY_ROSTER_DATE"] = iso_day
        if _is_enabled(env, "ENABLE_FANTASY_ROSTER_BACKFILL", True):
            overall_code = max(overall_code, _run("fantasy_roster_backfill.py", env, optional=True))

        env["SOFASCORE_FANTASY_DATE"] = iso_day
        if _is_enabled(env, "ENABLE_SOFASCORE_FANTASY_LINEUPS", True):
            overall_code = max(overall_code, _run("sofascore_fantasy_lineup_importer.py", env, optional=True))

        if _is_enabled(env, "ENABLE_VIDEO_SCRAPER", True):
            if env.get("YALLASHOOT_APP_API_URL"):
                print("[pipeline] YallaShoot app API highlights enabled through yallakora_video_scraper.py")
            else:
                print("[pipeline] YallaShoot app API URL not set; yallakora_video_scraper.py will use YallaKora/general highlight fallbacks")
            overall_code = max(overall_code, _run("yallakora_video_scraper.py", env, optional=True))

        if _is_enabled(env, "ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS", False):
            overall_code = max(overall_code, _run("yallashoot_appium_highlights.py", env, optional=True))

        if _is_enabled(env, "ENABLE_LIVE_STREAM_LINKING", True):
            live_script = "unified_live_stream_linker.py" if _is_enabled(env, "ENABLE_UNIFIED_LIVE_LINKER", True) else "live_stream_scraper.py"
            overall_code = max(overall_code, _run(live_script, env, optional=True))

    if target_dates and _is_enabled(os.environ, "ENABLE_NEWS_SCRAPER", True):
        news_env = os.environ.copy()
        _apply_appsettings_env(news_env, settings)
        overall_code = max(overall_code, _run("yallakora_engine.py", news_env, ["news"], optional=True))

    return overall_code


def main(argv: list[str] | None = None):
    args = _parse_args(argv)
    _apply_cli_overrides(args)

    # Loop interval can come from --loop-seconds or the LOOP_SECONDS env var
    # (env var is handy when this is launched by a service/Task Scheduler entry
    # that only lets you set environment variables, not CLI flags).
    loop_seconds = args.loop_seconds
    if loop_seconds is None:
        env_loop = os.getenv("LOOP_SECONDS")
        if env_loop:
            try:
                loop_seconds = float(env_loop)
            except ValueError:
                print(f"[pipeline] WARNING: LOOP_SECONDS={env_loop!r} is not a valid number; ignoring.")

    if loop_seconds is None:
        # Original behavior: run exactly once and exit.
        return run_once(argv, args)

    if loop_seconds < 5:
        print(f"[pipeline] WARNING: loop interval of {loop_seconds}s is very aggressive and may get you "
              f"rate-limited/blocked by YallaKora/ESPN/Sofascore/ATP. Consider a larger interval.")

    run_number = 0
    try:
        while True:
            run_number += 1
            started_at = datetime.now()
            print(f"\n[pipeline] ########## Loop run #{run_number} starting at {started_at.isoformat(timespec='seconds')} ##########")
            exit_code = run_once(argv, args)
            finished_at = datetime.now()
            elapsed = (finished_at - started_at).total_seconds()
            print(f"[pipeline] ########## Loop run #{run_number} finished (exit_code={exit_code}, took {elapsed:.1f}s). "
                  f"Sleeping {loop_seconds}s before next run. Press Ctrl+C to stop. ##########")
            time.sleep(loop_seconds)
    except KeyboardInterrupt:
        print("\n[pipeline] Loop stopped by user (Ctrl+C).")
        return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))