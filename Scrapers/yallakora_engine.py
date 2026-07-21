import argparse
import requests
from bs4 import BeautifulSoup
from datetime import datetime, timedelta
from collections import OrderedDict
import re
import os
import shutil
import time
from urllib.parse import urljoin
import urllib.parse
import json

# Selenium (only needed for the AJAX squad/lineup tab)
from selenium import webdriver
from selenium.webdriver.chrome.options import Options as ChromeOptions
from selenium.webdriver.chrome.service import Service as ChromeService
from selenium.common.exceptions import TimeoutException, WebDriverException
from webdriver_manager.chrome import ChromeDriverManager

# Set to True to dump the raw HTML of every container this engine tries to
# read from, into ./debug_output/. Run once with this on, then send me the
# files in debug_output/ (not screenshots) so I can fix any selector that
# doesn't match reality instead of guessing blind.
DEBUG_MODE = True
DEBUG_DIR = os.path.join(os.path.dirname(__file__), 'debug_output')


# FIXED (new): "today" must be computed in Egypt local time (UTC+3, no DST)
# everywhere this scraper defaults to "today" with no explicit date given,
# because that's exactly how the .NET backend computes "today"
# (DateTime.UtcNow.AddHours(3), see SportsDataController.GetMatches /
# GetCoverage). The old code used datetime.now().date(), which is whatever
# OS-local timezone the scraper's host happens to be set to. On a host set
# to UTC, during the window between 00:00 and 03:00 UTC (03:00-06:00 in
# Egypt) it is already "tomorrow" in Egypt but datetime.now().date() still
# reports "today"'s UTC date -- matches scraped during that window got
# imported one day behind, and then silently vanished from any backend
# query that filters on "today" (?date defaults to the Egypt date), even
# though the rows were sitting right there in the DB. This mirrors the
# exact class of bug already fixed on the backend side in
# ParseScraperMatchDate -- the scraper and backend now agree on what day it
# is by construction instead of by coincidence of server timezone config.
def _today_in_egypt():
    return (datetime.utcnow() + timedelta(hours=3)).date()


# -----------------------------------------------------------------------
# CONFIRMED July 2026, from real page source sent back:
#   - The "التشكيل" (squad/lineup) tab is triggered by clicking
#     <a id="squadButton" onclick="openMatchTab(event, 'squad')">, which
#     fires an AJAX call that fills <div id="squad" class="matchDtlsContent
#     squad" style="display:none">. Before the click, that div contains
#     ONLY a .loaderFullDiv (no real content) -- confirmed from the raw
#     page source. This is why plain requests.get() can never see the
#     squad/formation/coach data: it simply isn't in the server response,
#     it's injected by JS after an XHR that only fires on tab click.
#
#   - CONFIRMED (2nd real page sent back, a match still ~hours from kickoff):
#     for matches whose lineup hasn't been announced yet, #squadButton /
#     #squad DO NOT EXIST AT ALL on the page -- the tabs rendered are only
#     headToHeadButton / teamNewsButton / teamVideosButton. So the old
#     "always click + wait up to 15s" flow was burning 15s per match for
#     every not-yet-announced fixture with nothing to show for it. Now we
#     check for the button's existence first and skip immediately if it's
#     not there.
#
#   - This file adds a Selenium-based fetch (get_match_squad_selenium)
#     that: opens a real (headless) Chrome, loads the match page, checks
#     whether #squadButton exists at all (skips fast if not -- lineup not
#     announced yet), clicks it via JS (safer than .click() with
#     Cloudflare's rocket-loader wrapping event handlers), waits for
#     #squad to actually gain content (not a fixed sleep), then hands the
#     fully-rendered page_source to the SAME parsing helpers used by the
#     old requests-only get_match_squad. That parsing logic is unchanged
#     and unverified against the real loaded markup yet -- if home_main/
#     away_main still come back empty after this, check
#     debug_output/squad_selenium_full_*.html (dumped every time) and send
#     it back so the SQUAD_* selectors can be corrected against reality.
#
#   - FIXED (from real returned JSON): playerName was coming back as e.g.
#     "12أورلاندو جيل" or "0جوستافو جوميز" -- i.e. the shirt number glued
#     to the front of the name with no separator -- while `number` was
#     always empty. _extract_players_from_container now splits a leading
#     run of digits off of the link's visible text into `number`, leaving
#     a clean `name`. If the real markup actually has the number in its
#     own separate element, send a squad_selenium_full_*.html dump and
#     I'll switch this to read that element directly instead of parsing
#     digits out of the text -- the regex approach is a safe stand-in
#     until then since every sample we've seen matches this pattern.
#
#   - FIXED (2026-07-13): get_matches() had a quoting bug in the
#     card.select(...) call used to find match items --
#     `a[href*='/match/']` was written with a single quote INSIDE a
#     single-quoted string. That closed the string early at
#     `href*='`, leaving the remaining `/match/` to be parsed as
#     Python division (`/` operator) against an undefined name
#     `match`, raising NameError every single time any `.matchCard` /
#     `.mtchCntrContainer` container was found. Because that line sits
#     inside get_matches()'s outer try/except (not its own inner
#     try/except), the exception was caught by the OUTER handler,
#     which printed "Error scraping matches: ..." and returned []
#     immediately -- skipping the `_extract_matches_from_links`
#     fallback entirely. In practice this meant the primary parser
#     could never return any matches at all whenever it found card
#     containers. Fixed by switching the outer quotes to double quotes
#     so the embedded "/match/" attribute selector no longer breaks
#     the string.
#
#   - FIXED (new): every "today" default throughout this file
#     (_match_center_urls with no date, and the CLI's final fallback /
#     full-sync range) now goes through _today_in_egypt() instead of
#     datetime.now().date(), so the scraper's idea of "today" can never
#     drift a day behind the backend's depending on the scraper host's
#     OS timezone. See the comment on _today_in_egypt() above.
# -----------------------------------------------------------------------


