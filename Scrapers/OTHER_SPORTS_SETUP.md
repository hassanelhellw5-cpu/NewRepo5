# Other sports setup

This folder has one generic import path for non-football sports:

1. Import event metadata from a legal data source into `/api/other-sports/import/events`.
2. Attach any stream that you own or are allowed to use through `other_sports_stream_overrides.json`.
3. Route HLS streams through `/api/SportsData/proxy/hls` so the frontend receives one playable `playbackUrl` shape.

## Supported event sources

`other_sports_importer.py` already supports these modes:

| Use case | Environment | Notes |
| --- | --- | --- |
| Live scores for common sports | `OTHER_SPORTS_SOURCE=thesportsdb` | Uses TheSportsDB livescore endpoints. Configure sports with `THESPORTSDB_SPORTS`. |
| Your own normalized API | `OTHER_SPORTS_FEED_URL=https://.../events.json` | The response can be either an array or `{ "events": [...] }`. |
| Local generated feed | `OTHER_SPORTS_FEED_FILE=/path/to/events.json` | Good for scheduled jobs that scrape or buy data elsewhere first. |
| Formula 1 schedule/news | `ENABLE_F1_MOTORSPORT_IMPORT=true` | Runs `f1_motorsport_importer.py` from `run_all.py`. |

Recommended `THESPORTSDB_SPORTS` values for the current importer:

```bash
export THESPORTSDB_SPORTS="basketball,tennis,motorsport,ice_hockey,baseball,american_football"
```

If a tournament is not covered well by TheSportsDB, generate a normalized JSON feed and point `OTHER_SPORTS_FEED_URL` or `OTHER_SPORTS_FEED_FILE` at it.

## Normalized event shape

Every non-football provider should be converted to this shape before import:

```json
{
  "sportKey": "basketball",
  "externalId": "provider-game-123",
  "title": "Los Angeles Lakers vs Boston Celtics",
  "competitionName": "NBA",
  "eventDate": "2026-07-10T22:30:00Z",
  "status": "Scheduled",
  "time": "22:30 UTC",
  "venue": "Crypto.com Arena",
  "country": "USA",
  "source": "provider-name",
  "sourceUrl": "https://provider.example/events/123",
  "participants": [
    { "name": "Los Angeles Lakers", "teamName": "Los Angeles Lakers", "country": "USA", "role": "home" },
    { "name": "Boston Celtics", "teamName": "Boston Celtics", "country": "USA", "role": "away" }
  ],
  "results": [],
  "streams": []
}
```

## Stream overrides for all tournaments

Use `Scrapers/other_sports_stream_overrides.json` for every sport. The matcher supports:

- `sportKey`
- `competitionName`
- `titleContains`
- `externalId`

Example for Formula 1:

```json
{
  "sportKey": "formula1",
  "competitionName": "formula 1",
  "titleContains": "grand prix",
  "streams": [
    {
      "source": "Owned F1 HLS feed",
      "m3U8Url": "https://xameleon.phantemlis.top/five/secure/example/premium98/tracks-v1a1/mono.m3u8",
      "headers": {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36",
        "Referer": "https://hamis.romponalis.st/"
      }
    }
  ]
}
```

Example for basketball:

```json
{
  "sportKey": "basketball",
  "competitionName": "NBA",
  "titleContains": "Lakers",
  "streams": [
    {
      "source": "Owned NBA feed",
      "m3U8Url": "https://your-owned-domain.example/nba/lakers/master.m3u8",
      "headers": {
        "User-Agent": "Mozilla/5.0",
        "Referer": "https://your-owned-domain.example/"
      }
    }
  ]
}
```

Example for tennis:

```json
{
  "sportKey": "tennis",
  "competitionName": "wimbledon",
  "titleContains": "centre court",
  "streams": [
    {
      "source": "Owned tennis feed",
      "m3U8Url": "https://your-owned-domain.example/tennis/centre-court/master.m3u8",
      "headers": {
        "User-Agent": "Mozilla/5.0",
        "Referer": "https://your-owned-domain.example/"
      }
    }
  ]
}
```

## Required stream proxy environment

For HLS playback through the backend proxy, configure:

```bash
export STREAM_PROXY_SECRET="change-me"
export STREAM_PROXY_ALLOWED_HOSTS="xameleon.phantemlis.top,your-owned-domain.example"
export STREAM_PROXY_TTL_SECONDS=21600
```

`STREAM_PROXY_ALLOWED_HOSTS` is comma-separated. Add every upstream HLS host that you own or are authorized to proxy.

## Running the pipeline

```bash
export API_DOMAIN="https://your-api.example"
export IMPORT_API_KEY="your-import-key"
export OTHER_SPORTS_SOURCE="thesportsdb"
export THESPORTSDB_SPORTS="basketball,tennis,motorsport,ice_hockey"
export ENABLE_F1_MOTORSPORT_IMPORT=true
python Scrapers/run_all.py
```

For a custom feed instead of TheSportsDB:

```bash
export OTHER_SPORTS_FEED_URL="https://your-data-provider.example/other-sports/events.json"
python Scrapers/other_sports_importer.py
```

## Quick answer: where each tournament comes from

| Sport/tournament | Events/scores | Live streams |
| --- | --- | --- |
| Formula 1 | `f1_motorsport_importer.py` or a paid sports-data feed | `other_sports_stream_overrides.json` with `sportKey=formula1` |
| Basketball | TheSportsDB, your provider API, or normalized feed | `other_sports_stream_overrides.json` with `sportKey=basketball` |
| Tennis | TheSportsDB, your provider API, or normalized feed | `other_sports_stream_overrides.json` with `sportKey=tennis` |
| Ice hockey/baseball/American football | TheSportsDB or normalized feed | Matching override by `sportKey` and `competitionName` |
| Any unsupported tournament | Build/generate a normalized JSON feed | Add an override matched by `externalId` for exact event-level streams |

## Automatic live linking for football, tennis, basketball, and Formula 1

Use `unified_live_stream_linker.py` when you want one job to attach live streams to every supported event type:

```bash
export ENABLE_UNIFIED_LIVE_LINKER=true
python Scrapers/run_all.py
```

The unified linker does two things:

1. Football: keeps the existing `live_stream_scraper.py` flow, so matches with a `channel` are matched against `live_stream_overrides.json` and the M3U playlist.
2. Other sports: reads `other_sports_live_rules.json`, finds matching events from `/api/other-sports/events`, resolves the configured `channelName` against the same channel sources, validates HLS, and posts streams to `/api/other-sports/events/{eventId}/streams`.

Create `Scrapers/other_sports_live_rules.json` from `other_sports_live_rules.example.json`:

```json
[
  {
    "sportKey": "formula1",
    "competitionName": "formula 1",
    "titleContains": "grand prix",
    "channelName": "Formula 1"
  },
  {
    "sportKey": "tennis",
    "competitionName": "wimbledon",
    "titleContains": "centre court",
    "channelName": "Tennis Centre Court"
  },
  {
    "sportKey": "basketball",
    "competitionName": "NBA",
    "channelName": "NBA TV"
  }
]
```

Then make sure the channel names exist in `live_stream_overrides.json` or in the M3U playlist. When the user opens a football match or other-sport event, the API response includes `streams[].playbackUrl` if a playable HLS stream was linked.
