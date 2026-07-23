# دليل ربط الفرونت الكامل للـ AI Agent

هذا الملف مخصص لأي AI أو مطور فرونت يربط المشروع. اتبع الخطوات كما هي ولا تخترع endpoints غير موجودة.

## 1) الإعداد العام

- API base في Vite غالبًا: `/qemma-api/api`.
- SignalR base غالبًا: `/qemma-api` ثم hub path `/matchHub`.
- كل request محمي يرسل `Authorization: Bearer <token>`.
- أي request فيه `userId` لازم يكون نفس المستخدم الموجود في التوكن، أو أدمن.

## 2) اللاعبين Player Profiles

### البحث/القائمة

`GET /api/players?q={name}&teamName={team}&take=50`

استخدمه في search boxes، صفحات اللاعبين، واختيار لاعب للعرض. الرد المختصر فيه `cardUrl` لكل لاعب.

### كارت لاعب كامل

`GET /api/players/{playerId}/card`

يعرض: بيانات اللاعب، الفريق، المركز، الصورة، الجنسية، العمر، إحصائيات مجمعة، نقاط فانتازي، آخر مباريات، ورابط share image.

### مباريات لاعب

`GET /api/players/{playerId}/matches?take=25`

يعرض تفاصيل أداء اللاعب في كل مباراة: دقائق، بدأ/بديل، أهداف، أسيست، كروت، تسديدات، key passes، saves، clean sheet، fantasy points، rating.

### لاعيبة ماتش

`GET /api/matches/{matchId}/player-cards`

لو عندنا stats يرجع player-match-stats، ولو stats لسه غير موجودة يرجع lineups كـ fallback حتى لا تظهر الصفحة فاضية.

## 3) Fantasy Leagues/Contests

### إنشاء مسابقة فانتازي مجانية

`POST /api/fantasy/contests` — Auth

```json
{
  "tournamentId": 1,
  "contestDate": "2026-07-23T00:00:00Z",
  "name": "Fantasy Friends",
  "ownerUserId": "current-user-id",
  "isPublic": false,
  "maxMembers": 20,
  "format": 0
}
```

مهم جدًا:
- لا ترسل `priceCoins` أو `entryFeeCoins` أو أي بيانات دفع.
- إنشاء الفانتازي مجاني بالكامل.
- بعد النجاح خزن `id` و`code`.

### مسابقات المستخدم في الفانتازي

- `GET /api/fantasy/my?userId={userId}`
- `GET /api/fantasy/contests/my?userId={userId}`

### تفاصيل مسابقة

`GET /api/fantasy/contests/{contestId}?userId={userId}`

يعرض contest، currentUserEntry، leaderboard، matches، وlinks للاعبين والـ knockout.

### لاعيبة الفانتازي لماتشات اليوم فقط

`GET /api/fantasy/tournaments/{tournamentId}/today/players?date=YYYY-MM-DD`

لا تستخدم أي endpoint عام للاعبين في fantasy picker. اعتمد على `playerSource`:
- `today-match-lineups`: أفضل وضع، لاعيبة ماتشات اليوم.
- `recent-team-lineups-and-scorers-fallback`: fallback لو تشكيلات اليوم غير متاحة.
- `no-matches-for-date`: اعرض empty state.

### حفظ 5 لاعبين

`POST /api/fantasy/contests/{contestId}/entries`

```json
{ "userId": "current-user-id", "playerIds": [1, 2, 3, 4, 5] }
```

## 4) Prediction Leagues

### إنشاء دوري توقعات

`POST /api/PredictionLeagues` — Auth

```json
{
  "name": "توقعات الأصدقاء",
  "ownerUserId": "current-user-id",
  "isPublic": false,
  "maxMembers": 20,
  "format": 0,
  "startsAt": "2026-07-23T00:00:00Z",
  "endsAt": "2026-08-01T00:00:00Z"
}
```

دوريات التوقعات مجانية في الإنشاء والانضمام؛ لا تعرض دفع هنا.