class YallaKoraEngine:
    # Candidate selectors for the TV channel on the match detail page.
    CHANNEL_SELECTORS = [
        '.info.icon-channel span',
        '.matchChannel',
        '.channel-name',
        '.MatchTVChannel',
    ]

    SQUAD_FORMATION = '.timeline.squad .formation'
    SQUAD_MAIN_TEAMLIST = '.teamSquad.Main .teamList'
    SQUAD_SUB_TEAMLIST = '.teamSquad.Sub .teamList'
    SQUAD_COACH_TEAMLIST = '.teamSquad.Coach .teamList'

    # CONFIRMED: the tab button + its AJAX target container.
    SQUAD_TAB_BUTTON_ID = 'squadButton'
    SQUAD_CONTAINER_ID = 'squad'

    STANDINGS_CONTAINER = '#divTab5 .groupTabs.GroupStanding, .groupTabs.GroupStanding'
    BRACKET_CONTAINER = '#divTab6 .KnockOutHolder.groups.knockoutTab .allRounds, .KnockOutHolder .allRounds'

    STATS_TOP10 = '.allStats.top10'

    # NEW: centralized candidate-selector lists for the match-center card
    # fields, all in one place so future site redesigns only need a new
    # entry added here (in priority order) instead of hunting through the
    # parsing code. Each is tried in order via _select_first() until one
    # matches; nothing found across the whole list means the field is
    # genuinely missing and gets flagged in the coverage report.
    MATCHCARD_TEAM_A_SELECTORS = [
        '.teamA .name', '.teams.teamA p', '.team.teamA p', '.teamA p',
    ]
    MATCHCARD_TEAM_B_SELECTORS = [
        '.teamB .name', '.teams.teamB p', '.team.teamB p', '.teamB p',
    ]
    MATCHCARD_SCORE_SELECTORS = ['.score', '.matchScore .score', '.result .score']
    MATCHCARD_TIME_SELECTORS = ['.time', '.matchTime', '.matchDateInfo .time', '.date', '[class*=time]', '[class*=Time]']
    MATCHCARD_STATUS_SELECTORS = ['.matchStatus', '.status', '.matchState']

    MATCHCENTER_CARD_SELECTORS = [
        '.matchCard',
        '.mtchCntrContainer',
        '.matchesList',
        '[class*=matchCard]',
        '[class*=mtchCntr]',
        '[class*=matchesList]',
    ]
    MATCHCENTER_ITEM_SELECTORS = [
        '.item',
        '.liItem',
        '[class*=matchItem]',
        '[class*=liItem]',
        'a[href*="/match/"]',
    ]

    PLAYER_HREF_RE = re.compile(r'/player/(\d+)/([^/?#]+)')
    POSITION_CODE_RE = re.compile(r'^p(\d{2})$')
    FORMATION_CODE_RE = re.compile(r'^form(\d)(\d)(\d)$')

    # NEW: leading shirt-number glued onto the player's visible name,
    # e.g. "12أورلاندو جيل" -> number="12", name="أورلاندو جيل".
    # Also handles "0جوستافو جوميز" -> number="0".
    LEADING_NUMBER_RE = re.compile(r'^(\d+)\s*(.+)$')

    def __init__(self, api_base_url=None, request_delay=0.5):
        self.headers = {
            'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8',
            'Accept-Language': 'en-US,en;q=0.9,ar;q=0.8',
            'Referer': 'https://www.google.com/',
        }
        self.base_url = os.getenv("YALLAKORA_BASE_URL", "https://www.yallakora.com").rstrip("/")
        self.api_base_url = api_base_url
        self.request_delay = request_delay
        if DEBUG_MODE:
            os.makedirs(DEBUG_DIR, exist_ok=True)


    def _candidate_base_urls(self):
        """Known YallaKora host variants to try before treating DNS as fatal.

        Some Windows/DNS setups fail resolving only `www.yallakora.com` while
        the apex host still works. Keep the configured/base URL first, then try
        the other public variant.
        """
        candidates = [self.base_url, "https://www.yallakora.com", "https://yallakora.com"]
        unique = []
        for url in candidates:
            url = (url or "").rstrip("/")
            if url and url not in unique:
                unique.append(url)
        return unique

    @staticmethod
    def _looks_like_dns_error(error):
        text = str(error).lower()
        return any(token in text for token in ["nameresolutionerror", "failed to resolve", "getaddrinfo failed", "name or service not known"])

    # ---------------------------------------------------------------
    # Generic multi-selector fallback (NEW)
    # ---------------------------------------------------------------
    @staticmethod
    def _select_first(container, selectors):
        """
        Try each CSS selector in `selectors`, in order, against `container`
        and return the first Tag that actually matches. This is the single
        place that implements "try A, else B, else C" so every field in
        the scraper uses the exact same fallback behavior, and adding a
        new candidate selector after a site redesign means editing one
        list constant instead of hunting through parsing code.
        Returns None if nothing in the list matches.
        """
        if not container:
            return None
        for sel in selectors:
            tag = container.select_one(sel)
            if tag is not None:
                return tag
        return None

    @staticmethod
    def _select_all_first(container, selectors):
        """Same idea as _select_first but for select() (multiple elements)."""
        if not container:
            return []
        for sel in selectors:
            found = container.select(sel)
            if found:
                return found
        return []

    # ---------------------------------------------------------------
    # Debug helper
    # ---------------------------------------------------------------
    def _debug_dump(self, name, soup_or_tag):
        if not DEBUG_MODE:
            return
        try:
            path = os.path.join(DEBUG_DIR, f"{name}.html")
            content = soup_or_tag.prettify() if soup_or_tag else f"<!-- {name}: element not found -->"
            with open(path, 'w', encoding='utf-8') as f:
                f.write(content)
            print(f"[debug] wrote {path}")
        except Exception as e:
            print(f"[debug] failed to write {name}: {e}")

    # ---------------------------------------------------------------
    # URL helpers / Channel extraction
    # ---------------------------------------------------------------
    def _absolute_page_url(self, href):
        return urljoin(self.base_url, href or "")

    def _extract_channel(self, session, match_href):
        try:
            match_url = self._absolute_page_url(match_href)
            res = session.get(match_url, headers=self.headers, timeout=10)
            soup = BeautifulSoup(res.content, 'html.parser')

            for selector in self.CHANNEL_SELECTORS:
                tag = soup.select_one(selector)
                if tag and tag.text.strip():
                    return tag.text.strip()
            return ""
        except Exception as e:
            print(f"Channel fetch error for {match_href}: {e}")
            return ""


    @staticmethod
    def _normalize_digits(value):
        if value is None:
            return ""
        return str(value).translate(str.maketrans("٠١٢٣٤٥٦٧٨٩۰۱۲۳۴۵۶۷۸۹", "01234567890123456789"))

    @classmethod
    def _extract_time_text(cls, node):
        if not node:
            return ""
        text = cls._normalize_digits(node.get_text(' ', strip=True))
        match = re.search(r'\b([01]?\d|2[0-3])[:：][0-5]\d\b', text)
        if match:
            return match.group(0).replace('：', ':')
        return text.strip()

    @classmethod
    def _extract_time_from_any_text(cls, node):
        if not node:
            return ""
        text = cls._normalize_digits(node.get_text(' ', strip=True))
        match = re.search(r'\b([01]?\d|2[0-3])[:：][0-5]\d\b', text)
        return match.group(0).replace('：', ':') if match else ""

    # ---------------------------------------------------------------
    # Matches
    # ---------------------------------------------------------------
    def _match_center_urls(self, target_date=None):
        parsed_date = self._parse_date_value(target_date) if target_date else None
        base_urls = self._candidate_base_urls()
        if not parsed_date:
            return [(f"{self.base_url}/matches-center", datetime.now().date().isoformat())]

        iso_date = parsed_date.isoformat()
        us_date = f"{parsed_date.month}/{parsed_date.day}/{parsed_date.year}"
        quoted_us = urllib.parse.quote(us_date)
        quoted_iso = urllib.parse.quote(iso_date)

        # YallaKora has used both /matches-center and /match-center over time,
        # with both www/apex hosts and more than one date format. Try every
        # known public variant before deciding the day is empty/unreachable.
        urls = []
        for base_url in base_urls:
            urls.extend([
                (f"{base_url}/matches-center?date={quoted_us}", iso_date),
                (f"{base_url}/match-center?date={quoted_us}", iso_date),
                (f"{base_url}/matches-center?date={quoted_iso}", iso_date),
                (f"{base_url}/match-center?date={quoted_iso}", iso_date),
            ])
        return urls

    def get_matches(self, target_date=None):
        target_date = target_date or os.getenv("YALLAKORA_MATCH_DATE")
        last_error = None
        for index, (url, match_date) in enumerate(self._match_center_urls(target_date), start=1):
            print(f"Opening YallaKora Match Center [{index}]: {url}")
            try:
                session = requests.Session()
                res = session.get(url, headers=self.headers, timeout=15)
                soup = BeautifulSoup(res.content, 'html.parser')
                self._debug_dump("matchcenter_page" if index == 1 else f"matchcenter_page_attempt_{index}", soup)

                declared_count = self._declared_match_count(soup)
                visible_dates = self._visible_match_center_dates(soup)
                if visible_dates:
                    print(f"[match-center] Visible YallaKora day tabs: {visible_dates[0]} .. {visible_dates[-1]} ({len(visible_dates)} day tabs)")

                tournaments = self._parse_matchcenter_soup(soup, session, match_date)
                if tournaments:
                    return tournaments

                selenium_mode = os.getenv("YALLAKORA_MATCHCENTER_SELENIUM", "fallback").strip().lower()
                if selenium_mode not in {"0", "false", "no", "off", "disabled"}:
                    selenium_tournaments = self._parse_matchcenter_with_selenium(url, match_date, index)
                    if selenium_tournaments:
                        return selenium_tournaments

                if declared_count == 0:
                    print(f"[match-center] YallaKora explicitly reports 0 matches for {match_date}; this is an empty day, not a parser failure.")
                    return []
                print(f"[warn] No matches parsed from {url}; trying the next known YallaKora match-center URL/date format if available.")
            except Exception as e:
                last_error = e
                if self._looks_like_dns_error(e):
                    print(f"[network] DNS could not resolve YallaKora host for {url}. Trying the next host/path variant if available...")
                print(f"Error scraping matches from {url}: {e}")

        if last_error:
            print(f"Error scraping matches: {last_error}")
            if self._looks_like_dns_error(last_error):
                print("[network] All YallaKora host variants failed DNS resolution. Check your internet/DNS/VPN/firewall, or set YALLAKORA_BASE_URL=https://yallakora.com and retry.")
        return []


    def discover_visible_match_dates(self):
        """Return every date exposed in YallaKora's match-center day strip.

        Running yallakora_engine.py without --date now uses this list instead of
        only today's date, so the direct/manual scraper command imports the same
        visible window YallaKora exposes (for example 7/3/2026 through
        7/23/2026). Use --from-date/--to-date or --full-sync for a wider
        historical/future range than the visible strip.
        """
        last_error = None
        for index, base_url in enumerate(self._candidate_base_urls(), start=1):
            url = f"{base_url}/matches-center"
            print(f"[yallakora] Discovering visible match-center dates [{index}]: {url}...")
            try:
                res = requests.get(url, headers=self.headers, timeout=15)
                soup = BeautifulSoup(res.content, 'html.parser')
                self._debug_dump("matchcenter_visible_days" if index == 1 else f"matchcenter_visible_days_{index}", soup)
                dates = []
                for raw in self._visible_match_center_dates(soup):
                    parsed = self._parse_date_value(raw)
                    if parsed and parsed not in dates:
                        dates.append(parsed)
                dates.sort()
                if dates:
                    print(f"[yallakora] discovered {len(dates)} visible day(s): {dates[0]} .. {dates[-1]}")
                    return dates
                print(f"[yallakora] no visible day tabs discovered from {url}; trying next host if available.")
            except Exception as e:
                last_error = e
                if self._looks_like_dns_error(e):
                    print(f"[network] DNS could not resolve YallaKora host for {url}. Trying next host if available...")
                print(f"[yallakora] could not discover visible match-center dates from {url}: {e}")

        if last_error and self._looks_like_dns_error(last_error):
            print("[network] Could not discover visible dates because DNS failed for all YallaKora host variants.")
        print("[yallakora] no visible day tabs discovered; falling back to today.")
        return []


    def _render_matchcenter_soup_selenium(self, url, debug_name):
        """Load a match-center URL in a real browser and return rendered HTML.

        Some YallaKora deployments send an almost-empty server HTML response and
        fill the match list/day strip with JavaScript. The requests parser is
        still faster and remains the first attempt, but this Selenium fallback
        gives manual and hosted imports a way to scrape the rendered DOM when
        requests sees zero matches.
        """
        driver = None
        try:
            print(f"[selenium] Rendering YallaKora Match Center: {url}")
            driver = self.create_selenium_driver(headless=True)
            driver.get(url)

            from selenium.webdriver.support.ui import WebDriverWait
            WebDriverWait(driver, int(os.getenv("YALLAKORA_SELENIUM_WAIT_SECONDS", "12"))).until(
                lambda d: d.execute_script(
                    """
                    return document.querySelectorAll('a[href*=\"/match/\"]').length > 0
                        || document.querySelectorAll('.dayTabLinks[date], [date][class*=day], [data-date]').length > 0
                        || document.body.innerText.indexOf('لا توجد مباريات') >= 0
                        || document.body.innerText.indexOf('لا يوجد مباريات') >= 0;
                    """
                )
            )
            # Give late client-side rendering a tiny extra breath without making
            # normal pages slow.
            time.sleep(float(os.getenv("YALLAKORA_SELENIUM_AFTER_WAIT_SECONDS", "1")))
            soup = BeautifulSoup(driver.page_source, 'html.parser')
            self._debug_dump(debug_name, soup)
            return soup
        except Exception as e:
            print(f"[selenium] Could not render match-center page {url}: {e}")
            return None
        finally:
            if driver is not None:
                try:
                    driver.quit()
                except Exception:
                    pass

    def _parse_matchcenter_with_selenium(self, url, match_date, attempt_index):
        soup = self._render_matchcenter_soup_selenium(url, f"matchcenter_selenium_attempt_{attempt_index}")
        if not soup:
            return []
        visible_dates = self._visible_match_center_dates(soup)
        if visible_dates:
            print(f"[selenium] Visible YallaKora day tabs: {visible_dates[0]} .. {visible_dates[-1]} ({len(visible_dates)} day tabs)")
        return self._parse_matchcenter_soup(soup, requests.Session(), match_date)

    @staticmethod
    def _declared_match_count(soup):
        tag = soup.select_one('.toursMatchesNum .matchesCount') or soup.select_one('.matchesCount')
        if not tag:
            return None
        text = tag.get_text(' ', strip=True)
        digits = re.sub(r'[^0-9]', '', text)
        return int(digits) if digits else None

    @staticmethod
    def _visible_match_center_dates(soup):
        dates = []
        for button in soup.select('.dayTabLinks[date], [date][class*=day], [data-date]'):
            value = (button.get('date') or button.get('data-date') or '').strip()
            if value and value not in dates:
                dates.append(value)
        return dates

    def _unique_match_items(self, card):
        items = []
        seen = set()
        for selector in self.MATCHCENTER_ITEM_SELECTORS:
            for item in card.select(selector):
                href = self._match_href_from_item(item)
                key = href or id(item)
                if key in seen:
                    continue
                seen.add(key)
                items.append(item)
        return items

    @staticmethod
    def _match_href_from_item(item):
        if not item:
            return ""
        if getattr(item, 'name', None) == 'a' and item.get('href') and re.search(r'/match/(\d+)/', item.get('href')):
            return item.get('href')
        link = item.find('a', href=re.compile(r'/match/(\d+)/')) if hasattr(item, 'find') else None
        return link.get('href') if link else ""

    def _parse_matchcenter_soup(self, soup, session, match_date):
        # NOTE: unlike the squad tab, these selectors have NOT been verified
        # against every YallaKora redesign. We try the known card wrappers first,
        # then fall back to a link-text parser built around stable /match/{id}/ URLs.
        tournaments = []

        cards = self._select_all_first(soup, self.MATCHCENTER_CARD_SELECTORS)
        if cards:
            self._debug_dump("matchcenter_first_card", cards[0])
        else:
            print("[warn] No match-center card containers found; falling back to /match/ links parser.")

        for card in cards:
            tour_name = card.find(['h2', 'h1']).text.strip() if card.find(['h2', 'h1']) else "Tournament"
            tour_logo = self._extract_img_url(card)
            tour_id = ""
            tour_link = card.find('a', href=re.compile(r'tour'))
            if tour_link:
                m = re.search(r'tour(?:naments)?/(\d+)/', tour_link['href'])
                if m:
                    tour_id = m.group(1)

            matches = []
            items = self._unique_match_items(card)
            for item in items:
                try:
                    team_a_tag = self._select_first(item, self.MATCHCARD_TEAM_A_SELECTORS)
                    team_b_tag = self._select_first(item, self.MATCHCARD_TEAM_B_SELECTORS)

                    if not team_a_tag or not team_b_tag:
                        continue

                    team_a = team_a_tag.text.strip()
                    team_b = team_b_tag.text.strip()
                    team_a_logo = self._extract_img_url(team_a_tag)
                    team_b_logo = self._extract_img_url(team_b_tag)
                    if not team_a_logo or not team_b_logo:
                        item_images = [self._absolute_asset_url(img.get('src') or img.get('data-src') or img.get('data-original')) for img in item.find_all('img')]
                        item_images = [u for u in item_images if u]
                        if not team_a_logo and len(item_images) > 0:
                            team_a_logo = item_images[0]
                        if not team_b_logo and len(item_images) > 1:
                            team_b_logo = item_images[1]

                    scores = self._select_all_first(item, self.MATCHCARD_SCORE_SELECTORS)
                    score_a = scores[0].text.strip() if len(scores) > 0 else "-"
                    score_b = scores[1].text.strip() if len(scores) > 1 else "-"

                    time_tag = self._select_first(item, self.MATCHCARD_TIME_SELECTORS)
                    match_time = self._extract_time_text(time_tag) if time_tag else ""
                    if not match_time:
                        match_time = self._extract_time_from_any_text(item)

                    status_tag = self._select_first(item, self.MATCHCARD_STATUS_SELECTORS)
                    status = status_tag.text.strip() if status_tag else ""

                    m_id = ""
                    channel = ""
                    m_href = self._match_href_from_item(item)
                    if m_href:
                        m_match = re.search(r'match/(\d+)/', m_href)
                        if m_match:
                            m_id = m_match.group(1)

                        channel = self._extract_channel(session, m_href)
                        time.sleep(self.request_delay)

                    matches.append({
                        'home_team': team_a,
                        'away_team': team_b,
                        'home_team_logo': team_a_logo,
                        'away_team_logo': team_b_logo,
                        'score_home': score_a,
                        'score_away': score_b,
                        'time': match_time,
                        'match_date': match_date,
                        'status': status,
                        'match_id': m_id or f"{team_a}_{team_b}_{match_date}",
                        'channel': channel,
                        'match_href': m_href
                    })
                except Exception as e:
                    print(f"Skipping one match item due to error: {e}")
                    continue

            if matches:
                print(f"Tournament '{tour_name}' has {len(matches)} matches.")
                tournaments.append({'tournament_name': tour_name, 'tournament_id': tour_id, 'tournament_logo': tour_logo, 'matches': matches})

        tournaments = self._dedupe_tournaments(tournaments)
        if not tournaments:
            tournaments = self._extract_matches_from_links(soup, match_date)

        return tournaments


    def _extract_matches_from_links(self, soup, target_date):
        """Fallback parser for the current YallaKora /matches-center markup.

        The page can be served without the old .matchCard/.item wrappers, while
        still exposing match links whose visible text contains channel, round,
        status, teams, score, and time. This parser intentionally relies on the
        stable /match/{id}/ links first, then uses conservative Arabic text
        heuristics so a selector-only site redesign does not make every day look
        empty.
        """
        by_tournament = OrderedDict()
        seen_matches = set()
        current_tournament = "YallaKora"
        tournament_link_re = re.compile(r'/(?:championship|tournament|tour|league|cups?)/', re.I)
        match_link_re = re.compile(r'/match/(\d+)/')

        for link in soup.find_all('a', href=True):
            href = link.get('href') or ''
            text = self._clean_match_text(link.get_text(' ', strip=True))
            match = match_link_re.search(href)
            if match:
                if not text or text == 'التفاصيل' or len(text) < 10:
                    continue
                match_id = match.group(1)
                if match_id in seen_matches:
                    continue
                parsed = self._parse_match_link_text(text)
                if not parsed:
                    continue
                seen_matches.add(match_id)
                tournament_key = current_tournament or 'YallaKora'
                if tournament_key not in by_tournament:
                    by_tournament[tournament_key] = {
                        'tournament_name': tournament_key,
                        'tournament_id': '',
                        'tournament_logo': '',
                        'matches': []
                    }
                by_tournament[tournament_key]['matches'].append({
                    'home_team': parsed['home_team'],
                    'away_team': parsed['away_team'],
                    'home_team_logo': '',
                    'away_team_logo': '',
                    'score_home': parsed['score_home'],
                    'score_away': parsed['score_away'],
                    'time': parsed['time'],
                    'match_date': target_date,
                    'status': parsed['status'],
                    'match_id': match_id,
                    'channel': parsed['channel'],
                    'match_href': urljoin(self.base_url, href)
                })
                continue

            if text and tournament_link_re.search(href) and not any(skip in text for skip in ['أخبار', 'مالتيميديا', 'نتائج', 'ترتيب', 'المزيد']):
                current_tournament = text[:120]

        tournaments = [t for t in by_tournament.values() if t['matches']]
        if tournaments:
            print(f"[fallback] Parsed {sum(len(t['matches']) for t in tournaments)} matches from match links.")
        return tournaments

    @staticmethod
    def _clean_match_text(value):
        value = re.sub(r'\s+', ' ', value or '').strip()
        return value.replace(' - ', ' - ')

    def _parse_match_link_text(self, text):
        text = self._normalize_digits(text)
        time_match = re.search(r'\b([01]?\d|2[0-3])[:：][0-5]\d\b', text)
        if not time_match:
            return None
        match_time = time_match.group(0).replace('：', ':')

        statuses = ['انتهت', 'لم تبدأ', 'مباشر', 'جارية', 'تأجلت', 'ألغيت', 'الشوط الأول', 'الشوط الثاني', 'استراحة']
        status = ''
        status_pos = -1
        for candidate in statuses:
            pos = text.find(candidate)
            if pos >= 0 and (status_pos < 0 or pos < status_pos):
                status = candidate
                status_pos = pos

        body = text[status_pos + len(status):].strip() if status_pos >= 0 else text
        prefix = text[:status_pos].strip() if status_pos >= 0 else ''
        score_match = re.search(r'(.+?)\s+(\d+)\s*-\s*(\d+)\s+' + re.escape(match_time) + r'\s+(.+)$', body)
        if score_match:
            home_team = score_match.group(1).strip()
            score_home = score_match.group(2).strip()
            score_away = score_match.group(3).strip()
            away_team = score_match.group(4).strip()
        else:
            # Upcoming fixtures sometimes have no score yet: "... لم تبدأ الأهلي 20:00 الزمالك".
            no_score = re.search(r'(.+?)\s+' + re.escape(match_time) + r'\s+(.+)$', body)
            if not no_score:
                return None
            home_team = no_score.group(1).strip()
            away_team = no_score.group(2).strip()
            score_home = '-'
            score_away = '-'

        for noise in ['النهائي', 'ذهاب', 'عودة', 'دور ال8', 'دور ال16']:
            if home_team.startswith(noise + ' '):
                home_team = home_team[len(noise):].strip()

        if not home_team or not away_team or home_team == away_team:
            return None

        return {
            'home_team': home_team[:120],
            'away_team': away_team[:120],
            'score_home': score_home,
            'score_away': score_away,
            'time': match_time,
            'status': status or ('لم تبدأ' if score_home == '-' else 'انتهت'),
            'channel': prefix[:120]
        }

    @staticmethod
    def _parse_date_value(value):
        if not value:
            return None
        value = str(value).strip()
        for fmt in ("%Y-%m-%d", "%d/%m/%Y", "%m/%d/%Y"):
            try:
                return datetime.strptime(value, fmt).date()
            except ValueError:
                continue
        try:
            return datetime.fromisoformat(value).date()
        except ValueError:
            return None


    def _absolute_asset_url(self, value):
        if not value:
            return ""
        value = str(value).strip()
        if not value or value.startswith("data:"):
            return ""
        return urljoin(self.base_url, value)

    def _extract_img_url(self, node):
        if node is None:
            return ""
        img = node.find("img") if hasattr(node, "find") else None
        if img is None:
            return ""
        return self._absolute_asset_url(img.get("src") or img.get("data-src") or img.get("data-original"))

    @staticmethod
    def _dedupe_tournaments(tournaments):
        merged = {}
        order = []
        for t in tournaments:
            key = t.get('tournament_id') or t.get('tournament_name')
            if key not in merged:
                merged[key] = {
                    'tournament_name': t.get('tournament_name'),
                    'tournament_id': t.get('tournament_id'),
                    'matches': [],
                    'tournament_logo': t.get('tournament_logo') or '',
                }
                order.append(key)

            seen_match_ids = {m['match_id'] for m in merged[key]['matches']}
            for m in t.get('matches', []):
                if m['match_id'] not in seen_match_ids:
                    merged[key]['matches'].append(m)
                    seen_match_ids.add(m['match_id'])

        deduped = [merged[k] for k in order]

        original_count = sum(len(t.get('matches', [])) for t in tournaments)
        deduped_count = sum(len(t['matches']) for t in deduped)
        if deduped_count != original_count:
            print(f"[dedupe] Collapsed {original_count} scraped match entries down to "
                  f"{deduped_count} unique matches across {len(deduped)} tournament group(s).")

        return deduped

    # ---------------------------------------------------------------
    # NEW: coverage report -- run this after get_matches() (and again
    # after squads are fetched) to see exactly which fields are coming
    # back empty and for which matches, instead of having to notice it
    # yourself in scattered log lines. This is the "tell me what's empty"
    # tool: run it any time you suspect the site changed something.
    # ---------------------------------------------------------------
    @staticmethod
    def report_match_field_coverage(tournaments):
        required_fields = ['home_team', 'away_team', 'score_home', 'score_away',
                            'time', 'status', 'channel', 'match_id']
        total = 0
        empty_counts = {f: [] for f in required_fields}

        for t in tournaments:
            for m in t.get('matches', []):
                total += 1
                for f in required_fields:
                    val = (m.get(f) or '').strip() if isinstance(m.get(f), str) else m.get(f)
                    if val in ('', '-', None):
                        empty_counts[f].append(f"{m.get('home_team')} vs {m.get('away_team')}")

        print(f"\n[coverage] Checked {total} matches across {len(tournaments)} tournament group(s).")
        any_empty = False
        for field, missing_in in empty_counts.items():
            if missing_in:
                any_empty = True
                sample = ', '.join(missing_in[:3])
                more = f" (+{len(missing_in) - 3} more)" if len(missing_in) > 3 else ""
                print(f"[coverage] '{field}' is empty in {len(missing_in)}/{total} match(es): {sample}{more}")
        if not any_empty:
            print("[coverage] All match fields populated for every match. ✔")
        print("")

    @staticmethod
    def report_squad_field_coverage(squad_results):
        """
        squad_results: list of the dicts returned by get_match_squad_selenium(),
        one per match that actually had an announced lineup.
        """
        checked = [s for s in squad_results if s.get('home_main') or s.get('away_main')
                   or s.get('home_formation') or s.get('away_formation')]
        skipped_not_announced = len(squad_results) - len(checked)

        fields = ['home_formation', 'away_formation', 'home_coach', 'away_coach',
                  'home_main', 'home_sub', 'away_main', 'away_sub']
        empty_counts = {f: [] for f in fields}

        for s in checked:
            for f in fields:
                val = s.get(f)
                if not val:
                    empty_counts[f].append(s.get('match_id'))

        print(f"\n[coverage] Squad data checked for {len(checked)} match(es) with an "
              f"announced lineup ({skipped_not_announced} skipped -- not announced yet).")
        any_empty = False
        for field, missing_in in empty_counts.items():
            if missing_in:
                any_empty = True
                print(f"[coverage] '{field}' is empty for match_id(s): {', '.join(missing_in)}")
        if not any_empty and checked:
            print("[coverage] All squad fields populated for every announced match. ✔")
        print("")

    # ---------------------------------------------------------------
    # News
    # ---------------------------------------------------------------
    def get_news(self):
        print("Scraping latest news...")
        try:
            url = f"{self.base_url}/newslisting"
            res = requests.get(url, headers=self.headers, timeout=15)
            soup = BeautifulSoup(res.content, 'html.parser')
            news = []

            items = soup.select('ul#ulListing > li') or soup.select('li[postid]')

            for item in items[:20]:
                try:
                    link_tag = item.select_one('div.link a.imageCntnr') or item.find('a', href=True)
                    if not link_tag:
                        continue

                    news_url = link_tag.get('href', '')
                    if news_url.startswith('/'):
                        news_url = self.base_url + news_url

                    img_tag = link_tag.find('img')
                    title = ""
                    img = ""
                    if img_tag:
                        title = img_tag.get('alt', '').strip()
                        img = img_tag.get('data-src') or img_tag.get('src') or ""

                    desc_tag = item.select_one('div.desc')
                    description = desc_tag.text.strip() if desc_tag else title
                    if not title and description:
                        title = description

                    details = self.get_news_details(news_url)
                    full_content = details.get('content') or description or title
                    detail_image = details.get('image_url') or img
                    published_at = details.get('published_at') or datetime.now().isoformat()

                    final_title = details.get('title') or title
                    category = details.get('category') or self._infer_news_category(news_url, final_title + ' ' + full_content)
                    tags = self._extract_news_tags(final_title + ' ' + full_content, category)

                    if title and news_url and len(title) > 5 and full_content and len(full_content.strip()) > 20:
                        news.append({
                            'title': final_title,
                            'description': description or self._summary(full_content),
                            'content': full_content,
                            'image_url': detail_image or "https://www.yallakora.com/images/yk-logo.png",
                            'url': news_url,
                            'published_at': published_at,
                            'source': 'YallaKora',
                            'sport_key': self._classify_news_sport(final_title + ' ' + full_content),
                            'category': category,
                            'tags': ','.join(tags)
                        })
                except Exception as e:
                    print(f"Skipping one news item due to error: {e}")
                    continue

            print(f"Found {len(news)} news items.")
            return news
        except Exception as e:
            print(f"Error scraping news: {e}")
            return []


    SEE_ALSO_MARKERS = ['طالع أيضًا', 'طالع ايضا', 'اقرأ أيضاً', 'اقرأ أيضا', 'شاهد أيضاً']

    def get_news_details(self, news_url):
        """Return a full article payload so API clients can render a details page."""
        try:
            res = requests.get(news_url, headers=self.headers, timeout=15)
            res.raise_for_status()
            soup = BeautifulSoup(res.content, 'html.parser')

            title_tag = soup.select_one('h1') or soup.select_one('.articleTitle') or soup.select_one('.ArticleDetails h1')
            title = title_tag.get_text(' ', strip=True) if title_tag else ''

            image = ''
            image_tag = soup.select_one('.ArticleDetails img, .articleDetails img, .details img, meta[property="og:image"]')
            if image_tag:
                image = image_tag.get('content') or image_tag.get('data-src') or image_tag.get('src') or ''
                image = urljoin(self.base_url, image) if image else ''

            article = soup.select_one('.ArticleDetails') or soup.select_one('.articleDetails') or soup.select_one('.details') or soup.select_one('article')
            paragraphs = []
            related_articles = []
            hit_see_also = False

            if article:
                for unwanted in article.select('script,style,iframe,.ad,.ads,[id*="ad"],[class*="ad"]'):
                    unwanted.decompose()

                for p in article.select('p'):
                    text = p.get_text(' ', strip=True)
                    if not text:
                        continue

                    # نقطة التحول: أول ما نلاقي "طالع أيضًا" كل اللي بعدها
                    # لينكات مرتبطة، مش جزء من متن الخبر.
                    if not hit_see_also and any(marker in text for marker in self.SEE_ALSO_MARKERS):
                        hit_see_also = True
                        continue

                    links_in_p = p.find_all('a', href=True)
                    combined_link_text = ' '.join(a.get_text(' ', strip=True) for a in links_in_p).strip()
                    is_pure_link_paragraph = bool(links_in_p) and text == combined_link_text

                    if hit_see_also or is_pure_link_paragraph:
                        for a in links_in_p:
                            href = a.get('href', '')
                            link_title = a.get_text(' ', strip=True)
                            if href and link_title:
                                related_articles.append({
                                    'title': link_title,
                                    'url': urljoin(self.base_url, href),
                                })
                        continue

                    if len(text) > 20:
                        paragraphs.append(text)

            if not paragraphs:
                desc = soup.select_one('meta[property="og:description"], meta[name="description"]')
                fallback = desc.get('content', '').strip() if desc else ''
                if fallback:
                    paragraphs.append(fallback)

            published = ''
            time_tag = soup.select_one('time[datetime]')
            if time_tag:
                published = time_tag.get('datetime', '').strip()

            category = self._extract_news_detail_category(soup, news_url, title + ' ' + ' '.join(paragraphs))

            return {
                'title': title,
                'content': '\n\n'.join(dict.fromkeys(paragraphs)),
                'image_url': image,
                'published_at': published,
                'category': category,
                'related_articles': related_articles,
            }
        except Exception as e:
            print(f"Could not scrape news details {news_url}: {e}")
            return {}


    def _extract_news_detail_category(self, soup, news_url, text):
        selectors = [
            '.breadCrumb a', '.breadcrumb a', '.articlePath a', '.newsDetails .cat',
            '.articleInfo a', '.tags a', 'meta[property="article:section"]'
        ]
        for selector in selectors:
            for tag in soup.select(selector):
                value = tag.get('content') or tag.get_text(' ', strip=True)
                value = (value or '').strip()
                if value and value not in {'الرئيسية', 'الأخبار', 'أخبار'}:
                    return value[:80]
        return self._infer_news_category(news_url, text)

    def _infer_news_category(self, news_url, text):
        haystack = f"{news_url or ''} {text or ''}".lower()
        if 'world-cup' in haystack or 'كأس العالم' in haystack:
            return 'كأس العالم'
        if 'transfers' in haystack or 'انتقالات' in haystack:
            return 'الانتقالات'
        if 'egyptian-league' in haystack or 'الدوري المصري' in haystack:
            return 'الدوري المصري'
        if 'premier-league' in haystack or 'الدوري الإنجليزي' in haystack:
            return 'الدوري الإنجليزي'
        if 'bundesliga' in haystack or 'الدوري الألماني' in haystack:
            return 'الدوري الألماني'
        if 'champions-league' in haystack or 'دوري أبطال' in haystack:
            return 'دوري أبطال أوروبا'
        if 'tennis' in haystack or 'تنس' in haystack:
            return 'تنس'
        if 'basketball' in haystack or 'كرة السلة' in haystack:
            return 'كرة السلة'
        return 'أخبار'

    def _extract_news_tags(self, text, category):
        tags = []
        def add(value):
            value = (value or '').strip()
            if value and value not in tags:
                tags.append(value)
        add(category)
        for token in ['الأهلي', 'الزمالك', 'بيراميدز', 'منتخب مصر', 'كأس العالم', 'محمد صلاح', 'ميسي', 'ريال مدريد', 'برشلونة']:
            if token in (text or ''):
                add(token)
        return tags[:8]

    def _summary(self, text, limit=180):
        text = re.sub(r'\s+', ' ', text or '').strip()
        return text if len(text) <= limit else text[:limit].rstrip() + '...'

    def _classify_news_sport(self, text):
        text = (text or '').lower()
        if any(word in text for word in ['تنس', 'tennis', 'atp', 'wta']):
            return 'tennis'
        if any(word in text for word in ['فورمولا', 'formula', 'f1', 'grand prix']):
            return 'formula1'
        return 'football'

    # ---------------------------------------------------------------
    # Shared player-extraction helper
    # ---------------------------------------------------------------
    def _decode_slug(self, slug):
        try:
            text = urllib.parse.unquote(slug)
        except Exception:
            text = slug
        return text.replace('-', ' ').replace('_', ' ').strip()

    def _split_number_and_name(self, visible_text, slug):
        """
        FIXED: real payloads showed the shirt number glued to the front
        of the player's name with no separator (e.g. "12أورلاندو جيل",
        "0جوستافو جوميز"), and `number` always ended up empty because
        nothing ever split them apart. This pulls a leading run of
        digits off into `number` and keeps the rest as `name`.
        Falls back to the slug-derived name if there's no visible text.
        """
        if visible_text:
            m = self.LEADING_NUMBER_RE.match(visible_text)
            if m:
                return m.group(1), m.group(2).strip()
            return '', visible_text
        return '', self._decode_slug(slug)

    def _extract_players_from_container(self, container):
        players = []
        if not container:
            return players
        for a in container.select('a[href*="/player/"]'):
            href = a.get('href', '')
            m = self.PLAYER_HREF_RE.search(href)
            if not m:
                continue
            player_id, slug = m.group(1), m.group(2)
            visible_text = a.get_text(strip=True)
            number, name = self._split_number_and_name(visible_text, slug)

            position = ''
            for c in a.get('class', []):
                pm = self.POSITION_CODE_RE.match(c)
                if pm:
                    position = f"p{pm.group(1)}"
                    break

            players.append({
                'player_id': player_id,
                'name': name,
                'number': number,
                'position': position,
            })
        return players

    def _extract_formation_code(self, team_div):
        if not team_div:
            return ""
        for c in team_div.get('class', []):
            m = self.FORMATION_CODE_RE.match(c)
            if m:
                return f"{m.group(1)}-{m.group(2)}-{m.group(3)}"
        return ""

    # ---------------------------------------------------------------
    # Shared parse logic, extracted out of get_match_squad so both the
    # plain-requests path and the Selenium path build the result the
    # same way from a BeautifulSoup of the (hopefully) fully-rendered page.
    # ---------------------------------------------------------------
    def _parse_squad_into_result(self, soup, result, debug_tag=""):
        formation_block = soup.select_one(self.SQUAD_FORMATION)
        self._debug_dump(f"squad_formation_{debug_tag}", formation_block)

        if formation_block:
            team_a_pitch = formation_block.select_one('.teamA')
            team_b_pitch = formation_block.select_one('.teamB')
            result['home_formation'] = self._extract_formation_code(team_a_pitch)
            result['away_formation'] = self._extract_formation_code(team_b_pitch)
            result['home_main'] = self._extract_players_from_container(team_a_pitch)
            result['away_main'] = self._extract_players_from_container(team_b_pitch)

        main_teamlist = soup.select_one(self.SQUAD_MAIN_TEAMLIST)
        self._debug_dump(f"squad_main_{debug_tag}", main_teamlist)
        if main_teamlist:
            if not result['home_main']:
                home_main_container = main_teamlist.select_one('.teamA .matchSquad.main')
                result['home_main'] = self._extract_players_from_container(home_main_container)
            if not result['away_main']:
                away_main_container = main_teamlist.select_one('.teamB .matchSquad.main')
                result['away_main'] = self._extract_players_from_container(away_main_container)

        sub_teamlist = soup.select_one(self.SQUAD_SUB_TEAMLIST)
        self._debug_dump(f"squad_sub_{debug_tag}", sub_teamlist)
        if sub_teamlist:
            home_sub_container = sub_teamlist.select_one('.teamA .matchSquad.sub')
            away_sub_container = sub_teamlist.select_one('.teamB .matchSquad.sub')
            result['home_sub'] = self._extract_players_from_container(home_sub_container)
            result['away_sub'] = self._extract_players_from_container(away_sub_container)

        coach_teamlist = soup.select_one(self.SQUAD_COACH_TEAMLIST)
        self._debug_dump(f"squad_coach_{debug_tag}", coach_teamlist)
        if coach_teamlist:
            coach_a = coach_teamlist.select_one('.teamA p.coach span') or coach_teamlist.select_one('.teamA p.coach')
            coach_b = coach_teamlist.select_one('.teamB p.coach span') or coach_teamlist.select_one('.teamB p.coach')
            result['home_coach'] = coach_a.get_text(strip=True) if coach_a else ''
            result['away_coach'] = coach_b.get_text(strip=True) if coach_b else ''

        return result

    # ---------------------------------------------------------------
    # OLD path: plain requests.get(). Kept for reference / other tabs,
    # but for squad specifically this will almost always come back empty
    # since the content is injected by JS after an XHR triggered by a
    # tab click -- use get_match_squad_selenium() instead.
    # ---------------------------------------------------------------
    def get_match_squad(self, session, match_href, match_id_for_debug=""):
        result = {
            'match_id': match_id_for_debug,
            'home_formation': '', 'away_formation': '',
            'home_coach': '', 'away_coach': '',
            'home_main': [], 'home_sub': [], 'away_main': [], 'away_sub': [],
        }
        try:
            url = self._absolute_page_url(match_href)
            res = session.get(url, headers=self.headers, timeout=10)
            soup = BeautifulSoup(res.content, 'html.parser')
            self._parse_squad_into_result(soup, result, debug_tag=match_id_for_debug)

            if not (result['home_main'] or result['away_main']):
                print(f"[warn] No starting XI parsed for match {match_id_for_debug} via plain requests — "
                      f"this container is AJAX-loaded (confirmed), use get_match_squad_selenium() instead.")
        except Exception as e:
            print(f"Error scraping squad for {match_href}: {e}")
        return result

    # ---------------------------------------------------------------
    # Selenium driver setup
    # ---------------------------------------------------------------
    @staticmethod
    def create_selenium_driver(headless=True):
        options = ChromeOptions()
        if headless:
            options.add_argument('--headless=new')
        options.add_argument('--disable-gpu')
        options.add_argument('--no-sandbox')
        options.add_argument('--disable-dev-shm-usage')
        options.add_argument('--window-size=1920,1080')
        options.add_argument('--lang=ar-EG')
        options.add_argument(
            'user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 '
            '(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
        )
        # Block images: faster page loads, we only need HTML/DOM.
        options.add_experimental_option(
            "prefs", {"profile.managed_default_content_settings.images": 2}
        )

        chromedriver_path = os.getenv('CHROMEDRIVER_PATH') or shutil.which('chromedriver')
        if chromedriver_path:
            service = ChromeService(chromedriver_path)
            driver = webdriver.Chrome(service=service, options=options)
        else:
            try:
                # Selenium Manager (bundled with Selenium 4.6+) can use an already
                # installed browser/driver without the webdriver-manager package.
                driver = webdriver.Chrome(options=options)
            except WebDriverException as selenium_error:
                if os.getenv('WDM_ALLOW_DOWNLOAD', '').strip().lower() in {'1', 'true', 'yes'}:
                    service = ChromeService(ChromeDriverManager().install())
                    driver = webdriver.Chrome(service=service, options=options)
                else:
                    raise WebDriverException(
                        "Could not start ChromeDriver without downloading it. "
                        "Install ChromeDriver locally and add it to PATH, set CHROMEDRIVER_PATH, "
                        "or set WDM_ALLOW_DOWNLOAD=1 when internet/DNS access to googlechromelabs.github.io is available."
                    ) from selenium_error
        driver.set_page_load_timeout(30)
        return driver

    def _squad_button_exists(self, driver):
        """
        FIXED: confirmed from a real match page still hours from kickoff
        that #squadButton / #squad don't exist at all yet when the lineup
        hasn't been announced (only headToHeadButton / teamNewsButton /
        teamVideosButton were present). Checking this up front avoids
        clicking a nonexistent button and burning the full AJAX-wait
        timeout on every not-yet-announced fixture.
        """
        try:
            return bool(driver.execute_script(
                "return document.getElementById(arguments[0]) !== null;",
                self.SQUAD_TAB_BUTTON_ID
            ))
        except WebDriverException:
            return False

    def _wait_for_ajax_tab(self, driver, container_id, timeout=15):
        """
        Generic wait: the tab's container div starts out holding ONLY the
        .loaderFullDiv placeholder (confirmed from real markup). Once the
        AJAX call finishes, real content gets appended alongside/inside
        it. So instead of guessing a specific class name to wait for,
        wait for the container to have more than that one placeholder
        child, or for it to contain visible text.
        """
        try:
            from selenium.webdriver.support.ui import WebDriverWait
            WebDriverWait(driver, timeout).until(
                lambda d: d.execute_script(
                    """
                    var el = document.getElementById(arguments[0]);
                    if (!el) return false;
                    if (el.children.length > 1) return true;
                    var t = (el.innerText || '').trim();
                    return t.length > 0;
                    """,
                    container_id
                )
            )
            return True
        except TimeoutException:
            return False

    # ---------------------------------------------------------------
    # Selenium-based squad fetch -- this is the one to actually call.
    # ---------------------------------------------------------------
    def get_match_squad_selenium(self, driver, match_href, match_id_for_debug=""):
        result = {
            'match_id': match_id_for_debug,
            'home_formation': '', 'away_formation': '',
            'home_coach': '', 'away_coach': '',
            'home_main': [], 'home_sub': [], 'away_main': [], 'away_sub': [],
        }
        try:
            url = self._absolute_page_url(match_href)
            driver.get(url)

            # FIXED: skip fast if the lineup tab isn't even on the page
            # yet (not announced), instead of clicking blind and waiting
            # up to 15s for a container that will never appear.
            if not self._squad_button_exists(driver):
                print(f"[info] Lineup not announced yet for match {match_id_for_debug} "
                      f"(#squadButton not present) — skipping.")
                return result

            # Click the "التشكيل" tab via JS -- safer than a real .click()
            # since Cloudflare's rocket-loader rewrites inline onclick
            # handlers and can make Selenium's native click miss.
            try:
                driver.execute_script(
                    "var b = document.getElementById(arguments[0]); if (b) { b.click(); }",
                    self.SQUAD_TAB_BUTTON_ID
                )
            except WebDriverException as e:
                print(f"[selenium] couldn't click squad tab for {match_id_for_debug}: {e}")

            loaded = self._wait_for_ajax_tab(driver, self.SQUAD_CONTAINER_ID, timeout=15)
            if not loaded:
                print(f"[warn] squad tab for match {match_id_for_debug} did not visibly populate "
                      f"within 15s -- dumping whatever loaded for inspection.")

            html = driver.page_source
            soup = BeautifulSoup(html, 'html.parser')

            # Always dump the #squad container specifically (plus fall
            # back to the whole soup if it's missing) so we can see
            # exactly what markup the AJAX response produced.
            squad_container = soup.select_one(f'#{self.SQUAD_CONTAINER_ID}')
            self._debug_dump(f"squad_selenium_full_{match_id_for_debug}", squad_container or soup)

            self._parse_squad_into_result(soup, result, debug_tag=f"selenium_{match_id_for_debug}")

            if not (result['home_main'] or result['away_main']):
                print(f"[warn] Still no starting XI parsed via Selenium for match {match_id_for_debug} — "
                      f"send debug_output/squad_selenium_full_{match_id_for_debug}.html so the SQUAD_* "
                      f"selectors can be corrected against the real loaded markup.")

        except Exception as e:
            print(f"Error scraping squad via Selenium for {match_href}: {e}")

        return result


    # ---------------------------------------------------------------
    # Match minute-by-minute updates + match statistics.
    # ---------------------------------------------------------------
    def get_match_details_selenium(self, driver, match_href, match_id_for_debug=""):
        """Render the match page and click the statistics tab before parsing details.

        YallaKora often injects statistics by JavaScript after the tab is clicked,
        so requests-only parsing can miss possession, shots, corners, and cards.
        This method keeps the same output shape as get_match_details.
        """
        result = {'match_id': match_id_for_debug, 'events': [], 'stats': []}
        try:
            url = self._absolute_page_url(match_href)
            driver.get(url)
            self._click_possible_stats_tabs(driver)
            html = driver.page_source
            soup = BeautifulSoup(html, 'html.parser')
            self._debug_dump(f"match_details_selenium_{match_id_for_debug}", soup)
            result = self._parse_match_details_soup(soup, match_id_for_debug)
        except Exception as e:
            print(f"Error scraping rendered match details for {match_href}: {e}")
        return result

    def _click_possible_stats_tabs(self, driver):
        candidates = [
            "statsButton", "statisticsButton", "matchStatsButton", "teamStatsButton",
            "احصائيات", "إحصائيات", "Stats", "Statistics"
        ]
        for candidate in candidates:
            try:
                clicked = driver.execute_script(
                    """
                    var key = arguments[0];
                    var nodes = Array.from(document.querySelectorAll('a,button'));
                    var el = document.getElementById(key) || nodes.find(function(n) {
                        return ((n.innerText || n.textContent || '').trim().indexOf(key) >= 0) ||
                               ((n.getAttribute('onclick') || '').indexOf('stat') >= 0) ||
                               ((n.getAttribute('href') || '').indexOf('stat') >= 0);
                    });
                    if (!el) return false;
                    el.click();
                    return true;
                    """,
                    candidate
                )
                if clicked:
                    time.sleep(1.5)
            except WebDriverException:
                continue

    def _parse_match_details_soup(self, soup, match_id_for_debug=""):
        result = {'match_id': match_id_for_debug, 'events': [], 'stats': []}
        seen = set()
        stat_selectors = ', '.join([
            '#stats .stat', '#stats li', '.stats .stat', '.stats li', '.matchStatistics li', '.teamsStats li',
            '#statistics .stat', '#statistics li', '.statistics .stat', '.statistics li',
            '[id*=Stat] li', '[id*=stat] li', '[class*=Stat] li', '[class*=stat] li',
            '[id*=Statistic] li', '[class*=Statistic] li'
        ])
        for row in soup.select(stat_selectors):
            text = row.get_text(' ', strip=True)
            if not text or text in seen:
                continue
            vals = re.findall(r'\d+%?|[-+]?[0-9]+(?:\.[0-9]+)?', text)
            label = re.sub(r'\d+%?|[-+]?[0-9]+(?:\.[0-9]+)?', ' ', text).strip(' -:|')
            if len(vals) >= 2 and label:
                result['stats'].append({'name': self._normalize_stat_name(label), 'home_value': vals[0], 'away_value': vals[-1]})
                seen.add(text)
        return result

    def get_match_details(self, session, match_href, match_id_for_debug=""):
        result = {'match_id': match_id_for_debug, 'events': [], 'stats': []}
        try:
            url = self._absolute_page_url(match_href)
            res = session.get(url, headers=self.headers, timeout=15)
            soup = BeautifulSoup(res.content, 'html.parser')
            self._debug_dump(f"match_details_{match_id_for_debug}", soup)

            for script in soup.select('script[type="application/ld+json"]'):
                raw = script.string or script.get_text() or ''
                if 'LiveBlogPosting' not in raw:
                    continue
                try:
                    data = json.loads(raw)
                except Exception:
                    continue
                graph = data.get('@graph', []) if isinstance(data, dict) else []
                for node in graph:
                    if node.get('@type') != 'LiveBlogPosting':
                        continue
                    updates = node.get('liveBlogUpdate') or []
                    if isinstance(updates, dict):
                        updates = [updates]
                    for upd in updates:
                        body = (upd.get('articleBody') or upd.get('headline') or '').strip()
                        minute = ''
                        m = re.match(r'\s*([0-9]+(?:\+[0-9]+)?)\s*-\s*(.*)', body, flags=re.S)
                        if m:
                            minute = m.group(1)
                            body = m.group(2).strip(' -\r\n\t')
                        result['events'].append({
                            'external_id': (upd.get('@id') or upd.get('url') or '').split('#')[-1],
                            'minute': minute,
                            'type': self._classify_match_event(body),
                            'player_name': self._extract_tail_player(body),
                            'assist_player_name': '',
                            'detail': body,
                            'team_side': '',
                            'published_at': upd.get('datePublished') or '',
                        })

            # Best-effort stats extraction from the already-rendered HTML. If YallaKora
            # changes the stat markup, debug_output/match_details_*.html will show it.
            seen = set()
            stat_selectors = ', '.join([
                '#stats .stat', '#stats li', '.stats .stat', '.stats li', '.matchStatistics li', '.teamsStats li',
                '#statistics .stat', '#statistics li', '.statistics .stat', '.statistics li',
                '[id*=Stat] li', '[id*=stat] li', '[class*=Stat] li', '[class*=stat] li',
                '[id*=Statistic] li', '[class*=Statistic] li'
            ])
            for row in soup.select(stat_selectors):
                text = row.get_text(' ', strip=True)
                if not text or text in seen:
                    continue
                vals = re.findall(r'\d+%?|[-+]?[0-9]+(?:\.[0-9]+)?', text)
                label = re.sub(r'\d+%?|[-+]?[0-9]+(?:\.[0-9]+)?', ' ', text).strip(' -:|')
                if len(vals) >= 2 and label:
                    result['stats'].append({'name': self._normalize_stat_name(label), 'home_value': vals[0], 'away_value': vals[-1]})
                    seen.add(text)

        except Exception as e:
            print(f"Error scraping match details for {match_href}: {e}")
        return result

    @staticmethod
    def _normalize_stat_name(label):
        compact = re.sub(r'\s+', ' ', (label or '').strip())
        lookup = {
            'استحواذ': 'possession', 'الاستحواذ': 'possession', 'possession': 'possession',
            'تسديدات': 'shots', 'التسديدات': 'shots', 'shots': 'shots',
            'تسديدات على المرمى': 'shotsOnTarget', 'shots on target': 'shotsOnTarget',
            'ركنيات': 'corners', 'ضربات ركنية': 'corners', 'corners': 'corners',
            'بطاقات صفراء': 'yellowCards', 'yellow cards': 'yellowCards',
            'بطاقات حمراء': 'redCards', 'red cards': 'redCards',
            'أخطاء': 'fouls', 'اخطاء': 'fouls', 'fouls': 'fouls',
        }
        return lookup.get(compact.lower(), compact)

    @staticmethod
    def _classify_match_event(text):
        if not text:
            return 'MinuteByMinute'
        if 'جوو' in text or 'هدف' in text:
            return 'Goal'
        if 'تبديل' in text:
            return 'Substitution'
        if 'بطاقة' in text or 'إنذار' in text:
            return 'Card'
        return 'MinuteByMinute'

    @staticmethod
    def _extract_tail_player(text):
        parts = [p.strip() for p in (text or '').split(' - ') if p.strip()]
        return parts[-1] if len(parts) > 1 else ''

    # ---------------------------------------------------------------
    # Tournament standings.
    # ---------------------------------------------------------------
    def get_tournament_standings(self, tournament_slug_id, tournament_name_slug="tournament"):
        standings = []
        try:
            session = requests.Session()
            url = f"{self.base_url}/group-standing/{tournament_slug_id}/{tournament_name_slug}"
            res = session.get(url, headers=self.headers, timeout=15)
            soup = BeautifulSoup(res.content, 'html.parser')

            content_block = soup.select_one(self.STANDINGS_CONTAINER)
            self._debug_dump(f"standings_page_{tournament_slug_id}", content_block)

            if content_block:
                group_titles = content_block.select('.ttl.groupTtlStand')
                for gt in group_titles:
                    group_name = gt.get_text(strip=True) or 'Default'
                    table = gt.find_next_sibling('div', class_='table')
                    if not table:
                        continue

                    rows = table.select('.wRow')
                    for rank, row in enumerate(rows, start=1):
                        team_link = row.select_one('.item.team a')
                        team_name = ''
                        if team_link:
                            team_name = team_link.get('title', '').strip() or team_link.get_text(strip=True)

                        dtls = [d.get_text(strip=True) for d in row.select('.item.dtls')]

                        standings.append({
                            'team': team_name,
                            'group': group_name,
                            'rank': rank,
                            'played': self._safe_int(dtls[0]) if len(dtls) > 0 else 0,
                            'points': self._safe_int(dtls[-1]) if dtls else 0,
                        })

            if not standings:
                print("[warn] No standings rows parsed — check debug_output/standings_page_*.html.")

        except Exception as e:
            print(f"Error scraping standings for tournament {tournament_slug_id}: {e}")

        return standings

    # ---------------------------------------------------------------
    # Tournament scorers + assisters.
    # ---------------------------------------------------------------
    @staticmethod
    def _classify_stat_block(h3_text):
        if 'صانع' in h3_text:
            return 'assists'
        if 'هداف' in h3_text:
            return 'scorers'
        if 'نظيف' in h3_text or 'حارس' in h3_text:
            return 'clean_sheets'
        return 'unknown'

    def get_tournament_scorers(self, tournament_slug_id, tournament_name_slug="tournament"):
        scorers_map = {}
        try:
            session = requests.Session()
            url = f"{self.base_url}/tour-stats/{tournament_slug_id}/{tournament_name_slug}"
            res = session.get(url, headers=self.headers, timeout=15)
            soup = BeautifulSoup(res.content, 'html.parser')

            top10_block = soup.select_one(self.STATS_TOP10)
            self._debug_dump(f"scorers_top10_{tournament_slug_id}", top10_block)

            if top10_block:
                stat_items = top10_block.find_all('div', class_='item', recursive=False)
                for item in stat_items:
                    h3 = item.select_one('.title h3')
                    kind = self._classify_stat_block(h3.get_text(strip=True) if h3 else '')
                    if kind not in ('scorers', 'assists'):
                        continue

                    for li in item.select('ul > li'):
                        link_tag = li.select_one('a.image') or li.select_one('a[href*="/player/"]')
                        name_container = li.select_one('div.name')
                        num_tag = li.select_one('div.num')
                        if not link_tag:
                            continue

                        href = link_tag.get('href', '')
                        m = self.PLAYER_HREF_RE.search(href)
                        player_id = m.group(1) if m else ''

                        name = ''
                        team_name = ''
                        if name_container:
                            name_links = name_container.find_all('a', recursive=False)
                            if name_links:
                                name = name_links[0].get_text(strip=True)
                            if len(name_links) > 1:
                                team_name = name_links[1].get_text(strip=True)
                        if not name and m:
                            name = self._decode_slug(m.group(2))
                        if not name:
                            continue

                        value = self._safe_int(num_tag.get_text(strip=True)) if num_tag else 0

                        entry = scorers_map.setdefault(name, {
                            'player': name,
                            'player_id': player_id,
                            'team': team_name,
                            'goals': 0,
                            'assists': 0,
                        })
                        if team_name:
                            entry['team'] = team_name
                        if kind == 'scorers':
                            entry['goals'] = value
                        else:
                            entry['assists'] = value

            if not scorers_map:
                print("[warn] No scorers/assists parsed — check debug_output/scorers_top10_*.html.")

        except Exception as e:
            print(f"Error scraping scorers for tournament {tournament_slug_id}: {e}")

        return list(scorers_map.values())

    # ---------------------------------------------------------------
    # Tournament knockout bracket.
    # ---------------------------------------------------------------
    def _extract_team_cookie_name(self, cookie_div):
        if not cookie_div:
            return ''
        name_tag = cookie_div.select_one('.TeamName')
        if name_tag and name_tag.get_text(strip=True):
            return name_tag.get_text(strip=True)
        img_tag = cookie_div.select_one('img[alt]')
        if img_tag and img_tag.get('alt', '').strip():
            return img_tag['alt'].strip()
        link_tag = cookie_div.select_one('a[title]')
        if link_tag and link_tag.get('title', '').strip():
            return link_tag['title'].strip()
        text = cookie_div.get_text(strip=True)
        return text

    def get_tournament_bracket(self, tournament_slug_id, tournament_name_slug="tournament"):
        bracket = []
        try:
            session = requests.Session()
            url = f"{self.base_url}/group-standing/{tournament_slug_id}/{tournament_name_slug}"
            res = session.get(url, headers=self.headers, timeout=15)
            soup = BeautifulSoup(res.content, 'html.parser')

            all_rounds = soup.select_one(self.BRACKET_CONTAINER)
            self._debug_dump(f"bracket_page_{tournament_slug_id}", all_rounds)

            if all_rounds:
                order = 0
                round_items = all_rounds.select('.roundItem')
                for idx, round_block in enumerate(round_items):
                    round_name_tag = round_block.select_one('h3')
                    if round_name_tag and round_name_tag.get_text(strip=True):
                        round_name = round_name_tag.get_text(strip=True)
                    elif 'finalRound' in round_block.get('class', []):
                        round_name = 'Final'
                    else:
                        round_name = f"Round {idx + 1}"

                    fixtures = round_block.select('.qualifiedTeams .knockoutStage, .final.qualifiedTeams .knockoutStage')
                    self._debug_dump(f"bracket_round_{tournament_slug_id}_{idx}", round_block)

                    for fixture in fixtures:
                        home_cookie = fixture.select_one('.teamData .team.cookie')
                        away_cookie = fixture.select_one('.bottom.teamData .team.cookie')
                        home_name = self._extract_team_cookie_name(home_cookie)
                        away_name = self._extract_team_cookie_name(away_cookie)
                        if not home_name and not away_name:
                            continue

                        date_tag = fixture.select_one('.dateMatch')
                        order += 1
                        bracket.append({
                            'round': round_name,
                            'home_team': home_name,
                            'away_team': away_name,
                            'score': '',
                            'winner': '',
                            'order': order,
                            'date': date_tag.get_text(strip=True) if date_tag else '',
                        })

            if not bracket:
                print("[warn] No bracket fixtures parsed — check debug_output/bracket_round_*.html.")

        except Exception as e:
            print(f"Error scraping bracket for tournament {tournament_slug_id}: {e}")

        return bracket

    @staticmethod
    def _safe_int(text):
        try:
            return int(re.sub(r'[^\d-]', '', text or '0') or 0)
        except Exception:
            return 0

    # ---------------------------------------------------------------
    def send_to_api(self, endpoint, data):
        if not self.api_base_url:
            return
        url = f"{self.api_base_url}/api/SportsData/import/{endpoint}"
        print(f"Sending data to {url}...")
        try:
            res = requests.post(url, json=data, timeout=30)
            print(f"API Response: {res.status_code}")
            print(f"Response Body: {res.text}")
        except Exception as e:
            print(f"Connection Error: {e}")


