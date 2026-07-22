# دليل الفرونت إند الكامل: الفانتازي، دوريات التوقعات، والـ SignalR

> استخدم هذا الملف كـ checklist أثناء بناء صفحات الفرونت. كل endpoints التي تحتاج Bearer token مكتوب بجانبها `Auth`.

## 1) قاعدة الـ API والـ Proxy

لو الفرونت شغال على Vite والبروكسي عندك مثل `/qemma-api -> backend`:

- اكتب في الكود: `/qemma-api/api/...`
- الباك إند يستقبلها: `/api/...`
- لا تكتب `/qemma-api/api/api/...` ولا `/api/api/...`.

مثال helper:

```ts
const API_PREFIX = import.meta.env.VITE_API_PREFIX ?? '/qemma-api/api';

async function apiFetch(path: string, options: RequestInit = {}) {
  const token = authStore.getState().token;
  return fetch(`${API_PREFIX}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options.headers ?? {})
    }
  });
}
```

## 2) Dashboard: عرض كل المسابقات التي دخلها المستخدم

### Endpoint موحد للداشبورد

`GET /api/users/{userId}/competitions` — `Auth`

الاستخدام من Vite:

```ts
const res = await apiFetch(`/users/${userId}/competitions`);
const data = await res.json();
```

الرد يحتوي:

```ts
type UserCompetitionsResponse = {
  userId: string;
  predictionLeagues: Array<{
    type: 'prediction';
    memberId: number;
    role: 'Owner' | 'Admin' | 'Member' | number;
    totalPoints: number;
    joinedAt: string;
    league: { id: number; name: string; code: string; isPublic: boolean; status: string | number; format: string | number };
  }>;
  fantasyContests: Array<{
    type: 'fantasy';
    entryId: number;
    role: 'Owner' | 'Admin' | 'Member' | number;
    totalPoints: number;
    joinedAt: string;
    contest: { id: number; name: string; code: string; tournamentId: number; tournament: string; contestDate: string; isPublic: boolean; isOpen: boolean; format: string | number };
  }>;
};
```

### Endpoints منفصلة لو الصفحة محتاجة نوع واحد

- `GET /api/PredictionLeagues/my?userId={userId}` — `Auth`
- `GET /api/fantasy/my?userId={userId}` — `Auth`
- `GET /api/fantasy/contests/my?userId={userId}` — `Auth` alias لنفس قائمة مسابقات الفانتازي.

## 3) Fantasy flow كامل

### إنشاء مسابقة فانتازي

`POST /api/fantasy/contests` — `Auth`

```json
{
  "tournamentId": 1,
  "contestDate": "2026-07-22T00:00:00Z",
  "name": "Fantasy Friends",
  "ownerUserId": "current-user-id",
  "isPublic": false,
  "maxMembers": 20,
  "format": 0
}
```

- `format = 0`: leaderboard.
- `format = 1`: knockout.
- إنشاء مسابقة الفانتازي مجاني تمامًا: لا ترسل `priceCoins` أو `entryFeeCoins` أو أي بيانات دفع، ولا تربط زر الإنشاء بمحفظة الكوينز.
- بعد النجاح خزّن `id` و`code` وافتح شاشة التفاصيل.

### الانضمام بالكود

`POST /api/fantasy/contests/join` — `Auth`

```json
{ "userId": "current-user-id", "code": "ABC123" }
```

لو المستخدم داخل بالفعل، الباك إند يرجع OK برسالة `User already joined...`، فلا تعتبرها fatal error.

### تفاصيل مسابقة فانتازي كاملة

`GET /api/fantasy/contests/{contestId}?userId={userId}` — قراءة عامة، و`userId` اختياري لكنه يحتاج Auth لو أرسلته.

استخدمه عند فتح صفحة المسابقة. الرد فيه:

- `contest`: بيانات المسابقة.
- `currentUserEntry`: دخول المستخدم الحالي، نقاطه، اختياراته، والتحويلات.
- `leaderboard`: الترتيب كامل مع picks.
- `matches`: ماتشات البطولة في يوم المسابقة.
- `links`: روابط جاهزة للاعبين والترتيب والـ knockout.

### جلب لاعيبة الاختيار

`GET /api/fantasy/tournaments/{tournamentId}/today/players?date=YYYY-MM-DD`

قواعد مهمة جدًا للفرونت:

1. لا تستخدم `GET /api/players` أو أي endpoint عام للاعبين داخل picker الفانتازي.
2. استخدم `contest.tournamentId` و`contest.contestDate` من تفاصيل المسابقة.
3. اقرأ `playerSource`:
   - `today-match-lineups`: ممتاز، اللاعبين من lineups ماتشات اليوم.
   - `recent-team-lineups-and-scorers-fallback`: lineups اليوم غير متاحة؛ اعرض تنبيه أن القائمة تقديرية من آخر قوائم الفرق.
   - `no-matches-for-date`: لا توجد ماتشات لهذا التاريخ؛ اعرض empty state.
4. لو `players.length === 0` اعرض: `لا توجد قوائم لاعبين متاحة لهذه المباريات حتى الآن`.

### حفظ تشكيلة 5 لاعبين

`POST /api/fantasy/contests/{contestId}/entries` — `Auth`

```json
{ "userId": "current-user-id", "playerIds": [1, 2, 3, 4, 5] }
```

Validation في الفرونت قبل الإرسال:

- لازم 5 لاعبين بالضبط.
- ممنوع تكرار نفس player id.
- عطّل زر الحفظ أثناء request.
- بعد النجاح اعمل refetch لـ `GET /api/fantasy/contests/{contestId}?userId=...`.

### التحويلات

`POST /api/fantasy/contests/{contestId}/transfers` — `Auth`

```json
{ "userId": "current-user-id", "outPlayerId": 10, "inPlayerId": 25 }
```

اعرض للمستخدم:

- `freeTransfersBanked`
- `transfersMadeThisRound`
- `transferPenaltyPoints`
- `totalPoints`

### ترتيب الفانتازي فقط

`GET /api/fantasy/contests/{contestId}/leaderboard`

لو صفحة التفاصيل تستخدم endpoint التفاصيل الكامل، يمكن الاستغناء عن هذا إلا في widgets خفيفة.

### Knockout للفانتازي

- `POST /api/fantasy/contests/{contestId}/convert-to-knockout` — `Auth`, owner/admin فقط.
- `POST /api/fantasy/contests/{contestId}/advance-knockout-round` — `Auth`, owner/admin فقط.
- `GET /api/fantasy/contests/{contestId}/knockout` — قراءة bracket.

## 4) Prediction leagues flow كامل

### إنشاء دوري توقعات

`POST /api/PredictionLeagues` — `Auth`

```json
{
  "name": "توقعات الأصدقاء",
  "ownerUserId": "current-user-id",
  "isPublic": false,
  "maxMembers": 20,
  "format": 0,
  "startsAt": "2026-07-22T00:00:00Z",
  "endsAt": "2026-08-01T00:00:00Z"
}
```

### الانضمام لدوري توقعات

`POST /api/PredictionLeagues/join` — `Auth`

```json
{ "userId": "current-user-id", "code": "XYZ789" }
```

### مباريات الدوري للتوقع

`GET /api/PredictionLeagues/{leagueId}/matches?userId={userId}&from=YYYY-MM-DD&to=YYYY-MM-DD` — `Auth` لو أرسلت `userId`.

استخدمه لبناء كروت التوقع. كل match يرجع `lockedForPrediction` و`prediction` الخاصة بالمستخدم لو موجودة.

### حفظ توقع

`POST /api/PredictionLeagues/{leagueId}/predict` — `Auth`

```json
{
  "userId": "current-user-id",
  "matchId": 123,
  "predictedHomeScore": 2,
  "predictedAwayScore": 1
}
```

لا تعرض زر التوقع لو `lockedForPrediction = true`.

### الترتيب والتحليلات

- `GET /api/PredictionLeagues/{leagueId}/leaderboard`
- `GET /api/PredictionLeagues/{leagueId}/analytics`
- `POST /api/PredictionLeagues/{leagueId}/customization` — `Auth`.

### Knockout لدوري التوقعات

- `POST /api/PredictionLeagues/{leagueId}/convert-to-knockout` — `Auth`, owner/admin فقط.
- `POST /api/PredictionLeagues/{leagueId}/advance-knockout-round` — `Auth`, owner/admin فقط.
- `GET /api/PredictionLeagues/{leagueId}/knockout`.

## 5) الرياضات الأخرى

- `GET /api/other-sports/sports`: قائمة الرياضات مع counts.
- `GET /api/other-sports/events?date=YYYY-MM-DD&sportKey=tennis`: أحداث اليوم/رياضة محددة.
- `GET /api/other-sports/events/{eventId}`: تفاصيل event، ويشمل live updates والـ streams العامة.

لو رجع 503 برسالة database login، دي مشكلة إعداد `ConnectionStrings:DefaultConnection` وليست مشكلة فرونت.

## 6) SignalR لتحديث النتائج والـ live UI

### إنشاء الاتصال

Hub path: `/matchHub`.

لو `API_PREFIX = /qemma-api/api`، فالـ hub ليس تحت `/api` عادة. استخدم base منفصل:

```ts
const SIGNALR_BASE = import.meta.env.VITE_SIGNALR_BASE ?? '/qemma-api';

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${SIGNALR_BASE}/matchHub`, {
    accessTokenFactory: () => authStore.getState().token ?? ''
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
  .build();

connection.onreconnected(() => {
  // أعد Join للـ rooms المفتوحة بعد reconnect.
});

await connection.start();
```

