"""Unified Selenium-based multi-sport importer for Qemma.

This merges what used to be TWO separate scripts into ONE, running entirely
through a single headless Chrome/Selenium session:

  - f1_motorsport_importer.py      (Motorsport.com F1 schedule, JSON-LD aware)
  - selenium_multisport_importer.py (tennis/F1/Arabic news via Selenium)

Sources covered now:
  - Tennis:     ATP Tour, WTA, Flashscore, Sofascore (tennis)
  - Basketball: Sofascore (basketball livescore)
  - Formula 1:  formula1.com results page (generic parser) + Motorsport.com
                schedule (rich JSON-LD aware parser, same quality as the old
                dedicated f1_motorsport_importer.py) + Motorsport.com news

  FOOTBALL IS INTENTIONALLY NOT COVERED HERE. yallakora_engine.py already
  handles every football match, squad, event, video, and news item — this
  importer exists purely to cover every OTHER sport (tennis, basketball,
  motorsport/F1, and anything added later) without duplicating that work.

Posts to:
  - /api/other-sports/import/events   (tennis / basketball / F1 events)
  - /api/SportsData/import/news       (Motorsport.com F1 news)

Does not bypass Cloudflare, login walls, paywalls, or DRM/HLS protection.
Selenium is used only to render public JavaScript pages. If a source blocks
automation, that source is skipped and reported in the console logs/summary.

RESILIENCE NOTE: each source used to share one long-lived Chrome session with
no page-load timeout. If one page (e.g. a source behind bot-detection) hung,
the underlying ChromeDriver connection itself would eventually time out and
leave the WHOLE session unusable — every source after it in the loop would
then fail too, even though nothing was actually wrong with them. Now each
source gets a bounded page-load timeout, and if a source's attempt raises,
we recycle (quit + rebuild) the driver before moving on, so a single bad
source can't take down the rest of the run.
"""

from __future__ import annotations

import json
import os
import re
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Any
from urllib.parse import urljoin, urlparse

import requests
from bs4 import BeautifulSoup

try:
    from selenium import webdriver
    from selenium.webdriver.chrome.options import Options as ChromeOptions
    from selenium.webdriver.chrome.service import Service as ChromeService
    from selenium.webdriver.common.by import By
    from selenium.webdriver.support import expected_conditions as EC
    from selenium.webdriver.support.ui import WebDriverWait
    from webdriver_manager.chrome import ChromeDriverManager
except Exception as exc:  # pragma: no cover - handled at runtime for server logs
    webdriver = None
    SELENIUM_IMPORT_ERROR = exc
else:
    SELENIUM_IMPORT_ERROR = None

# Reuse the same stream-override / posting helpers other_sports_importer.py
# already has, instead of duplicating that logic a third time.
from other_sports_importer import _attach_stream_overrides, _load_stream_overrides, _post_json

# ---------------------------------------------------------------------------
# General config
# ---------------------------------------------------------------------------
API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net").rstrip("/")
TIMEOUT = int(os.getenv("SELENIUM_MULTISPORT_TIMEOUT_SECONDS", "25"))
PAGE_WAIT_SECONDS = int(os.getenv("SELENIUM_MULTISPORT_PAGE_WAIT_SECONDS", "8"))
MAX_ITEMS_PER_SOURCE = int(os.getenv("SELENIUM_MULTISPORT_MAX_ITEMS_PER_SOURCE", "20"))
HEADLESS = os.getenv("SELENIUM_MULTISPORT_HEADLESS", "true").strip().lower() != "false"
# Hard ceiling on how long a single driver.get() is allowed to block for.
# Without this, a hung/challenge page (e.g. bot-detection) can block until the
# underlying ChromeDriver connection itself times out, which corrupts the
# whole session instead of just failing one source.
PAGE_LOAD_TIMEOUT_SECONDS = int(os.getenv("SELENIUM_MULTISPORT_PAGE_LOAD_TIMEOUT_SECONDS", "30"))

ENABLED_SOURCES = {
    item.strip().lower()
    for item in os.getenv(
        "SELENIUM_MULTISPORT_SOURCES",
        # NOTE: football is intentionally NOT covered here — yallakora_engine.py
        # already handles all football matches/news/squads/details. This
        # importer is for every OTHER sport only.
        "atp,wta,flashscore_tennis,sofascore_tennis,sofascore_basketball,"
        "formula1,formula1_home,motorsport_schedule,motorsport_news",
    ).split(",")
    if item.strip()
}