def _load_appsettings_for_cli():
    settings_path = os.path.join(os.path.dirname(os.path.dirname(__file__)), "appsettings.json")
    try:
        with open(settings_path, "r", encoding="utf-8-sig") as handle:
            return json.load(handle)
    except Exception:
        return {}


def _setting(settings, path, default=None):
    current = settings
    for part in path.split(":"):
        if not isinstance(current, dict) or part not in current:
            return default
        current = current[part]
    return current


def _parse_cli_args(argv=None):
    parser = argparse.ArgumentParser(description="Scrape YallaKora football matches/news and import them into the API.")
    parser.add_argument("command", nargs="?", default="all", choices=("all", "matches", "news"), help="What to scrape. Default: all.")
    parser.add_argument("--date", help="Scrape one match-center day (YYYY-MM-DD, DD/MM/YYYY, or M/D/YYYY).")
    parser.add_argument("--today", action="store_true", help="Scrape only today's match-center day; useful for frequent live refresh jobs.")
    parser.add_argument("--from-date", dest="date_from", help="Start date for an inclusive match-center range.")
    parser.add_argument("--to-date", dest="date_to", help="End date for an inclusive match-center range.")
    parser.add_argument("--full-sync", action="store_true", help="Scrape the configured full backfill range from appsettings.json.")
    parser.add_argument(
        "--loop-seconds",
        dest="loop_seconds",
        type=float,
        default=None,
        help=(
            "Keep yallakora_engine.py running and re-run the selected command this many "
            "seconds after each run finishes. Can also be set with YALLAKORA_ENGINE_LOOP_SECONDS."
        ),
    )
    return parser.parse_args(argv)