### Events عامة للمباريات

استمع دائمًا لـ:

```ts
connection.on('ReceiveMatchUpdate', payload => {
  // يحدث بعد sync/import عام، تفاصيل مباراة، أو فيديوهات.
  // لو payload.matches موجود: حدّث list cache مباشرة.
  // لو payload.matchId موجود: اعمل invalidate لتفاصيل هذا الماتش.
});
```

### شاشة تفاصيل مباراة كرة قدم

عند فتح صفحة match داخلي `match.id`:

```ts
await connection.invoke('JoinMatchRoom', match.id);

connection.on('ReceiveMatchScoreUpdate', update => {
  // update.Id = id الداخلي في DB.
  // update.MatchId = external match id من المصدر.
  // حدّث scoreHome, scoreAway, status, time في cache.
});

connection.on('ReceiveMatchDetailsUpdate', update => {
  // اعمل refetch لتفاصيل الماتش والإحصائيات والأحداث.
});
```

عند إغلاق الصفحة:

```ts
await connection.invoke('LeaveMatchRoom', match.id);
```

### الرياضات الأخرى live

عند فتح event من `other-sports`:

```ts
await connection.invoke('JoinOtherSportRoom', eventId);

connection.on('ReceiveOtherSportUpdate', payload => {
  // streams أو metadata للـ event اتحدثت؛ اعمل refetch لـ /other-sports/events/{eventId}
});

connection.on('ReceiveOtherSportLiveUpdate', payload => {
  // أضف live update أو اعمل refetch حسب تصميم الصفحة.
});
```