DEFAULT_HEADERS = {
    "User-Agent": os.getenv(
        "SELENIUM_MULTISPORT_USER_AGENT",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36",
    ),
    "Accept-Language": "ar,en;q=0.8",
}

# ---------------------------------------------------------------------------
# F1 / Motorsport.com specific config (kept from the old f1_motorsport_importer.py)
# ---------------------------------------------------------------------------
F1_BASE_URL = os.getenv("F1_MOTORSPORT_BASE_URL", "https://me.motorsport.com").rstrip("/")
F1_YEAR = int(os.getenv("F1_MOTORSPORT_YEAR", str(datetime.now(timezone.utc).year)))
F1_LIVE_WINDOW_MINUTES_BEFORE = int(os.getenv("F1_LIVE_WINDOW_MINUTES_BEFORE", "90"))
F1_LIVE_WINDOW_MINUTES_AFTER = int(os.getenv("F1_LIVE_WINDOW_MINUTES_AFTER", "180"))
OFFICIAL_WATCH_URL = os.getenv("F1_OFFICIAL_WATCH_URL", "").strip()
OFFICIAL_WATCH_SOURCE = os.getenv("F1_OFFICIAL_WATCH_SOURCE", "Official F1 watch link").strip()


@dataclass(frozen=True)
class PageSource:
    key: str
    url: str
    sport_key: str
    source_name: str
    mode: str  # "event" | "news" | "f1_schedule"


SOURCES = [
    PageSource("atp", os.getenv("ATP_TOUR_URL", "https://www.atptour.com/en/scores/current"), "tennis", "ATP Tour", "event"),
    PageSource("wta", os.getenv("WTA_URL", "https://www.wtatennis.com/scores"), "tennis", "WTA", "event"),
    PageSource("flashscore_tennis", os.getenv("FLASHSCORE_TENNIS_URL", "https://www.flashscore.com/tennis/"), "tennis", "Flashscore", "event"),
    PageSource("sofascore_tennis", os.getenv("SOFASCORE_TENNIS_URL", "https://www.sofascore.com/tennis"), "tennis", "Sofascore", "event"),
    PageSource("sofascore_basketball", os.getenv("SOFASCORE_BASKETBALL_URL", "https://www.sofascore.com/basketball/livescore"), "basketball", "Sofascore", "event"),
    PageSource("formula1", os.getenv("FORMULA1_URL", "https://www.formula1.com/en/results.html"), "formula1", "Formula1.com", "event"),
    PageSource("formula1_home", os.getenv("FORMULA1_HOME_URL", "https://www.formula1.com/"), "formula1", "Formula1.com", "news"),
    # Rich F1 schedule parser (JSON-LD aware), merged in from f1_motorsport_importer.py
    PageSource("motorsport_schedule", os.getenv("F1_MOTORSPORT_SCHEDULE_URL", f"{F1_BASE_URL}/f1/schedule/{F1_YEAR}/"), "formula1", "Motorsport.com", "f1_schedule"),
    PageSource("motorsport_news", os.getenv("F1_MOTORSPORT_NEWS_URL", f"{F1_BASE_URL}/f1/news/"), "formula1", "Motorsport.com", "news"),
]


# ---------------------------------------------------------------------------
# Small shared helpers
# ---------------------------------------------------------------------------
def _compact(value: str | None) -> str:
    return re.sub(r"\s+", " ", value or "").strip()


def _slug(value: str) -> str:
    normalized = re.sub(r"[^\w\u0600-\u06ff]+", "-", value.lower(), flags=re.UNICODE).strip("-")
    return normalized[:90] or "item"


def _utc_now() -> datetime:
    return datetime.now(timezone.utc).replace(tzinfo=None, microsecond=0)