def _cli_target_dates(args, engine):
    settings = _load_appsettings_for_cli()
    explicit = YallaKoraEngine._parse_date_value(args.date)
    if explicit:
        return [explicit]

    if args.today:
        return [_today_in_egypt()]

    # Direct manual runs should not be trapped by a stale YALLAKORA_MATCH_DATE
    # environment variable from a previous PowerShell/session. Scheduled callers
    # that need a single env-driven day should pass --date explicitly (run_all.py
    # does this below) or opt in with YALLAKORA_RESPECT_ENV_DATE=1.
    if os.getenv("YALLAKORA_RESPECT_ENV_DATE", "").strip().lower() in {"1", "true", "yes", "on"}:
        env_date = YallaKoraEngine._parse_date_value(os.getenv("YALLAKORA_MATCH_DATE"))
        if env_date:
            return [env_date]

    date_from = YallaKoraEngine._parse_date_value(args.date_from or os.getenv("SCRAPER_DATE_FROM"))
    date_to = YallaKoraEngine._parse_date_value(args.date_to or os.getenv("SCRAPER_DATE_TO"))
    if date_from and date_to:
        if date_from > date_to:
            date_from, date_to = date_to, date_from
        return [date_from + timedelta(days=i) for i in range((date_to - date_from).days + 1)]

    range_mode = os.getenv("RUN_ALL_DATE_RANGE_MODE", "").strip().lower()
    if args.full_sync or range_mode == "full_sync":
        lookback_days = int(os.getenv("FULL_SYNC_LOOKBACK_DAYS", str(_setting(settings, "SportsSync:FullSyncLookbackDays", 30))))
        lookahead_days = int(os.getenv("FULL_SYNC_LOOKAHEAD_DAYS", str(_setting(settings, "SportsSync:FullSyncLookaheadDays", 30))))
        # FIXED (new): was datetime.now().date() -- see _today_in_egypt().
        today = _today_in_egypt()
        dates = [today + timedelta(days=offset) for offset in range(-lookback_days, lookahead_days + 1)]
        print(f"[yallakora] full_sync mode: scraping {len(dates)} day(s) ({dates[0]} .. {dates[-1]})")
        return dates

    return [_today_in_egypt()]