وعند الخروج:

```ts
await connection.invoke('LeaveOtherSportRoom', eventId);
```

### شات وريأكشن وWatch Parties

- Join match room أولًا لتحديثات `ReceiveMatchChatMessage` و`ReceiveMatchReaction`.
- Watch party rooms تستخدم `JoinWatchPartyRoom(code)` و`LeaveWatchPartyRoom(code)`.
- events المتاحة:
  - `ReceiveWatchPartyMessage`
  - `ReceiveWatchPartyReaction`
  - `ReceiveWatchPartySync`

### Fallback polling

حتى مع SignalR، اعمل polling احتياطي كل 30-60 ثانية في صفحات live:

- قائمة المباريات: refetch للـ matches الحالية.
- صفحة ماتش: refetch details.
- صفحة فانتازي: refetch contest details/leaderboard بعد أي score update.
- صفحة توقعات: refetch leaderboard بعد score update.

## 7) Error handling موحد في الفرونت

- `400`: اعرض validation message من `message`.
- `401`: امسح الجلسة أو افتح login.
- `403`: المستخدم ليس owner/admin أو `userId` لا يطابق التوكن.
- `404`: الكود/المسابقة/الدوري غير موجود أو مغلق.
- `500`: اعرض toast عام وسجّل response body في logging.
- `503`: غالبًا إعداد قاعدة البيانات؛ اعرض رسالة صيانة/اتصال.

## 8) Checklist قبل التسليم

- Dashboard يستخدم `/users/{userId}/competitions` أو endpoints المنفصلة حسب الحاجة.
- صفحة Fantasy details تستخدم `/fantasy/contests/{contestId}?userId=...`.
- Player picker يستخدم فقط `/fantasy/tournaments/{tournamentId}/today/players?date=...`.
- Prediction cards تستخدم `lockedForPrediction`.
- SignalR يعيد join للـ rooms بعد reconnect.
- لا يوجد path فيه `/api/api`.
- كل request محمي يرسل Bearer token و`userId` الخاص بنفس المستخدم.