def _parse_date(value: str, fallback_year: int = F1_YEAR) -> datetime | None:
    """Parse a date string into a naive UTC datetime. Handles ISO 8601 (with or
    without a trailing 'Z'/offset) and compact Motorsport-style labels like
    'Jul 17' or '17 Jul'."""
    value = _compact(value)
    if not value:
        return None
    normalized = value.replace("Z", "+00:00")
    for candidate in (normalized, normalized.split(" - ")[0], normalized.split("–")[0].strip()):
        try:
            parsed = datetime.fromisoformat(candidate)
            return parsed.astimezone(timezone.utc).replace(tzinfo=None) if parsed.tzinfo else parsed
        except ValueError:
            pass
    for fmt in ("%b %d %Y", "%d %b %Y", "%B %d %Y", "%d %B %Y"):
        try:
            return datetime.strptime(f"{value} {fallback_year}", fmt)
        except ValueError:
            continue
    return None


# ---------------------------------------------------------------------------
# Selenium driver plumbing
# ---------------------------------------------------------------------------
def _build_driver():
    if webdriver is None:
        raise RuntimeError(f"Selenium is not available: {SELENIUM_IMPORT_ERROR}")
    options = ChromeOptions()
    if HEADLESS:
        options.add_argument("--headless=new")
    options.add_argument("--no-sandbox")
    options.add_argument("--disable-dev-shm-usage")
    options.add_argument("--disable-gpu")
    options.add_argument("--window-size=1440,1400")
    options.add_argument(f"--user-agent={DEFAULT_HEADERS['User-Agent']}")
    binary = os.getenv("CHROME_BINARY")
    if binary:
        options.binary_location = binary
    driver_path = os.getenv("CHROMEDRIVER_PATH")
    service = ChromeService(driver_path) if driver_path else ChromeService(ChromeDriverManager().install())
    driver = webdriver.Chrome(service=service, options=options)
    # Bound how long a single navigation can hang for. Without this, a page
    # stuck on a bot-detection challenge can block far longer than expected
    # and eventually poison the whole ChromeDriver session (see module note
    # above), which used to cascade into every later source failing too.
    driver.set_page_load_timeout(PAGE_LOAD_TIMEOUT_SECONDS)
    return driver


def _quit_driver_safely(driver) -> None:
    try:
        driver.quit()
    except Exception:
        pass


def _render(driver, url: str) -> str:
    driver.get(url)
    try:
        WebDriverWait(driver, PAGE_WAIT_SECONDS).until(EC.presence_of_element_located((By.TAG_NAME, "body")))
    except Exception:
        pass
    time.sleep(float(os.getenv("SELENIUM_MULTISPORT_EXTRA_SLEEP_SECONDS", "2")))
    return driver.page_source


# ---------------------------------------------------------------------------
# Generic event/news extraction (ATP, WTA, Flashscore, Sofascore, formula1.com,
# and any future simple source) — unchanged behavior from the old
# selenium_multisport_importer.py.
# ---------------------------------------------------------------------------
def _parse_datetime_from_node(node, fallback_days: int = 0) -> datetime:
    raw = ""
    if node:
        time_node = node.select_one("time[datetime]")
        if time_node:
            raw = time_node.get("datetime", "")
        raw = raw or node.get("data-date", "") or node.get("data-start-time", "")
    if raw:
        parsed = _parse_date(raw)
        if parsed:
            return parsed
    return _utc_now() + timedelta(days=fallback_days)


def _event_status(text: str, event_date: datetime) -> str:
    lower = text.lower()
    if any(word in lower for word in ["live", "مباشر", "جارية", "set", "q1", "q2", "lap"]):
        return "Live"
    now = _utc_now()
    return "Scheduled" if event_date > now else "Completed"


def _split_competitors(title: str) -> list[dict[str, str]]:
    separators = [" vs ", " v ", " - ", "–", " ضد "]
    for sep in separators:
        if sep in title:
            parts = [_compact(p) for p in title.split(sep, 1)]
            if len(parts) == 2 and all(parts):
                return [
                    {"name": parts[0], "teamName": parts[0], "country": "", "role": "home"},
                    {"name": parts[1], "teamName": parts[1], "country": "", "role": "away"},
                ]
    return []


def _sport_keywords(sport_key: str) -> list[str]:
    return {
        "tennis": ["vs", " v ", "set", "atp", "wta", "tennis", "court", "round", "final"],
        "formula1": ["formula", "grand prix", "practice", "qualifying", "race", "sprint", "f1"],
        "football": ["vs", " v ", "ft", "half", "goal", "match"],
        "basketball": ["vs", " v ", "quarter", "ot", "nba", "basketball"],
    }.get(sport_key, [])


