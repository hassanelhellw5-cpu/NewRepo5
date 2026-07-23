# Qemma API Endpoint Index for Frontend

Use this as the frontend checklist for every exposed endpoint in the current backend. Swagger remains the interactive source at `/swagger`, while this file explains where each route fits in the UI.

## SportsData `/api/SportsData`

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/SportsData/check` | Admin health/debug only. |
| GET | `/api/SportsData/matches` | Main match list, calendar, home live rail, filters. |
| GET | `/api/SportsData/tournaments` | Tournament filters and tournament listing pages. |
| GET | `/api/SportsData/coverage` | Admin data coverage dashboard. |
| POST | `/api/SportsData/import/matches` | Scraper import only. |
| POST | `/api/SportsData/import/news` | Scraper import only. |
| POST | `/api/SportsData/import/streams` | Scraper import only for football stream links. |
| GET | `/api/SportsData/proxy/hls` | HLS proxy for allowed stream hosts. Use only from video player if stream URLs are proxied. |
| GET | `/api/SportsData/proxy/check` | Admin/debug stream health check. |
| GET | `/api/SportsData/news` | Public news list/cards. |
| POST | `/api/SportsData/import/match-details` | Scraper import only for football timeline/stats. |
| GET | `/api/SportsData/match-details/{matchId}` | Football match detail page timeline/stats/score. |
| POST | `/api/SportsData/import/match-videos` | Scraper import only for highlights/goals. |
| GET | `/api/SportsData/match-videos/{matchId}` | Highlights tab/cards. |
| GET | `/api/SportsData/match-videos/{videoId:int}/playback` | Video player metadata/playback. |
| GET | `/api/SportsData/match-videos/{videoId:int}/download` | Optional download action. |
| POST | `/api/SportsData/import/squad` | Scraper import only for lineups. |
| GET | `/api/SportsData/squad/{matchId}` | Football lineup tab. |
| POST | `/api/SportsData/import/tournament-details/{tournamentId}` | Scraper import only for standings/scorers/bracket. |
| GET | `/api/SportsData/standings/{tournamentId}` | Tournament standings tab. |
| GET | `/api/SportsData/scorers/{tournamentId}` | Top scorers tab. |
| GET | `/api/SportsData/bracket/{tournamentId}` | Knockout bracket page/tab. |

## Other sports `/api/other-sports`

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/other-sports/events` | Other-sports listing/live rail. Query with `sportKey`, `date`, `liveOnly`. |
| GET | `/api/other-sports/events/{eventId:int}` | Other-sports detail page: overview/results/timeline/streams. |
| POST | `/api/other-sports/import/events` | Importer/provider only. |
| POST | `/api/other-sports/events/{eventId:int}/streams` | Importer/provider only for attaching linked live streams to one event. |
| POST | `/api/other-sports/events/{eventId:int}/live-updates` | Importer/provider only for live timeline. |

## Auth `/api/auth`

| Method | Endpoint | Frontend use |
|---|---|---|
| POST | `/api/auth/register` | Signup. |
| POST | `/api/auth/login` | Login and JWT storage. |
| GET | `/api/auth/me` | Current authenticated profile. |

