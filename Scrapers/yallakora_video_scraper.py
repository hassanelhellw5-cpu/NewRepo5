import hashlib
import os
import re
import sys
import time
import urllib.parse
import unicodedata
from datetime import UTC, datetime, timedelta

import requests
from bs4 import BeautifulSoup

try:
    from selenium.common.exceptions import TimeoutException, WebDriverException
    from selenium.webdriver.support.ui import WebDriverWait
    from yallakora_engine import YallaKoraEngine
except ImportError:
    TimeoutException = None
    WebDriverException = Exception
    WebDriverWait = None
    YallaKoraEngine = None

API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net")
DEBUG_MODE = os.getenv("VIDEO_SCRAPER_DEBUG", "0") == "1"
DAYS_BACK = int(os.getenv("VIDEO_SCRAPER_DAYS_BACK", "2"))
MAX_VIDEOS_PER_MATCH = int(os.getenv("VIDEO_SCRAPER_MAX_PER_MATCH", "12"))
REQUEST_DELAY = float(os.getenv("VIDEO_SCRAPER_REQUEST_DELAY", "0.5"))


class YallaKoraVideoScraper:
    VIDEO_RE = re.compile(r"/video/(\d+)/([^?#]+)", re.IGNORECASE)
    ARABIC_DIACRITICS_RE = re.compile(r"[\u0617-\u061A\u064B-\u0652]")

    YALLAKORA_SEARCH_PATHS = [
        "/search?q={query}",
        "/search?keyword={query}",
        "/videos?keyword={query}",
        "/video?keyword={query}",
    ]

    SOURCE_CONFIGS = [

        {
            "name": "YouTube",
            "base_url": "https://www.youtube.com",
            "landing_paths": [],
            "search_paths": ["/results?search_query={query}"],
            "href_contains": ["/watch?v=", "/shorts/"],
        },
        {
            "name": "FootyRoom",
            "base_url": "https://footyroom.co",
            "landing_paths": [
                "/",
                "/competitions/5/premier-league",
                "/competitions/21/mls",
                "/competitions/3/la-liga",
                "/competitions/4/serie-a",
                "/competitions/8/champions-league",
                "/competitions/6/bundesliga",
                "/competitions/15/ligue-1",
            ],
            "search_paths": ["/search?q={query}", "/search/{query}"],
            "href_contains": ["/matches/", "/videos/", "/video/"],
        },
        {
            "name": "HooFoot",
            "base_url": "https://hoofoot.com",
            "landing_paths": ["/", "/?Latest", "/?idp=58"],
            "search_paths": ["/?s={query}", "/?q={query}", "/?match={query}"],
            "href_contains": ["?match=", "/?match=", "video", "highlights"],
        },
        {
            "name": "DasFootball",
            "base_url": "https://dasfootball.com",
            "landing_paths": ["/", "/football-highlights/", "/full-match-replay/"],
            "search_paths": ["/?s={query}", "/search?q={query}"],
            "href_contains": ["highlights", "goals", "match", "replay"],
        },
    ]

    TEAM_ALIASES = {
        "امريكا": ["usa", "united states", "usmnt"],
        "الولايات المتحده": ["usa", "united states", "usmnt"],
        "بلجيكا": ["belgium"],
        "انجلترا": ["england"],
        "فرنسا": ["france"],
        "اسبانيا": ["spain"],
        "البرتغال": ["portugal"],
        "المانيا": ["germany"],
        "ايطاليا": ["italy"],
        "البرازيل": ["brazil"],
        "الارجنتين": ["argentina"],
        "هولندا": ["netherlands", "holland"],
        "المغرب": ["morocco"],
        "مصر": ["egypt"],
        "السعوديه": ["saudi arabia", "saudi"],
        "اليابان": ["japan"],
        "كندا": ["canada"],
        "المكسيك": ["mexico"],
        "النرويج": ["norway"],
        "السويد": ["sweden"],
        "سويسرا": ["switzerland"],
        "كرواتيا": ["croatia"],
        "اوروجواي": ["uruguay"],
        "كولومبيا": ["colombia"],
        "الاهلي": ["al ahly", "alahly", "ahly"],
        "الزمالك": ["zamalek"],
        "بيراميدز": ["pyramids"],
        "ريال مدريد": ["real madrid"],
        "برشلونه": ["barcelona", "barca"],
        "مانشستر سيتي": ["manchester city", "man city"],
        "مانشستر يونايتد": ["manchester united", "man united", "man utd"],
        "ليفربول": ["liverpool"],
        "ارسنال": ["arsenal"],
        "تشيلسي": ["chelsea"],
        "توتنهام": ["tottenham", "spurs"],
        "بايرن ميونخ": ["bayern munich", "bayern"],
        "باريس سان جيرمان": ["psg", "paris saint germain"],
        "انتر ميلان": ["inter milan", "inter"],
        "ميلان": ["ac milan", "milan"],
        "يوفنتوس": ["juventus"],
    }

    def __init__(self, api_base_url=API_DOMAIN):
        self.base_url = "https://www.yallakora.com"
        self.api_base_url = api_base_url.rstrip("/")
        self.headers = {
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                          "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "ar,en-US;q=0.9,en;q=0.8",
            "Referer": "https://www.yallakora.com/",
        }
        self.session = requests.Session()

    def get_recent_finished_matches(self):
        matches = []
        seen = set()
        today = datetime.now(UTC).date()
        for target in self._target_dates(today):
            url = f"{self.api_base_url}/api/SportsData/matches?date={target.isoformat()}"
            try:
                res = requests.get(url, timeout=30)
                if res.status_code != 200:
                    print(f"[videos] Failed fetching matches for {target}: {res.status_code} {res.text[:120]}")
                    continue
                day_matches = res.json()
                print(f"[videos] {target}: API returned {len(day_matches)} match(es).")
                for match in day_matches:
                    match_id = str(match.get("matchId") or "")
                    if not match_id or match_id in seen:
                        continue
                    if not self._is_finished_match(match, target, today):
                        continue
                    seen.add(match_id)
                    matches.append(match)
            except Exception as e:
                print(f"[videos] Error fetching matches for {target}: {e}")
        print(f"[videos] Found {len(matches)} finished match(es) to check for videos.")
        if not matches:
            print("[videos] No finished matches found. Set VIDEO_SCRAPER_DATE=YYYY-MM-DD or VIDEO_SCRAPER_DATE_FROM/VIDEO_SCRAPER_DATE_TO to scan an older match day.")
        return matches

    def _target_dates(self, today):
        explicit = os.getenv("VIDEO_SCRAPER_DATE")
        if explicit:
            parsed = self._parse_date(explicit)
            return [parsed] if parsed else []

        date_from = self._parse_date(os.getenv("VIDEO_SCRAPER_DATE_FROM"))
        date_to = self._parse_date(os.getenv("VIDEO_SCRAPER_DATE_TO"))
        if date_from and date_to:
            if date_from > date_to:
                date_from, date_to = date_to, date_from
            days = (date_to - date_from).days
            return [date_from + timedelta(days=offset) for offset in range(days + 1)]

        return [today - timedelta(days=offset) for offset in range(DAYS_BACK + 1)]

    @staticmethod
    def _parse_date(value):
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

    def _is_finished_match(self, match, match_day, today):
        if self._is_finished(match.get("status")):
            return True
        score = match.get("score") or {}
        has_score = bool(str(score.get("home") or "").strip()) and bool(str(score.get("away") or "").strip())
        return has_score and match_day <= today

    @staticmethod
    def _is_finished(status):
        text = (status or "").strip().lower()
        return any(token in text for token in ["انتهت", "انتهى", "finished", "full time", "ft", "final"])

    def scrape_match_videos(self, match):
        home = self._team_name(match.get("homeTeam"))
        away = self._team_name(match.get("awayTeam"))
        match_id = str(match.get("matchId") or "")
        source_url = match.get("sourceUrl") or ""

        candidates = []

        # 1) YallaShoot is a mobile app, not a website. Only call it when a
        #    captured/public app API endpoint is configured explicitly.
        candidates.extend(self._scrape_yallashoot_app(match, home, away))

        # 2) Broad football-highlight sites: public web pages, not app APIs.
        candidates.extend(self._scrape_multi_source_sites(home, away))

        # 3) YallaKora fallback: use the original match page/videos tab + search pages.
        if source_url:
            candidates.extend(self._scrape_match_page_videos(source_url, home, away))
            time.sleep(REQUEST_DELAY)

        for query in self._build_queries(home, away):
            candidates.extend(self._search_yallakora_videos(query, home, away))
            time.sleep(REQUEST_DELAY)

        videos = self._dedupe_and_rank(candidates, home, away)[:MAX_VIDEOS_PER_MATCH]
        print(f"[videos] {home} vs {away} ({match_id}) => {len(videos)} video(s).")
        return {"match_id": match_id, "videos": videos}

    @staticmethod
    def _team_name(team_obj):
        if isinstance(team_obj, dict):
            return team_obj.get("name") or ""
        return str(team_obj or "")

    def _build_queries(self, home, away):
        teams = f"{home} {away}".strip()
        return [
            f"أهداف مباراة {home} و {away}",
            f"ملخص مباراة {home} و {away}",
            f"يلا شوت ملخص {home} {away}",
            f"يلا شوت أهداف {home} {away}",
            f"هدف {home} أمام {away}",
            f"هدف {away} أمام {home}",
            f"ركلات ترجيح {home} {away}",
            teams,
        ]

    def _scrape_match_page_videos(self, source_url, home, away):
        url = self._absolute_url(source_url)
        videos = []
        try:
            res = self.session.get(url, headers=self.headers, timeout=20)
            videos.extend(self._parse_video_links(res.text, home, away, source_page=url))
        except Exception as e:
            print(f"[videos] Requests match page failed for {url}: {e}")

        # If the match page has an AJAX-loaded Videos tab, Selenium can click it and parse the rendered DOM.
        if YallaKoraEngine is not None:
            driver = None
            try:
                driver = YallaKoraEngine.create_selenium_driver(headless=True)
                driver.get(url)
                if self._element_exists(driver, "teamVideosButton"):
                    driver.execute_script("var b=document.getElementById('teamVideosButton'); if (b) b.click();")
                    self._wait_for_container(driver, "teamVideos", timeout=10)
                    videos.extend(self._parse_video_links(driver.page_source, home, away, source_page=url))
            except Exception as e:
                print(f"[videos] Selenium match video tab failed for {url}: {e}")
            finally:
                if driver is not None:
                    driver.quit()
        return videos

    @staticmethod
    def _element_exists(driver, element_id):
        try:
            return bool(driver.execute_script("return document.getElementById(arguments[0]) !== null;", element_id))
        except WebDriverException:
            return False

    @staticmethod
    def _wait_for_container(driver, container_id, timeout=10):
        if WebDriverWait is None:
            return False
        try:
            WebDriverWait(driver, timeout).until(
                lambda d: d.execute_script(
                    """
                    var el = document.getElementById(arguments[0]);
                    if (!el) return false;
                    return (el.innerText || '').trim().length > 0 || el.querySelectorAll('a[href*=video]').length > 0;
                    """,
                    container_id,
                )
            )
            return True
        except TimeoutException:
            return False


    def _scrape_yallashoot_app(self, match, home, away):
        """Import highlights from the YallaShoot mobile app API when configured.

        YallaShoot is not a normal website source. To avoid scraping a fake web
        domain, this integration is deliberately environment-driven: capture the
        app's legitimate/public highlights endpoint, put it in
        YALLASHOOT_APP_API_URL, and use placeholders below for the current match.
        """
        template = os.getenv("YALLASHOOT_APP_API_URL", "").strip()
        if not template:
            return []

        match_id = str(match.get("matchId") or "")
        params = {
            "match_id": urllib.parse.quote(match_id),
            "home": urllib.parse.quote(home),
            "away": urllib.parse.quote(away),
            "query": urllib.parse.quote(f"{home} {away}".strip()),
        }
        try:
            url = template.format(**params)
            headers = self._headers_for(url)
            token = os.getenv("YALLASHOOT_APP_TOKEN", "").strip()
            if token:
                headers["Authorization"] = f"Bearer {token}"
            res = self.session.get(url, headers=headers, timeout=20)
            if res.status_code != 200:
                print(f"[videos] YallaShoot app API returned {res.status_code} for {match_id}")
                return []
            data = res.json()
        except Exception as e:
            print(f"[videos] YallaShoot app API failed for {match_id}: {e}")
            return []

        items = data.get("videos") if isinstance(data, dict) else data
        if not isinstance(items, list):
            return []

        videos = []
        for item in items:
            if not isinstance(item, dict):
                continue
            title = str(item.get("title") or item.get("name") or item.get("caption") or "").strip()
            video_url = str(item.get("video_url") or item.get("videoUrl") or item.get("url") or item.get("link") or "").strip()
            embed_url = str(item.get("embed_url") or item.get("embedUrl") or video_url).strip()
            thumbnail = str(item.get("thumbnail_url") or item.get("thumbnailUrl") or item.get("image") or "").strip()
            if not title or not video_url:
                continue
            video_type = self._classify_video(title)
            score = self._match_score(title, home, away, video_type)
            if score <= 0:
                continue
            external_id = str(item.get("external_id") or item.get("id") or self._external_id("YallaShootApp", video_url))
            videos.append({
                "external_id": f"YallaShootApp:{external_id}" if not external_id.startswith("YallaShootApp:") else external_id,
                "title": title,
                "description": str(item.get("description") or title),
                "video_url": video_url,
                "embed_url": embed_url,
                "thumbnail_url": thumbnail,
                "source": "YallaShootApp",
                "type": video_type,
                "published_at": str(item.get("published_at") or item.get("publishedAt") or ""),
                "_score": score + self._source_priority("YallaShootApp"),
            })
        return videos

    def _scrape_multi_source_sites(self, home, away):
        videos = []
        for config in self.SOURCE_CONFIGS:
            source = config["name"]
            base_url = config["base_url"]
            seen_pages = set()

            for path in config.get("landing_paths", []):
                url = self._absolute_url_for(base_url, path)
                if url in seen_pages:
                    continue
                seen_pages.add(url)
                videos.extend(self._scrape_source_page(url, config, home, away))
                time.sleep(REQUEST_DELAY)

            for query in self._build_english_queries(home, away):
                encoded = urllib.parse.quote(query.replace(" ", "+"))
                for path in config.get("search_paths", []):
                    url = self._absolute_url_for(base_url, path.format(query=encoded))
                    if url in seen_pages:
                        continue
                    seen_pages.add(url)
                    videos.extend(self._scrape_source_page(url, config, home, away))
                    time.sleep(REQUEST_DELAY)

            if videos:
                print(f"[videos] {source}: collected candidate links for {home} vs {away}.")
        return videos

    def _scrape_source_page(self, url, config, home, away):
        try:
            res = self.session.get(url, headers=self._headers_for(url), timeout=20)
            if res.status_code != 200:
                return []
            candidates = self._parse_generic_video_links(
                res.text,
                home,
                away,
                source=config["name"],
                base_url=config["base_url"],
                source_page=url,
                href_contains=config.get("href_contains", []),
            )

            # If a listing page points to a likely match review/highlight page, open it once
            # and parse the embedded/detail page too.
            detail_videos = []
            for item in candidates[:5]:
                detail_url = item.get("video_url")
                if not detail_url or detail_url == url:
                    continue
                try:
                    detail = self.session.get(detail_url, headers=self._headers_for(detail_url), timeout=20)
                    if detail.status_code == 200:
                        detail_videos.extend(self._parse_generic_video_links(
                            detail.text,
                            home,
                            away,
                            source=config["name"],
                            base_url=config["base_url"],
                            source_page=detail_url,
                            href_contains=config.get("href_contains", []),
                        ))
                except Exception as e:
                    if DEBUG_MODE:
                        print(f"[videos] Detail page failed for {detail_url}: {e}")
            return candidates + detail_videos
        except Exception as e:
            if DEBUG_MODE:
                print(f"[videos] Source page failed for {url}: {e}")
            return []

    def _parse_generic_video_links(self, html, home, away, source, base_url, source_page, href_contains):
        soup = BeautifulSoup(html or "", "html.parser")
        videos = []
        for a in soup.select("a[href]"):
            href = a.get("href") or ""
            if not self._looks_like_source_video_link(href, href_contains):
                continue

            title = self._extract_title(a)
            url_title = self._title_from_url(href)
            if not title:
                title = url_title
            if not title:
                continue

            scoring_text = f"{title} {url_title}".strip()
            video_type = self._classify_video(scoring_text)
            score = self._match_score(scoring_text, home, away, video_type)
            if score <= 0:
                continue

            img = a.select_one("img")
            thumbnail = ""
            if img:
                thumbnail = img.get("data-src") or img.get("data-original") or img.get("src") or ""

            video_url = self._absolute_url_for(base_url, href)
            videos.append({
                "external_id": self._external_id(source, video_url),
                "title": title,
                "description": title,
                "video_url": video_url,
                "embed_url": video_url,
                "thumbnail_url": self._absolute_url_for(base_url, thumbnail) if thumbnail else "",
                "source": source,
                "type": video_type,
                "published_at": self._extract_datetime(a, source_page),
                "_score": score + self._source_priority(source),
            })
        return videos

    @staticmethod
    def _looks_like_source_video_link(href, href_contains):
        if not href or href.startswith("#") or href.startswith("javascript:"):
            return False
        lower = href.lower()
        return any(token.lower() in lower for token in href_contains)

    @staticmethod
    def _source_priority(source):
        return {"YallaShootApp": 40, "FootyRoom": 30, "YouTube": 25, "HooFoot": 20, "DasFootball": 10, "YallaKora": 5}.get(source, 0)

    @staticmethod
    def _external_id(source, url):
        digest = hashlib.sha1(url.encode("utf-8")).hexdigest()[:16]
        return f"{source}:{digest}"

    def _build_english_queries(self, home, away):
        home_names = self._team_aliases(home)
        away_names = self._team_aliases(away)
        queries = []
        for h in home_names[:3]:
            for a in away_names[:3]:
                queries.extend([
                    f"{h} vs {a} highlights",
                    f"{h} {a} goals",
                    f"{h} vs {a}",
                ])
        return list(dict.fromkeys(queries))

    def _team_aliases(self, text):
        norm = self._normalize(text)
        aliases = [text, norm]
        aliases.extend(self.TEAM_ALIASES.get(norm, []))
        # Keep token-only fallback for pages that abbreviate titles.
        aliases.extend(self._important_tokens(text))
        return [a for a in dict.fromkeys(a.strip() for a in aliases if a and a.strip())]

    @staticmethod
    def _title_from_url(href):
        path = urllib.parse.urlparse(href).path or href
        slug = path.strip("/").split("/")[-1]
        slug = urllib.parse.unquote(slug).replace("-", "_").replace("_", " ")
        return re.sub(r"\s+", " ", slug).strip()

    def _search_yallakora_videos(self, query, home, away):
        encoded = urllib.parse.quote(query)
        videos = []
        for path in self.YALLAKORA_SEARCH_PATHS:
            url = f"{self.base_url}{path.format(query=encoded)}"
            try:
                res = self.session.get(url, headers=self.headers, timeout=20)
                if res.status_code != 200:
                    continue
                videos.extend(self._parse_video_links(res.text, home, away, source_page=url))
            except Exception as e:
                if DEBUG_MODE:
                    print(f"[videos] Search failed for {url}: {e}")
        return videos

    def _parse_video_links(self, html, home, away, source_page=""):
        soup = BeautifulSoup(html or "", "html.parser")
        videos = []
        for a in soup.select('a[href*="/video/"]'):
            href = a.get("href") or ""
            m = self.VIDEO_RE.search(href)
            if not m:
                continue

            title = self._extract_title(a)
            if not title:
                continue

            video_type = self._classify_video(title)
            score = self._match_score(title, home, away, video_type)
            if score <= 0:
                continue

            img = a.select_one("img")
            thumbnail = ""
            if img:
                thumbnail = img.get("data-src") or img.get("data-original") or img.get("src") or ""

            video_url = self._absolute_url(href)
            videos.append({
                "external_id": m.group(1),
                "title": title,
                "description": title,
                "video_url": video_url,
                "embed_url": video_url,
                "thumbnail_url": self._absolute_url(thumbnail) if thumbnail else "",
                "source": "YallaKora",
                "type": video_type,
                "published_at": self._extract_datetime(a, source_page),
                "_score": score,
            })
        return videos

    @staticmethod
    def _extract_title(anchor):
        candidates = [
            anchor.get("title"),
            anchor.get("aria-label"),
        ]
        img = anchor.select_one("img")
        if img:
            candidates.extend([img.get("alt"), img.get("title")])
        candidates.append(anchor.get_text(" ", strip=True))
        for candidate in candidates:
            if candidate and candidate.strip():
                return re.sub(r"\s+", " ", candidate).strip()
        return ""

    def _classify_video(self, title):
        norm = self._normalize(title)
        if "ركلات ترجيح" in norm or "ضربات ترجيح" in norm or "penalties" in norm or "penalty shootout" in norm:
            return "Penalties"
        if "ملخص" in norm or "highlights" in norm or "review" in norm or "full match" in norm:
            return "Summary"
        if "اهداف مباراة" in norm or "أهداف مباراة" in title or "goals" in norm:
            return "Goals"
        if "هدف" in norm or "goal" in norm:
            return "SingleGoal"
        return "Other"

    def _match_score(self, title, home, away, video_type):
        norm_title = self._normalize(title)
        home_tokens = [self._normalize(t) for t in self._team_aliases(home)]
        away_tokens = [self._normalize(t) for t in self._team_aliases(away)]
        home_hit = any(t and t in norm_title for t in home_tokens)
        away_hit = any(t and t in norm_title for t in away_tokens)

        if video_type == "Other":
            return 0
        if home_hit and away_hit:
            return 100
        if (home_hit or away_hit) and video_type in ["SingleGoal", "Penalties"]:
            return 50
        return 0

    def _important_tokens(self, text):
        stop = {"نادي", "منتخب", "فريق", "اف", "fc", "sc", "و"}
        tokens = [t for t in re.split(r"\s+", self._normalize(text)) if len(t) > 1 and t not in stop]
        return tokens or [self._normalize(text)]

    def _normalize(self, text):
        text = unicodedata.normalize("NFKD", text or "")
        text = "".join(ch for ch in text if not unicodedata.combining(ch))
        text = self.ARABIC_DIACRITICS_RE.sub("", text)
        text = text.replace("أ", "ا").replace("إ", "ا").replace("آ", "ا")
        text = text.replace("ة", "ه").replace("ى", "ي")
        text = re.sub(r"[^\w\s\u0600-\u06FF]", " ", text.lower())
        return re.sub(r"\s+", " ", text).strip()

    @staticmethod
    def _extract_datetime(anchor, source_page):
        container = anchor
        for _ in range(4):
            if container is None:
                break
            time_tag = container.select_one("time[datetime]") if hasattr(container, "select_one") else None
            if time_tag and time_tag.get("datetime"):
                return time_tag["datetime"]
            container = container.parent
        return ""

    def _dedupe_and_rank(self, videos, home, away):
        deduped = {}
        for video in videos:
            key = video.get("external_id") or video.get("video_url")
            if not key:
                continue
            current = deduped.get(key)
            if current is None or video.get("_score", 0) > current.get("_score", 0):
                deduped[key] = video

        ordered = sorted(deduped.values(), key=lambda v: (v.get("_score", 0), self._type_priority(v.get("type"))), reverse=True)
        for video in ordered:
            video.pop("_score", None)
        return ordered

    @staticmethod
    def _type_priority(video_type):
        return {"Goals": 4, "Summary": 3, "SingleGoal": 2, "Penalties": 1}.get(video_type, 0)

    def _absolute_url(self, url):
        return self._absolute_url_for(self.base_url, url)

    @staticmethod
    def _absolute_url_for(base_url, url):
        if not url:
            return ""
        if url.startswith("http://") or url.startswith("https://"):
            return url
        return urllib.parse.urljoin(base_url, url)

    def _headers_for(self, url):
        headers = dict(self.headers)
        headers["Referer"] = urllib.parse.urljoin(url, "/")
        return headers

    def send_to_api(self, payload):
        if not payload.get("videos"):
            return
        url = f"{self.api_base_url}/api/SportsData/import/match-videos"
        try:
            res = requests.post(url, json=payload, timeout=30)
            print(f"[videos] API {payload.get('match_id')}: {res.status_code} {res.text[:200]}")
        except Exception as e:
            print(f"[videos] API error for {payload.get('match_id')}: {e}")


def main():
    scraper = YallaKoraVideoScraper(api_base_url=API_DOMAIN)
    matches = scraper.get_recent_finished_matches()
    for match in matches:
        payload = scraper.scrape_match_videos(match)
        scraper.send_to_api(payload)
        time.sleep(REQUEST_DELAY)


if __name__ == "__main__":
    main()