def _extract_events_from_html(html: str, page: PageSource) -> list[dict[str, Any]]:
    soup = BeautifulSoup(html, "lxml")
    candidates = soup.select(
        "article, tr, [class*='match'], [class*='event'], [class*='fixture'], [class*='score'], [data-testid*='event']"
    )
    events: list[dict[str, Any]] = []
    seen: set[str] = set()
    keywords = _sport_keywords(page.sport_key)
    for idx, node in enumerate(candidates):
        text = _compact(node.get_text(" "))
        if len(text) < 8 or len(text) > 450:
            continue
        lower = text.lower()
        if keywords and not any(token in lower for token in keywords):
            continue
        link = node.select_one("a[href]")
        href = urljoin(page.url, link.get("href")) if link else page.url
        title_node = node.select_one("h1,h2,h3,[class*='title'],[class*='name'],[class*='participant']")
        title = _compact(title_node.get_text(" ") if title_node else text)
        if len(title) > 140:
            title = title[:140].rsplit(" ", 1)[0]
        if len(title) < 5:
            continue
        event_date = _parse_datetime_from_node(node, fallback_days=idx)
        external_id = f"selenium-{page.key}-{_slug(title)}-{event_date.strftime('%Y%m%d%H%M')}"
        if external_id in seen:
            continue
        seen.add(external_id)
        events.append(
            {
                "sportKey": page.sport_key,
                "externalId": external_id,
                "title": title,
                "competitionName": page.source_name,
                "eventDate": event_date.isoformat() + "Z",
                "status": _event_status(text, event_date),
                "time": event_date.strftime("%H:%M UTC"),
                "venue": "",
                "country": "",
                "source": page.source_name,
                "sourceUrl": href,
                "participants": _split_competitors(title),
                "results": [],
                "streams": [],
            }
        )
        if len(events) >= MAX_ITEMS_PER_SOURCE:
            break
    return events


def _article_details(url: str) -> dict[str, str]:
    try:
        response = requests.get(url, headers=DEFAULT_HEADERS, timeout=TIMEOUT)
        response.raise_for_status()
    except Exception:
        return {}
    soup = BeautifulSoup(response.text, "lxml")
    title_node = soup.select_one("h1") or soup.select_one("meta[property='og:title']")
    title = title_node.get("content", "") if title_node and title_node.name == "meta" else (title_node.get_text(" ", strip=True) if title_node else "")
    image_node = soup.select_one("meta[property='og:image']") or soup.select_one("article img, .ArticleDetails img, .details img")
    image = ""
    if image_node:
        image = image_node.get("content") or image_node.get("data-src") or image_node.get("src") or ""
        image = urljoin(url, image) if image else ""
    article = soup.select_one("article") or soup.select_one(".ArticleDetails") or soup.select_one(".details") or soup.select_one("main")
    paragraphs: list[str] = []
    if article:
        for bad in article.select("script,style,iframe,.ad,.ads,[class*='ad'],[id*='ad']"):
            bad.decompose()
        for p in article.select("p"):
            text = _compact(p.get_text(" "))
            if len(text) > 25:
                paragraphs.append(text)
    desc_node = soup.select_one("meta[property='og:description'], meta[name='description']")
    if not paragraphs and desc_node:
        paragraphs.append(_compact(desc_node.get("content", "")))
    return {"title": title, "image_url": image, "content": "\n\n".join(dict.fromkeys(paragraphs))}


def _extract_news_from_html(html: str, page: PageSource) -> list[dict[str, Any]]:
    soup = BeautifulSoup(html, "lxml")
    candidates = soup.select("article, li, .news, [class*='article'], [class*='card']")
    news: list[dict[str, Any]] = []
    seen: set[str] = set()
    for node in candidates:
        link = node.select_one("a[href]")
        if not link:
            continue
        href = urljoin(page.url, link.get("href"))
        parsed = urlparse(href)
        if not parsed.scheme.startswith("http"):
            continue
        title_node = node.select_one("h1,h2,h3,[class*='title']")
        title = _compact(title_node.get_text(" ") if title_node else link.get_text(" "))
        if len(title) < 10 or href in seen:
            continue
        seen.add(href)
        details = _article_details(href)
        content = details.get("content") or title
        if len(content) < 20:
            continue
        img = details.get("image_url")
        if not img:
            img_node = node.select_one("img")
            if img_node:
                img = urljoin(page.url, img_node.get("data-src") or img_node.get("src") or "")
        news.append(
            {
                "title": details.get("title") or title,
                "description": content[:180],
                "content": content,
                "image_url": img or "",
                "url": href,
                "published_at": _utc_now().isoformat() + "Z",
                "source": page.source_name,
                "sport_key": page.sport_key,
            }
        )
        if len(news) >= MAX_ITEMS_PER_SOURCE:
            break
    return news


