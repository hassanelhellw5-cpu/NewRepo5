"""Extract Yalla Shoot mobile-app highlight/video links with Appium + UiAutomator2.

This scraper is optional and safe for run_all.py: it exits 0 when Appium or an
Android device is unavailable, so normal server syncs do not fail. When enabled
it opens the native Yalla Shoot Android app, scans visible text/content-desc
nodes for highlight/video URLs/titles, and posts matched videos to the existing
football match video endpoint when a YALLASHOOT_APP_PACKAGE is configured.

Required env for real device runs:
  ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS=true
  APPIUM_SERVER_URL=http://127.0.0.1:4723
  YALLASHOOT_APP_PACKAGE=<native app package>
Optional:
  YALLASHOOT_APP_ACTIVITY=<activity>
  APPIUM_DEVICE_NAME=Android
  API_DOMAIN=http://qemma.runasp.net
"""
from __future__ import annotations

import json
import os
import re
import sys
from datetime import datetime, timezone
from typing import Any

import requests

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
IMPORT_API_KEY = os.getenv("IMPORT_API_KEY", "").strip()
ENABLED = os.getenv("ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS", "false").strip().lower() in {"1", "true", "yes", "on"}
URL_PATTERN = re.compile(r"https?://[^\s\"']+", re.IGNORECASE)


def _post(path: str, payload: list[dict[str, Any]]) -> bool:
    if not payload:
        print("[appium-highlights] no highlight videos found to post")
        return True
    headers = {"Content-Type": "application/json"}
    if IMPORT_API_KEY:
        headers["X-Import-Key"] = IMPORT_API_KEY
    try:
        r = requests.post(f"{API_DOMAIN}{path}", json=payload, headers=headers, timeout=20)
        print(f"[appium-highlights] POST {path}: {r.status_code}")
        return r.status_code < 400
    except Exception as exc:
        print(f"[appium-highlights] post failed: {exc}", file=sys.stderr)
        return False


def _collect_highlights() -> list[dict[str, Any]]:
    try:
        from appium import webdriver
        from appium.options.android import UiAutomator2Options
        from selenium.webdriver.common.by import By
    except Exception as exc:
        print(f"[appium-highlights] Appium dependencies unavailable; install Appium-Python-Client. {exc}")
        return []

    package = os.getenv("YALLASHOOT_APP_PACKAGE", "").strip()
    if not package:
        print("[appium-highlights] YALLASHOOT_APP_PACKAGE is not set; skipping native-app scan")
        return []

    options = UiAutomator2Options()
    options.platform_name = "Android"
    options.automation_name = "UiAutomator2"
    options.device_name = os.getenv("APPIUM_DEVICE_NAME", "Android")
    options.app_package = package
    activity = os.getenv("YALLASHOOT_APP_ACTIVITY", "").strip()
    if activity:
        options.app_activity = activity
    options.no_reset = True

    driver = webdriver.Remote(os.getenv("APPIUM_SERVER_URL", "http://127.0.0.1:4723"), options=options)
    try:
        elements = driver.find_elements(By.XPATH, "//*[contains(@text,'ملخص') or contains(@text,'هدف') or contains(@text,'فيديو') or contains(@content-desc,'ملخص') or contains(@content-desc,'فيديو')]")
        videos: list[dict[str, Any]] = []
        seen: set[str] = set()
        for idx, element in enumerate(elements[:80]):
            text = (element.text or element.get_attribute("contentDescription") or "").strip()
            attrs = " ".join(filter(None, [text, element.get_attribute("resourceId"), element.get_attribute("contentDescription")]))
            url_match = URL_PATTERN.search(attrs)
            external_id = f"yallashoot-appium-{abs(hash(attrs))}-{idx}"
            if external_id in seen:
                continue
            seen.add(external_id)
            videos.append({
                "externalId": external_id,
                "title": text[:160] or "Yalla Shoot highlight",
                "description": text,
                "videoUrl": url_match.group(0) if url_match else "",
                "thumbnailUrl": "",
                "Type": "Highlight",
                "PublishedAt": datetime.now(timezone.utc).isoformat(),
                "Source": "YallaShoot Android App/Appium UiAutomator2",
            })
        print(f"[appium-highlights] collected {len(videos)} candidate highlight nodes")
        return videos
    finally:
        driver.quit()


def main() -> int:
    if not ENABLED:
        print("[appium-highlights] disabled; set ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS=true to run")
        return 0
    # Match-specific posting needs MATCH_ID because native cards do not expose
    # backend ids. Operators can run one match at a time when validating videos.
    match_id = os.getenv("YALLASHOOT_MATCH_ID", "").strip()
    videos = _collect_highlights()
    if match_id:
        return 0 if _post(f"/api/SportsData/matches/{match_id}/videos", videos) else 1
    print(json.dumps({"videos": videos}, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