### دورياتي

`GET /api/PredictionLeagues/my?userId={userId}`

### مباريات الدوري والتوقع

`GET /api/PredictionLeagues/{leagueId}/matches?userId={userId}&from=YYYY-MM-DD&to=YYYY-MM-DD`

اعرض زر التوقع فقط لو `lockedForPrediction=false`.

### حفظ توقع

`POST /api/PredictionLeagues/{leagueId}/predict`

```json
{
  "userId": "current-user-id",
  "matchId": 123,
  "predictedHomeScore": 2,
  "predictedAwayScore": 1
}
```

## 5) Fan Engagement والثيمات المدفوعة

### باقات الاشتراك والثيمات

`GET /api/fan-engagement/subscription-packages`

اعرض كل باقة كـ card فيها:
- `name`
- `priceCoins`
- `durationDays`
- `includes[]`
- `recommended`
- `ctaEndpoint`

### تفعيل Supporter subscription

`POST /api/fan-engagement/supporter/subscribe`

```json
{ "userId": "current-user-id", "months": 1, "priceCoins": 99 }
```

لو الرصيد غير كافٍ سيرجع 400 برسالة `Insufficient Qemma Coins balance.`.

### متجر الثيمات والكوزمتكس

`GET /api/fan-engagement/cosmetics?type=0&category=MatchDay`

`type=0` يعني Theme. اعرض السعر والمحتوى والوصف قبل الشراء.

### شراء وتجهيز ثيم

```http
POST /api/fan-engagement/cosmetics/{cosmeticId}/purchase
```

```json
{ "userId": "current-user-id", "equipNow": true }
```

أو تجهيز فقط لو مشتريه قبل كده:

```http
POST /api/fan-engagement/cosmetics/{cosmeticId}/equip
```

```json
{ "userId": "current-user-id" }
```

## 6) شحن الكوينز

### المحفظة

`GET /api/coins/wallet/{userId}`

### طلب شحن يدوي

`POST /api/coins/payments/manual`

```json
{
  "userId": "current-user-id",
  "amountPaid": 100,
  "requestedCoins": 100,
  "paymentMethod": "Vodafone Cash",
  "phoneNumberUsed": "01000000000",
  "receiptImageUrl": "https://..."
}
```

### سجل المعاملات

`GET /api/coins/transactions/{userId}?page=1&pageSize=25`

### مكافأة فيديو بإمضاء

`POST /api/coins/rewards/video/complete`

لا تستخدمه من الفرونت بدون provider/signature حقيقي في الإنتاج.

## 7) الرياضات الأخرى

### قائمة الرياضات للعرض

`GET /api/other-sports/sports`

### أحداث اليوم

`GET /api/other-sports/events?date=YYYY-MM-DD&sportKey=tennis`

### تفاصيل حدث

`GET /api/other-sports/events/{eventId}`

ادخل SignalR room عبر `JoinOtherSportRoom(eventId)` واستمع لـ `ReceiveOtherSportUpdate` و`ReceiveOtherSportLiveUpdate`.

## 8) YallaShoot app و run_all

`run_all.py` يشغل `yallakora_video_scraper.py` في profile full. هذا السكريبر يحاول مصدر YallaShoot app API إذا كان `YALLASHOOT_APP_API_URL` مضبوطًا، وإلا يكمل بفولباك يلا كورة والمصادر العامة بدون فشل.

للتفعيل:

```bash
export YALLASHOOT_APP_API_URL='https://api.example.com/highlights?match={match_id}&q={query}'
export YALLASHOOT_APP_TOKEN='optional-token'
python Scrapers/run_all.py --date 2026-07-23 --profile full
```

لو عايز Appium native scan:

```bash
export ENABLE_YALLASHOOT_APPIUM_HIGHLIGHTS=true
export YALLASHOOT_APP_PACKAGE='com.example.app'
python Scrapers/run_all.py --date 2026-07-23 --profile full
```