# ---------------------------------------------------------------------------
# F1 schedule: rich JSON-LD aware parser, merged in from the old
# f1_motorsport_importer.py (unchanged logic, just reading from Selenium's
# already-rendered page_source instead of a plain requests.get()).
# ---------------------------------------------------------------------------
def _json_ld_objects(soup: BeautifulSoup) -> list[dict[str, Any]]:
    objects: list[dict[str, Any]] = []
    for script in soup.select('script[type="application/ld+json"]'):
        try:
            data = json.loads(script.string or script.get_text() or "{}")
        except json.JSONDecodeError:
            continue
        stack = data if isinstance(data, list) else [data]
        for item in stack:
            if isinstance(item, dict):
                graph = item.get("@graph")
                if isinstance(graph, list):
                    objects.extend(x for x in graph if isinstance(x, dict))
                objects.append(item)
    return objects


def _f1_event_status(event_date: datetime) -> str:
    now = _utc_now()
    starts_at = event_date - timedelta(minutes=F1_LIVE_WINDOW_MINUTES_BEFORE)
    ends_at = event_date + timedelta(minutes=F1_LIVE_WINDOW_MINUTES_AFTER)
    if starts_at <= now <= ends_at:
        return "Live"
    return "Scheduled" if event_date > now else "Completed"


def _build_f1_event(title: str, event_date: datetime, venue: str, country: str, source_url: str) -> dict[str, Any]:
    return {
        "sportKey": "formula1",
        "externalId": f"motorsport-f1-{F1_YEAR}-{_slug(title)}-{event_date.strftime('%Y%m%d%H%M')}",
        "title": title,
        "competitionName": "Formula 1",
        "eventDate": event_date.isoformat() + "Z",
        "status": _f1_event_status(event_date),
        "time": event_date.strftime("%H:%M UTC"),
        "venue": venue,
        "country": country,
        "source": "Motorsport.com",
        "sourceUrl": source_url,
        "participants": [],
        "results": [],
        "streams": [],
    }


def _f1_event_from_json_ld(item: dict[str, Any], page: PageSource) -> dict[str, Any] | None:
    item_type = item.get("@type")
    types = item_type if isinstance(item_type, list) else [item_type]
    if "SportsEvent" not in types and "Event" not in types:
        return None
    name = _compact(item.get("name"))
    start = _parse_date(str(item.get("startDate") or ""))
    if not name or not start:
        return None
    location = item.get("location") if isinstance(item.get("location"), dict) else {}
    venue = _compact(location.get("name"))
    address = location.get("address") if isinstance(location.get("address"), dict) else {}
    country = _compact(address.get("addressCountry") or address.get("addressLocality"))
    url = urljoin(page.url, str(item.get("url") or page.url))
    return _build_f1_event(name, start, venue, country, url)


def _f1_events_from_cards(soup: BeautifulSoup, page: PageSource) -> list[dict[str, Any]]:
    events: list[dict[str, Any]] = []
    selectors = ["[class*='schedule'] [class*='event']", "[class*='ms-schedule'] article", "article"]
    for card in soup.select(",".join(selectors)):
        text = _compact(card.get_text(" "))
        lower_text = text.lower()
        if not text or ("formula" not in lower_text and "grand prix" not in lower_text):
            continue
        link = card.select_one("a[href]")
        title_node = card.select_one("h1,h2,h3,[class*='title'],[class*='name']")
        title = _compact(title_node.get_text(" ") if title_node else (link.get_text(" ") if link else text[:80]))
        date_node = card.select_one("time[datetime],time,[class*='date']")
        raw_date = date_node.get("datetime") if date_node and date_node.has_attr("datetime") else (date_node.get_text(" ") if date_node else text)
        parsed = _parse_date(str(raw_date))
        if title and parsed:
            href = link.get("href") if link else page.url
            events.append(_build_f1_event(title, parsed, "", "", urljoin(page.url, href)))
    return events