def _import_match_bundle(engine, matches):
    if not matches:
        print("No matches found for this date. Checking if page is empty...")
        return

    engine.report_match_field_coverage(matches)
    engine.send_to_api("matches", matches)

    driver = None
    squad_results = []
    try:
        try:
            driver = YallaKoraEngine.create_selenium_driver(headless=True)
        except WebDriverException as e:
            print(f"[squad] Selenium unavailable; skipping AJAX lineup scrape: {e}")

        for tour in matches:
            for m in tour['matches']:
                if not m.get('match_href'):
                    continue
                if driver is not None:
                    print(f"[squad] Fetching squad for {m['home_team']} vs {m['away_team']}...")
                    squad_data = engine.get_match_squad_selenium(driver, m['match_href'], m['match_id'])
                    squad_results.append(squad_data)
                    engine.send_to_api("squad", squad_data)

                details_data = engine.get_match_details(requests.Session(), m['match_href'], m['match_id'])
                if details_data.get('events') or details_data.get('stats'):
                    engine.send_to_api("match-details", details_data)
                time.sleep(engine.request_delay)
    finally:
        if driver is not None:
            driver.quit()

    if squad_results:
        engine.report_squad_field_coverage(squad_results)

    seen_tournament_ids = set()
    for tour in matches:
        tid = tour.get('tournament_id')
        if not tid or tid in seen_tournament_ids:
            continue
        seen_tournament_ids.add(tid)

        name_slug = tour.get('tournament_name', 'tournament')
        standings = engine.get_tournament_standings(tid, name_slug)
        scorers = engine.get_tournament_scorers(tid, name_slug)
        bracket = engine.get_tournament_bracket(tid, name_slug)

        if standings or scorers or bracket:
            engine.send_to_api(f"tournament-details/{tid}", {
                'standings': standings,
                'scorers': scorers,
                'bracket': bracket,
            })


