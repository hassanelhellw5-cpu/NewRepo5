# Live engagement and monetization roadmap

## Monetized cosmetics

Use cosmetics as non-pay-to-win purchases:

- Full website themes: free base themes plus paid premium palettes.
- Team/player emojis: free common emojis plus premium team/player packs.
- Badges: supporter, top predictor, match hero, derby badge.

Backend endpoints:

- `GET /api/fan-engagement/cosmetics` lists active themes, emojis, and badges.
- `POST /api/fan-engagement/cosmetics/{cosmeticId}/purchase` unlocks a cosmetic with coins when its unlock type is `Coins`.
- `POST /api/fan-engagement/cosmetics/{cosmeticId}/equip` equips an owned cosmetic.

## Live match chat rooms

Each match has its own chat room. The SignalR group name is `match:{matchId}`.

- Clients call `JoinMatchRoom(matchId)` when opening a live match page.
- Clients call `LeaveMatchRoom(matchId)` when closing it.
- Chat messages are sent through `POST /api/fan-engagement/matches/{matchId}/chat` and broadcast as `ReceiveMatchChatMessage`.

## Free cheering phrases

Cheering phrases are free one-tap messages during the live stream, such as famous chants or neutral football reactions.

- `GET /api/fan-engagement/cheers` returns active phrases.
- The frontend can render phrase buttons inside the live chat composer.
- Sending a phrase still goes through the match chat endpoint so it is stored and broadcast consistently.

## Paid pinned live comments

Pinned comments are a monetization feature similar to a lightweight super chat:

- User writes a comment.
- User chooses pin duration.
- Backend charges coins with transaction type `PinLiveComment`.
- The message is stored with `IsPinned`, `PaidCoins`, and `PinnedUntil`.
- The frontend renders pinned messages above normal chat until the pin expires.

## Halftime trivia

Trivia sessions are optional games that appear during a live match break.

Suggested product behavior:

1. Five minutes after the first half starts, show a teaser: `Trivia opens at halftime`.
2. At halftime, call `GET /api/fan-engagement/matches/{matchId}/trivia/active`.
3. If active, show the user a choice: play solo, challenge another user in the same match room, or ignore.
4. End the game automatically when the second half starts or when the configured `EndsAt` passes.
5. Show the quick leaderboard and distribute optional coin/badge rewards.

Question formats supported by the backend:

- Multiple choice.
- Complete a famous lineup.
- Guess the player.
- Guess the score.
- True/false.

Backend endpoints:

- `POST /api/fan-engagement/matches/{matchId}/trivia/sessions` schedules a trivia session.
- `POST /api/fan-engagement/trivia/questions` creates a reusable or match-specific question.
- `POST /api/fan-engagement/trivia/sessions/{sessionId}/answer` submits a solo answer and returns correctness and points.
- `POST /api/fan-engagement/trivia/sessions/{sessionId}/duels` creates a same-match duel.
- `POST /api/fan-engagement/trivia/duels/{duelId}/join` lets another user accept the duel.
- `POST /api/fan-engagement/trivia/sessions/{sessionId}/complete` closes the game and rewards winners.

## Extra monetization ideas for live matches

- Paid animated goal reactions.
- Limited-time derby emoji packs.
- Match predictor boost that highlights a user's prediction in chat.
- VIP live room with slower chat and fewer ads.
- Supporter leaderboard for each match based on coins spent, trivia score, and prediction accuracy.
