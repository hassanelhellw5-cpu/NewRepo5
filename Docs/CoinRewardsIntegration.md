# Qemma Coins and free rewarded ad integration

## Goal

Qemma Coins should be earned from a real ad network that is free to join as a publisher. The website must not credit coins just because the frontend says an ad was watched. The ad network should confirm the completed view, then the backend credits the user.

## Best free starter choice without a business email

### CPAlead for offerwall / locker rewards

Use CPAlead first if AppLixir asks for a business email. CPAlead supports publisher offerwalls, API feeds, direct links, and lockers, so it can unlock coins or live access after a user completes an offer. It is usually easier for early-stage websites than business-email-first rewarded-video networks.

Recommended placements:

- Wallet page: `Watch an ad and earn coins`.
- Before live stream access: `Watch an ad to unlock the live stream`.
- Before premium prediction features: `Watch an ad to get coins`.

Backend integration pattern:

1. The frontend opens the CPAlead offerwall/locker or another rewarded provider SDK.
2. The provider confirms that the offer/ad was completed.
3. The provider postback or frontend callback sends the reward to `POST /api/coins/rewards/video/complete`.
4. The backend checks that `(Provider, ExternalRewardId)` was not used before.
5. The backend verifies the HMAC signature when `Rewards:WebhookSecret` is configured.
6. The backend credits Qemma Coins and records a transaction.

### MyLead as a simple content-locker fallback

MyLead is another beginner-friendly option for lockers. Use it when you want a simple free account and a generated locker script to paste into the frontend. It is better for unlocking content/access than for a pure rewarded-video coin economy.

Suggested placements:

- Unlock the live stream after completing an offer.
- Unlock an archive video.
- Unlock a bonus prediction feature.

### AppLixir later if you get a business email

Keep AppLixir as a later option when you have a domain email such as `admin@yourdomain.com`, because it is closer to a pure rewarded-video experience than offerwall/locker networks.

## Free starter networks for regular site ads

Use these for normal monetization placements, not for coins unless they provide a trusted rewarded/postback flow.

### Adsterra

Adsterra is free for publishers and has no entrance traffic limits, so it is suitable for an early-stage website. Use it for normal ad zones such as banners, native ads, or a carefully limited social bar.

Suggested placements:

- Home page banner.
- Match details banner.
- Native ad between news cards.

### HilltopAds

HilltopAds supports publisher monetization and has video ad formats such as VAST and Slider. It can be tested for pre-roll or video-style placements when the frontend player supports VAST.

Suggested placements:

- Pre-live ad slot if the video player supports VAST.
- Video slider on content pages.
- Banner under match details.

### Monetag

Monetag lets publishers sign up and monetize traffic. Use it carefully for non-intrusive ad formats only, because aggressive formats can hurt user experience on a new sports website.

Suggested placements:

- In-page format after a user action.
- Direct monetization experiments after the core UX is stable.

## Live stream gate flow

For the live page, keep the flow optional and clear:

1. User opens the match live page.
2. Show a modal: `Watch a short ad to open the live stream`.
3. Start the rewarded ad provider.
4. If the ad completes, unlock the live player for that user/session.
5. If the ad fails or there is no fill, show a fallback message or allow a coin payment alternative.

Do not force repeated ads on every refresh. Store a short unlock window per user/session so the live page remains usable.

## Reward signature

When `Rewards:WebhookSecret` is set, sign this exact payload with HMAC-SHA256 and send it as lowercase hex in `signature`:

```text
{UserId}|{Provider}|{ExternalRewardId}|{CoinsAwarded}
```

Example request body:

```json
{
  "userId": "USER_ID",
  "provider": "CPAlead",
  "placementId": "live-unlock",
  "externalRewardId": "PROVIDER_UNIQUE_REWARD_ID",
  "coinsAwarded": 5,
  "signature": "hmac_sha256_hex"
}
```

## What to collect from any ad network

- Publisher ID.
- Site ID or Game ID.
- Zone ID or Placement ID.
- JavaScript SDK or ad tag.
- Reward callback or postback settings for rewarded ads.
- Secret/API key if the provider supports signed callbacks.
- `ads.txt` lines if required.
- Payment method and minimum payout.
- Allowed and blocked ad categories.