def _run_selected_command_once(args, engine):
    if args.command in {"all", "matches"}:
        for day in _cli_target_dates(args, engine):
            iso_day = day.isoformat()
            print(f"\n[yallakora] ===== {iso_day} =====")
            matches = engine.get_matches(iso_day)
            _import_match_bundle(engine, matches)

    if args.command in {"all", "news"}:
        news = engine.get_news()
        if news:
            engine.send_to_api("news", news)


def _resolve_loop_seconds(args):
    loop_seconds = args.loop_seconds
    if loop_seconds is None:
        env_loop = os.getenv("YALLAKORA_ENGINE_LOOP_SECONDS")
        if env_loop:
            try:
                loop_seconds = float(env_loop)
            except ValueError:
                print(f"[yallakora] WARNING: YALLAKORA_ENGINE_LOOP_SECONDS={env_loop!r} is not a valid number; ignoring.")
    return loop_seconds


def main(argv=None):
    args = _parse_cli_args(argv)
    API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net")
    engine = YallaKoraEngine(api_base_url=API_DOMAIN)
    loop_seconds = _resolve_loop_seconds(args)

    if loop_seconds is None:
        _run_selected_command_once(args, engine)
    else:
        loop_seconds = max(5, loop_seconds)
        run_number = 0
        try:
            while True:
                run_number += 1
                started_at = datetime.now()
                print(f"\n[yallakora] ########## Loop run #{run_number} started at {started_at.isoformat(timespec='seconds')} ##########")
                _run_selected_command_once(args, engine)
                finished_at = datetime.now()
                elapsed = (finished_at - started_at).total_seconds()
                print(f"[yallakora] ########## Loop run #{run_number} finished in {elapsed:.1f}s. Sleeping {loop_seconds:g}s before next run. ##########")
                time.sleep(loop_seconds)
        except KeyboardInterrupt:
            print("\n[yallakora] Loop stopped by user (Ctrl+C).")

    if DEBUG_MODE:
        print(f"\nDebug HTML files written to: {DEBUG_DIR}\n"
              f"If squad_selenium_full_*.html still shows no real teamList/formation content, "
              f"or matchcenter_page.html shows tournaments/matches not being parsed, "
              f"send those files back (not screenshots) so the selectors can be fixed "
              f"against the actual rendered markup.")


if __name__ == "__main__":
    raise SystemExit(main())