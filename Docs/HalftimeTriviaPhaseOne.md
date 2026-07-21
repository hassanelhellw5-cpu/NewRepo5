# Halftime trivia phase one

## User flow

0. Before opening the live page, call `GET /api/fan-engagement/matches/{matchId}/live-readiness` to confirm the stream, chat room, and trivia questions are ready.
1. Around minute 40, the frontend calls `GET /api/fan-engagement/matches/{matchId}/trivia/active`.
2. If the response phase is `Teaser`, show: `استعد لتحدي بين الشوطين`.
3. At halftime, the same endpoint returns phase `Playing` and message: `العب تحدي كروي سريع واكسب نقاط/كوينز/بادج`.
4. The user chooses one of three modes:
   - `Solo` plays against the clock.
   - `Duel` challenges another user in the same match room.
   - `Ignore` hides the game.
5. The game runs for the session window, usually 5-10 minutes.
6. Before the second half starts, call `POST /api/fan-engagement/trivia/sessions/{sessionId}/complete` to close the game and distribute rewards.
7. Show the quick leaderboard from `GET /api/fan-engagement/trivia/sessions/{sessionId}/leaderboard`.

## Solo mode

- Create or reuse a session with `POST /api/fan-engagement/matches/{matchId}/trivia/sessions`.
- Create questions with `POST /api/fan-engagement/trivia/questions` or use seeded starter questions.
- Submit answers with `POST /api/fan-engagement/trivia/sessions/{sessionId}/answer`.
- Users cannot answer the same question twice in the same session.

## Duel mode

- Create a duel with `POST /api/fan-engagement/trivia/sessions/{sessionId}/duels`.
- If no opponent is supplied, the duel waits in the same match room.
- Another user joins with `POST /api/fan-engagement/trivia/duels/{duelId}/join`.
- Both users answer with `POST /api/fan-engagement/trivia/duels/{duelId}/answer`.
- Read duel state and scores with `GET /api/fan-engagement/trivia/duels/{duelId}`.

## Seeded question set

The backend seeds varied starter questions across difficulty levels:

- Easy: World Cup 2022 winner.
- Easy: Ronaldo club true/false.
- Medium: Guess Mohamed Salah.
- Medium: Guess the 2005 Champions League final score before penalties.
- Hard: Complete Barcelona's MSN lineup.
- Hard: Germany 2014 World Cup final scorer.

## Rewards

A trivia session can define:

- `WinnerCoinsReward` for top users.
- `BadgeRewardCosmeticItemId` for an optional badge unlock.
- `RewardsDistributed` prevents duplicate reward payouts.

The completion endpoint currently rewards the top three solo leaderboard users.