## Coins `/api/coins`

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/coins/wallet/{userId}` | Wallet balance. |
| GET | `/api/coins/transactions/{userId}` | Wallet transaction history. |
| POST | `/api/coins/payments/manual` | Upload/manual payment request flow. |
| GET | `/api/coins/payments/manual` | Admin/manual payment list. |
| POST | `/api/coins/payments/manual/{requestId:int}/approve` | Admin approve payment. |
| POST | `/api/coins/payments/manual/{requestId:int}/reject` | Admin reject payment. |
| POST | `/api/coins/rewards/video/complete` | Reward user after rewarded video completion. |

## Fan engagement `/api/fan-engagement`

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/fan-engagement/cosmetics` | Store/catalog. |
| GET | `/api/fan-engagement/events/limited-store` | Limited-time store. |
| GET | `/api/fan-engagement/subscription-packages` | Paid subscription/theme packages and included features. |
| POST | `/api/fan-engagement/supporter/subscribe` | Supporter subscription flow. |
| GET | `/api/fan-engagement/profile/{userId}/premium` | Premium profile header/cards. |
| GET | `/api/fan-engagement/matches/{matchId:int}/experience?userId={userId}` | Match engagement store, fan pass status, supporter leaderboard, SignalR config. |
| GET | `/api/fan-engagement/matches/{matchId:int}/supporter-leaderboard` | Match supporter leaderboard. |
| POST | `/api/fan-engagement/matches/{matchId:int}/fan-pass` | Unlock match fan pass. |
| POST | `/api/fan-engagement/custom-tournaments/requests` | User custom tournament request. |
| POST | `/api/fan-engagement/cosmetics/{cosmeticId:int}/purchase` | Buy/unlock cosmetic. |
| POST | `/api/fan-engagement/cosmetics/{cosmeticId:int}/equip` | Equip cosmetic. |
| GET | `/api/fan-engagement/cheers` | Quick cheer phrases. |
| POST | `/api/fan-engagement/matches/{matchId:int}/chat` | Send match chat message or pinned cheer. |
| GET | `/api/fan-engagement/matches/{matchId:int}/chat` | Read match chat messages. |
| POST | `/api/fan-engagement/matches/{matchId:int}/reactions` | Send normal reaction or premium reaction burst. |
| GET | `/api/fan-engagement/matches/{matchId:int}/live-readiness` | Enable/disable watch/live UI. |