def _extract_f1_schedule_from_html(html: str, page: PageSource) -> list[dict[str, Any]]:
    soup = BeautifulSoup(html, "lxml")
    by_id: dict[str, dict[str, Any]] = {}
    for item in _json_ld_objects(soup):
        event = _f1_event_from_json_ld(item, page)
        if event:
            by_id[event["externalId"]] = event
    for event in _f1_events_from_cards(soup, page):
        by_id.setdefault(event["externalId"], event)
    return sorted(by_id.values(), key=lambda e: e["eventDate"])


def _attach_official_watch_url(events: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Attach an operator-configured official watch URL to live/upcoming F1
    events only. Mirrors the old f1_motorsport_importer.py behavior — does not
    crawl third-party players, bypass headers, or discover HLS manifests."""
    if not OFFICIAL_WATCH_URL:
        return events

    linked_events: list[dict[str, Any]] = []
    for event in events:
        if event.get("sportKey") != "formula1":
            linked_events.append(event)
            continue
        event_copy = dict(event)
        status = str(event_copy.get("status") or "").strip().lower()
        if status in {"live", "scheduled"}:
            streams = list(event_copy.get("streams") or [])
            if not any(stream.get("streamUrl") == OFFICIAL_WATCH_URL for stream in streams if isinstance(stream, dict)):
                streams.append({
                    "source": OFFICIAL_WATCH_SOURCE,
                    "streamUrl": OFFICIAL_WATCH_URL,
                    "m3U8Url": "",
                    "statusMessage": "official/operator configured watch link",
                })
            event_copy["streams"] = streams
        linked_events.append(event_copy)
    return linked_events


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main() -> int:
    if webdriver is None:
        print(f"[multisport] ERROR: Selenium dependencies are missing: {SELENIUM_IMPORT_ERROR}", file=sys.stderr)
        return 2

    driver = _build_driver()
    all_events: list[dict[str, Any]] = []
    all_news: list[dict[str, Any]] = []
    failures: dict[str, str] = {}

    try:
        for page in SOURCES:
            if page.key not in ENABLED_SOURCES:
                continue
            try:
                html = _render(driver, page.url)
                if page.mode == "f1_schedule":
                    items = _extract_f1_schedule_from_html(html, page)
                    all_events.extend(items)
                    print(f"[multisport] {page.key}: parsed {len(items)} F1 schedule events")
                elif page.mode == "event":
                    items = _extract_events_from_html(html, page)
                    all_events.extend(items)
                    print(f"[multisport] {page.key}: parsed {len(items)} events")
                else:  # "news"
                    items = _extract_news_from_html(html, page)
                    all_news.extend(items)
                    print(f"[multisport] {page.key}: parsed {len(items)} news items")
            except Exception as exc:
                failures[page.key] = str(exc)
                print(f"[multisport] WARNING: {page.key} failed: {exc}")
                # A hung/challenge page can leave the ChromeDriver session itself
                # in a broken state (this is exactly what caused every source
                # after the first failure to also fail with the same read-timeout
                # in earlier runs). Recycle the driver so the remaining sources
                # in SOURCES still get a fair, independent attempt.
                _quit_driver_safely(driver)
                try:
                    driver = _build_driver()
                except Exception as rebuild_exc:
                    print(f"[multisport] ERROR: could not rebuild driver after {page.key} failure: {rebuild_exc}", file=sys.stderr)
                    failures["driver_rebuild"] = str(rebuild_exc)
                    break
    finally:
        _quit_driver_safely(driver)

    all_events = _attach_official_watch_url(all_events)

    overrides = _load_stream_overrides()
    if overrides:
        all_events = [_attach_stream_overrides(event, overrides) for event in all_events]

    _post_json("/api/other-sports/import/events", all_events)
    _post_json("/api/SportsData/import/news", all_news)

    summary = {"events": len(all_events), "news": len(all_news), "failures": failures}
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0 if not failures else 1


if __name__ == "__main__":
    raise SystemExit(main())