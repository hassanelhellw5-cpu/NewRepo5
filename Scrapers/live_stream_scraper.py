import requests

import re

import json

import os

from difflib import SequenceMatcher
from urllib.parse import urlencode, urlparse
import hmac
import hashlib
import time





class LiveStreamScraper:

    def __init__(self, api_base_url=None):

        self.headers = {

            'User-Agent': 'VLC/3.0.18 LibVLC/3.0.18',
            'Accept': '*/*',
            'Connection': 'keep-alive'

        }

        self.api_base_url = api_base_url

        self.m3u_path = os.path.join(os.path.dirname(__file__), 'tv_channels_TarikBellhcen.m3u')

        self.overrides_path = os.path.join(os.path.dirname(__file__), 'live_stream_overrides.json')
        self.proxy_secret = os.getenv('STREAM_PROXY_SECRET', '')
        self.proxy_ttl_seconds = int(os.getenv('STREAM_PROXY_TTL_SECONDS', '21600'))



    # ---------------------------------------------------------------

    # M3U parsing

    # ---------------------------------------------------------------

    def parse_m3u(self):

        channels = []

        if not os.path.exists(self.m3u_path):

            print(f"M3U file not found at {self.m3u_path}")

            return channels

        try:

            with open(self.m3u_path, 'r', encoding='utf-8', errors='ignore') as f:

                current_channel = {}

                for line in f:

                    line = line.strip()

                    if line.startswith('#EXTINF:'):

                        match = re.search(r',(.+)$', line)

                        if match:

                            current_channel['name'] = match.group(1).strip()

                    elif line.startswith('http'):

                        current_channel['url'] = line

                        if 'name' in current_channel:

                            channels.append(current_channel)

                        current_channel = {}

        except Exception as e:

            print(f"Error parsing M3U file: {e}")

        return channels





    # ---------------------------------------------------------------

    # Optional manual HLS overrides

    # ---------------------------------------------------------------

    def parse_overrides(self):

        if not os.path.exists(self.overrides_path):

            return []

        try:

            with open(self.overrides_path, 'r', encoding='utf-8') as f:

                data = json.load(f)

            if not isinstance(data, list):

                print(f"Overrides file must contain a list: {self.overrides_path}")

                return []

            return data

        except Exception as e:

            print(f"Error parsing overrides file: {e}")

            return []



    def build_proxy_url(self, stream_url, headers):

        if not self.api_base_url:

            return stream_url

        headers = headers or {}

        query = {'url': stream_url}

        if self.proxy_secret:
            expires = str(int(time.time()) + self.proxy_ttl_seconds)
            query['expires'] = expires
            query['sig'] = self._sign_proxy_request(stream_url, expires)

        if headers.get('Referer'):

            query['referer'] = headers['Referer']

        if headers.get('User-Agent'):

            query['userAgent'] = headers['User-Agent']

        return f"{self.api_base_url}/api/SportsData/proxy/hls?{urlencode(query)}"

    def _sign_proxy_request(self, stream_url, expires):
        payload = f"{stream_url}|{expires}".encode('utf-8')
        return hmac.new(self.proxy_secret.encode('utf-8'), payload, hashlib.sha256).hexdigest()

    # ---------------------------------------------------------------

    # Channel matching (this is the part that was missing/broken)

    # ---------------------------------------------------------------

    @staticmethod

    def _normalize(text):

        if not text:

            return ""

        text = text.lower()

        # The matches API can send "HD2", while the playlist writes the
        # same channel as "2 ... HD". Splitting letter/number boundaries keeps
        # MAX HD1/HD2 from matching the generic MAX HD playlist entry.
        text = re.sub(r'([a-z])([0-9])', r'\1 \2', text)
        text = re.sub(r'([0-9])([a-z])', r'\1 \2', text)

        # keep letters (latin + arabic) and digits, drop everything else (HD/+/-, punctuation...)

        text = re.sub(r'[^a-z0-9\u0600-\u06ff]+', ' ', text)

        text = re.sub(r'\s+', ' ', text).strip()

        return text



    @staticmethod

    def _extract_number(text):

        m = re.search(r'\d+', text)

        return m.group(0) if m else None


    @staticmethod

    def _match_score(target, candidate):

        target_tokens = set(target.split())

        candidate_tokens = set(candidate.split())

        if not target_tokens or not candidate_tokens:

            return 0.0

        token_overlap = len(target_tokens & candidate_tokens) / len(target_tokens)

        text_similarity = SequenceMatcher(None, target, candidate).ratio()

        return (token_overlap * 0.65) + (text_similarity * 0.35)



    @staticmethod

    def _looks_like_media_response(response):

        if response.status_code in (401, 403, 404):

            return False

        if response.status_code >= 400:

            return False

        content_type = (response.headers.get('content-type') or '').lower()

        if any(media_type in content_type for media_type in (
            'mpegurl', 'application/vnd.apple.mpegurl', 'video/',
            'application/octet-stream'
        )):

            return True

        try:

            first_bytes = response.raw.read(256, decode_content=True)

        except Exception:

            return True

        stripped = first_bytes.lstrip()

        return (
            stripped.startswith(b'#EXTM3U') or
            stripped.startswith(b'\x47') or
            b'EXT-X-' in stripped[:128]
        )

    @staticmethod

    def _looks_like_live_playlist(playlist_text):

        if not playlist_text.lstrip().startswith('#EXTM3U'):

            return True, "not an HLS playlist"

        upper = playlist_text.upper()

        if '#EXT-X-ENDLIST' in upper:

            return False, "HLS playlist is VOD/ended (#EXT-X-ENDLIST)"

        if '#EXTINF:' not in upper and '#EXT-X-STREAM-INF' not in upper:

            return False, "HLS playlist has no media or variants"

        return True, "live HLS playlist"


    def validate_stream_url(self, url, extra_headers=None):

        parsed = urlparse(url or '')

        if parsed.scheme not in ('http', 'https') or not parsed.netloc:

            return False, "invalid url"

        try:

            request_headers = dict(self.headers)

            if extra_headers:

                request_headers.update(extra_headers)

            response = requests.get(
                url,
                headers=request_headers,
                stream=False,
                timeout=12,
                allow_redirects=True,
            )

            try:

                content_type = (response.headers.get('content-type') or '').lower()
                is_hls_playlist = (
                    parsed.path.lower().endswith('.m3u8') or
                    'mpegurl' in content_type or
                    response.text.lstrip().startswith('#EXTM3U')
                )

                if is_hls_playlist:

                    is_live, reason = self._looks_like_live_playlist(response.text)
                    if not is_live:

                        return False, reason

                if self._looks_like_media_response(response):

                    return True, f"HTTP {response.status_code}"

                return False, f"HTTP {response.status_code}"

            finally:

                response.close()

        except requests.RequestException as e:

            return False, str(e)



    def find_channel_candidates(self, channel_name, channels, threshold=0.62):

        """

        Return all playlist/override entries that confidently match the channel,

        sorted from best match to weakest match. Returning more than one match is

        important because the best M3U entry can be expired (401), while a lower

        priority override or alternate playlist entry may still be playable.

        """

        if not channel_name or not channels:

            return []

        target = self._normalize(channel_name)

        target_num = self._extract_number(target)

        matches = []



        for ch in channels:

            candidate = self._normalize(ch.get('name', ''))

            if not candidate:

                continue



            candidate_num = self._extract_number(candidate)



            # If the requested channel has a number (HD1, HD2, Sports 1...),

            # require the playlist entry to have the same number. Otherwise the

            # generic "beIN SPORTS MAX HD" URL can be selected for every MAX

            # channel, which is what produced unauthorized links.

            if target_num and candidate_num != target_num:

                continue



            score = self._match_score(target, candidate)

            if score >= threshold:

                item = dict(ch)

                item['_match_score'] = score

                matches.append(item)



        return sorted(
            matches,
            key=lambda item: (item.get('priority', 100), -item['_match_score'])
        )

    def find_channel(self, channel_name, channels, threshold=0.62):

        candidates = self.find_channel_candidates(channel_name, channels, threshold)

        return candidates[0] if candidates else None


    @staticmethod

    def _is_finished_status(status):

        if not status:

            return False

        normalized = status.strip().lower()

        return any(token in normalized for token in (
            'انتهت',
            'انتهى',
            'finished',
            'ended',
            'full time',
            'ft',
        ))



    # ---------------------------------------------------------------

    # Fetch matches from your .NET API

    # ---------------------------------------------------------------

    def get_matches_from_api(self):

        if not self.api_base_url:

            return []

        url = f"{self.api_base_url}/api/SportsData/matches"

        try:

            res = requests.get(url, headers=self.headers, timeout=15)

            res.raise_for_status()

            data = res.json()

        except Exception as e:

            print(f"Error fetching matches from API: {e}")

            return []



        return self._flatten_matches(data)



    @staticmethod

    def _flatten_matches(data):

        """

        Supports both shapes:

        1) A flat list of match dicts.

        2) A list of tournaments, each with a 'matches' key (like

           YallaKoraEngine.get_matches() returns).

        """

        flat = []

        if not data:

            return flat



        if isinstance(data, list) and data and isinstance(data[0], dict) and 'matches' in data[0]:

            for tournament in data:

                for m in tournament.get('matches', []):

                    m = dict(m)

                    m['tournament_name'] = tournament.get('tournament_name', '')

                    flat.append(m)

        else:

            flat = data



        return flat



    # ---------------------------------------------------------------

    # Main flow: match -> channel -> m3u8 url

    # ---------------------------------------------------------------

    def get_live_matches(self):

        matches = self.get_matches_from_api()

        m3u_channels = self.parse_m3u()

        override_channels = self.parse_overrides()



        if not matches:

            print("No matches returned from API.")

            return []



        if not m3u_channels and not override_channels:

            print("No channels loaded from M3U file or overrides file, cannot build streams.")

            return []



        all_live = []

        skipped = 0



        for match in matches:

            channel_name = (match.get('channel') or '').strip()

            match_id = match.get('match_id') or match.get('matchId') or match.get('id')

            home_team = match.get('home_team', '')

            away_team = match.get('away_team', '')

            match_status = (match.get('status') or '').strip()

            if self._is_finished_status(match_status):

                message = f"Live is closed because match status is finished: '{match_status}'."
                print(f"Closing {home_team} vs {away_team}: {message}")
                all_live.append({
                    'match_id': match_id,
                    'match_name': f"{home_team} vs {away_team}".strip(),
                    'home_team': home_team,
                    'away_team': away_team,
                    'source': 'LIVE_OFFLINE',
                    'stream_url': '',
                    'm3u8_url': '',
                    'alt_m3u8_url': '',
                    'channel': channel_name,
                    'playlist_channel': '',
                    'status_message': message,
                })
                skipped += 1
                continue



            if not channel_name:

                print(f"Skipping {home_team} vs {away_team}: no channel info.")

                skipped += 1

                continue



            candidates = (
                self.find_channel_candidates(channel_name, override_channels) +
                self.find_channel_candidates(channel_name, m3u_channels)
            )
            print(f"Found {len(candidates)} candidate server(s) for '{channel_name}'.")



            if not candidates:

                message = f"Live is closed right now: channel '{channel_name}' has no configured HLS server."
                print(f"Skipping {home_team} vs {away_team}: {message}")
                all_live.append({
                    'match_id': match_id,
                    'match_name': f"{home_team} vs {away_team}".strip(),
                    'home_team': home_team,
                    'away_team': away_team,
                    'source': 'LIVE_OFFLINE',
                    'stream_url': '',
                    'm3u8_url': '',
                    'alt_m3u8_url': '',
                    'channel': channel_name,
                    'playlist_channel': '',
                    'status_message': message,
                })

                skipped += 1

                continue



            playable_count = 0

            max_servers = int(os.getenv('MAX_STREAM_SERVERS_PER_MATCH', '6'))

            validation_message = "no candidate checked"



            for index, candidate in enumerate(candidates, start=1):

                if playable_count >= max_servers:

                    break

                candidate_url = candidate.get('url')

                candidate_headers = candidate.get('headers') or {}

                is_valid, validation_message = self.validate_stream_url(candidate_url, candidate_headers)

                if not is_valid:

                    print(f"Candidate for '{channel_name}' ({candidate.get('name')}) is not playable: "

                          f"{validation_message}.")

                    continue



                proxied_url = self.build_proxy_url(candidate_url, candidate_headers)

                playable_count += 1

                all_live.append({

                    'match_id': match_id,

                    'match_name': f"{home_team} vs {away_team}".strip(),

                    'home_team': home_team,

                    'away_team': away_team,

                    'source': f"{candidate.get('source', 'M3U Playlist')} #{playable_count}",

                    'stream_url': proxied_url,

                    'm3u8_url': proxied_url,

                    'alt_m3u8_url': "",

                    'channel': channel_name,

                    'playlist_channel': candidate.get('name', ''),

                })



            if playable_count == 0:

                message = f"Live is closed right now for '{channel_name}'. Checked {len(candidates)} server(s). Last check: {validation_message}."
                print(f"Skipping {home_team} vs {away_team}: {message}")
                all_live.append({
                    'match_id': match_id,
                    'match_name': f"{home_team} vs {away_team}".strip(),
                    'home_team': home_team,
                    'away_team': away_team,
                    'source': 'LIVE_OFFLINE',
                    'stream_url': '',
                    'm3u8_url': '',
                    'alt_m3u8_url': '',
                    'channel': channel_name,
                    'playlist_channel': '',
                    'status_message': message,
                })

                skipped += 1

                continue



        print(f"Matched {len(all_live)} streams, skipped {skipped} matches with no channel match.")

        return all_live



    # ---------------------------------------------------------------

    # Send to API

    # ---------------------------------------------------------------

    def send_to_api(self, data):

        if not data:

            print("Nothing to send.")

            return

        if not self.api_base_url:

            print(json.dumps(data, ensure_ascii=False, indent=2))

            return



        url = f"{self.api_base_url}/api/SportsData/import/streams"

        try:

            print(f"Sending {len(data)} streams to API...")

            response = requests.post(url, json=data, timeout=30)

            if response.status_code != 200:

                print(f"Failed to send streams. Status: {response.status_code}, Response: {response.text}")

            else:

                print(f"Successfully sent {len(data)} streams to API. Status: {response.status_code}")

        except Exception as e:

            print(f"Error sending streams to API: {e}")





if __name__ == "__main__":

    API_DOMAIN = os.getenv("API_DOMAIN", "http://qemma.runasp.net")

    scraper = LiveStreamScraper(api_base_url=API_DOMAIN)



    streams = scraper.get_live_matches()



    for s in streams[:5]:

        print(json.dumps(s, ensure_ascii=False, indent=2))



    if streams:

        scraper.send_to_api(streams)