## Admin `/api/admin` and `/api/admin/cosmetics`

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/admin/scraping/status` | Admin scraping dashboard. |
| GET | `/api/admin/scraping/video-coverage` | Admin video coverage. |
| GET | `/api/admin/scraping/logo-coverage` | Admin logo coverage. |
| GET | `/api/admin/payments/stats` | Admin payment statistics. |
| GET | `/api/admin/payments/manual` | Admin manual payments. |
| POST | `/api/admin/payments/manual/{requestId:int}/approve` | Admin approve manual payment. |
| POST | `/api/admin/payments/manual/{requestId:int}/reject` | Admin reject manual payment. |
| GET | `/api/admin/users` | Admin users list. |
| POST | `/api/admin/users/{userId}/coins/adjust` | Admin coin adjustment. |
| POST | `/api/admin/users/{userId}/roles` | Admin role management. |
| GET | `/api/admin/cosmetics` | Admin cosmetics list. |
| POST | `/api/admin/cosmetics` | Admin create cosmetic. |
| PUT | `/api/admin/cosmetics/{cosmeticId:int}` | Admin update cosmetic. |
| GET | `/api/admin/custom-tournaments/requests` | Admin custom tournament requests. |
| POST | `/api/admin/custom-tournaments/requests/{requestId:int}/status` | Admin update custom tournament status. |
| POST | `/api/admin/cosmetics/limited-events` | Admin limited-event cosmetics helper. |

## Fantasy `/api/fantasy`

| Method | Endpoint | Frontend use |
|---|---|---|
| POST | `/api/fantasy/players/import` | Admin/import players. |
| POST | `/api/fantasy/contests` | Create a free contest; do not send coins/fees. |
| POST | `/api/fantasy/contests/join` | Join free contest by code. |
| GET | `/api/fantasy/tournaments/{tournamentId:int}/today/players` | Player picker. |
| POST | `/api/fantasy/contests/{contestId:int}/entries` | Submit fantasy entry. |
| POST | `/api/fantasy/contests/{contestId:int}/score` | Admin/manual scoring. |
| GET | `/api/fantasy/contests/{contestId:int}` | Full contest details, current user entry, leaderboard, matches. |
| GET | `/api/fantasy/contests/{contestId:int}/leaderboard` | Contest leaderboard. |
| GET | `/api/fantasy/contests/code/{code}` | Resolve contest invite code. |
| GET | `/api/fantasy/my?userId={userId}` | User fantasy contests. |
| GET | `/api/fantasy/contests/my?userId={userId}` | Alias for user fantasy contests. |
| POST | `/api/fantasy/contests/{contestId:int}/convert-to-knockout` | Convert contest format. |
| POST | `/api/fantasy/contests/{contestId:int}/advance-knockout-round` | Advance knockout bracket. |
| GET | `/api/fantasy/contests/{contestId:int}/knockout` | Knockout bracket view. |

## Prediction leagues `/api/PredictionLeagues`

| Method | Endpoint | Frontend use |
|---|---|---|
| POST | `/api/PredictionLeagues` | Create prediction league. |
| POST | `/api/PredictionLeagues/join` | Join prediction league. |
| GET | `/api/PredictionLeagues/{leagueId:int}/leaderboard` | League leaderboard. |
| POST | `/api/PredictionLeagues/{leagueId:int}/customization` | League customization. |
| GET | `/api/PredictionLeagues/{leagueId:int}/analytics` | League analytics. |
| POST | `/api/PredictionLeagues/{leagueId:int}/convert-to-knockout` | Convert to knockout. |
| POST | `/api/PredictionLeagues/{leagueId:int}/advance-knockout-round` | Advance knockout round. |
| GET | `/api/PredictionLeagues/{leagueId:int}/knockout` | Knockout bracket. |

## Watch parties `/api/watch-parties`

| Method | Endpoint | Frontend use |
|---|---|---|
| POST | `/api/watch-parties` | Create watch party. |
| GET | `/api/watch-parties/{code}` | Load watch party. |
| POST | `/api/watch-parties/{code}/join` | Join watch party. |
| POST | `/api/watch-parties/{code}/messages` | Send watch party message. |
| GET | `/api/watch-parties/{code}/messages` | Read watch party messages. |

## Notifications and users

| Method | Endpoint | Frontend use |
|---|---|---|
| POST | `/api/users/favorite-teams` | Follow favorite team. |
| POST | `/api/notifications/push-subscriptions` | Register push subscription. |
| DELETE | `/api/notifications/push-subscriptions/{id:int}` | Remove push subscription. |
| GET | `/api/notifications` | Notification center. |
| POST | `/api/notifications/{id:int}/read` | Mark notification read. |
| PUT | `/api/notifications/preferences` | Update notification preferences. |

## Miscellaneous

| Method | Endpoint | Frontend use |
|---|---|---|
| GET | `/api/players?q={name}&teamName={team}&take=50` | Player search/list. |
| GET | `/api/players/{playerId:int}/card` | Player card. |
| GET | `/api/players/{playerId:int}/matches` | Player match-by-match stats. |
| PUT | `/api/players/{playerId:int}/image` | Admin/player image update. |
| GET | `/api/matches/{matchId:int}/player-cards` | Match player cards. |
| GET | `/api/matches/{matchId:int}/summaries` | Match summaries. |
| POST | `/api/matches/{matchId:int}/summaries/generate` | Generate summary. |
| POST | `/api/uploads/receipts` | Upload payment receipt. |
| GET | `/api/analytics/matches/{matchId:int}/momentum` | Match momentum chart. |
| POST | `/api/gamification/streaks/{userId}/touch` | Touch daily streak. |
| GET | `/api/gamification/streaks/{userId}` | Streak state. |
| GET | `/api/gamification/achievements/{userId}` | Achievements. |

## UX readiness checklist

The backend/API is ready when:
- Production DB has the latest migration scripts applied.
- Scraper process can reach Python dependencies and the upstream providers.
- `SportsSync:Enabled=true` on the server.
- `STREAM_PROXY_SECRET` is set if stream proxy signing is required.
- `other_sports_live_rules.json` maps tennis/basketball/F1 events to channel names, and those channels exist in `live_stream_overrides.json` or the M3U playlist.
- Admin dashboard exposes coverage and stream-mapping controls instead of requiring file edits.

The frontend should still handle empty states:
- No `streams[].playbackUrl` available.
- Score source delayed.
- Timeline unavailable.
- Video/highlight not ready yet.
- SignalR disconnected, fallback to polling.
