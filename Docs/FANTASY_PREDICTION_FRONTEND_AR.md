# تشغيل الفانتازي ودوري التوقعات على الفرونت إند

## الحالة الحالية المختصرة

- إنشاء **دوري التوقعات** متاح لأي مستخدم مسجل؛ `OwnerUserId` لازم يطابق المستخدم الحالي أو يكون أدمن.
- إنشاء **Fantasy contest** متاح الآن لأي مستخدم مسجل؛ `OwnerUserId` لازم يطابق المستخدم الحالي أو يكون أدمن.
- تحويل الفانتازي أو دوري التوقعات إلى **Knockout** يتم بواسطة صاحب الدوري/المسابقة أو الأدمن فقط، وليس أي عضو عادي.
- إحصائيات المباراة موجود لها جدول و endpoint استيراد، والسكريبر يحاول يسحبها من صفحة تفاصيل يلا كورة، لكنها best-effort وليست مضمونة لكل ماتش.
- فيديوهات الملخصات موجود لها موديل و APIs وسكريبر حالي يعتمد على يلا كورة ومصادر highlights عامة؛ تطبيق يلاشوت ليس مدمجًا كمصدر حاليًا.

## 1) Match statistics: المطلوب والجاهز

### الجاهز في الباك إند

- الداتا مخزنة في `MatchStatistics`، وممنوع تكرار نفس اسم الإحصائية لنفس الماتش عن طريق unique index على `MatchId + Name`.
- السكريبر `YallaKoraEngine.get_match_details` يرجع `events` و `stats`، ويستخرج قيم مثل الاستحواذ أو التسديدات لو الصفحة فيها بلوك إحصائيات واضح.
- `live_match_updater.py` يحدث تفاصيل المباريات اللايف والمنتهية ويرسل `match-details` عندما يجد events أو stats.

### المطلوب تحسينه في phase الإحصائيات

1. إضافة مصدر أقوى لإحصائيات المباراة لو يلا كورة لا يعرضها دائمًا، مثل Sofascore أو API رياضي رسمي/مدفوع.
2. توحيد أسماء الإحصائيات للفرونت، مثل:
   - `possession`
   - `shots`
   - `shotsOnTarget`
   - `corners`
   - `yellowCards`
   - `redCards`
   - `fouls`
3. إضافة endpoint قراءة واضح لو غير موجود في شاشة الفرونت، أو استخدام تفاصيل الماتش الموجودة حاليًا إن كانت ترجع `statistics`.
4. في الواجهة: اعرض skeleton ثم message مثل `الإحصائيات غير متاحة لهذا الماتش حتى الآن` بدل مساحة فاضية.

## 2) Highlights من يلاشوت

يلاشوت هنا **تطبيق موبايل وليس موقع ويب**. لذلك التكامل الحالي ليس scraping لدومين ويب وهمي؛ السكريبر لا يحاول يفتح موقع باسم يلاشوت. بدل ذلك يدعم مصدر اختياري باسم `YallaShootApp` يتفعل فقط لو تم ضبط endpoint حقيقي/مسموح من التطبيق في env باسم `YALLASHOOT_APP_API_URL`. الموجود بجانبه هو سكريبر فيديوهات يلا كورة + مصادر highlights عامة، وبعدها يتم حفظ الفيديوهات في `MatchVideos` وتعرض عبر endpoints الفيديو.

### تشغيل مصدر يلاشوت التطبيق

- اضبط `YALLASHOOT_APP_API_URL` بقالب URL من الـ app/API الحقيقي ويدعم placeholders: `{match_id}`, `{home}`, `{away}`, `{query}`.
- لو الـ endpoint يحتاج توكن اضبط `YALLASHOOT_APP_TOKEN`.
- السكريبر يتوقع JSON إما array مباشرة أو object فيه `videos`.
- كل فيديو ممكن يحتوي: `id`, `title`, `description`, `video_url`/`videoUrl`/`url`, `embed_url`/`embedUrl`, `thumbnail_url`/`thumbnailUrl`, `published_at`.
- السكريبر يحولها لنفس payload الحالي:

```json
{
  "match_id": "external-match-id",
  "videos": [
    {
      "external_id": "YallaShootApp:123",
      "title": "ملخص المباراة",
      "video_url": "https://...",
      "embed_url": "https://...",
      "thumbnail_url": "https://...",
      "type": "Summary",
      "source": "YallaShootApp"
    }
  ]
}
```

- استخدم نفس endpoint الاستيراد: `POST /api/SportsData/import/match-videos`.
- لو `YALLASHOOT_APP_API_URL` غير مضبوط، السكريبر يكمل عادي بمصادر الفيديو الأخرى من غير فشل.
- في الفرونت اعرض تبويب Highlights من `GET /api/SportsData/match-videos/{matchId}`، ولو مفيش فيديوهات اعرض CTA بسيط: `الملخصات هتظهر بعد نهاية الماتش`.

## 3) Fan engagement: ما الجاهز وما يحتاج تظبيط

### جاهز ومفيد للمستخدم المدفوع/النشط

- متجر cosmetics عام مع فلاتر type/category/team/match/limited.
- متجر عروض محدودة.
- اشتراك supporter بالكوينز.
- premium profile يعرض حالة الاشتراك، الثيم، البادج، ملخص التوقعات، الدوريات، والـ cosmetics المملوكة.
- fan pass للماتش كـ digital collectible.
- طلب بطولات مخصصة.
- شراء وتجهيز cosmetics.
- cheers جاهزة.
- شات الماتش + pin message مدفوع بالكوينز.
- reactions لحظية عبر SignalR.
- live-readiness لمعرفة هل الماتش عنده stream/chat جاهزين.

### محتاج تظبيط قبل البيع بقوة

- ربط الدفع النقدي الحقيقي بـ cosmetics ذات `UnlockType = Money`؛ endpoint الشراء الحالي يرفضها ويطلب payment flow.
- أدوات moderation للشات والـ reactions.
- شاشة admin لإضافة limited cosmetics وربطها بماتشات حقيقية.
- Analytics للـ fan engagement: عدد الشراء، معدل استخدام badges، أكثر ماتشات عليها chat، revenue من pinned comments/fan passes.
- تجربة fallback لو مفيش stream أو highlights.

## 4) Fantasy frontend flow

### إنشاء مسابقة فانتازي

`POST /api/fantasy/contests` مع Bearer token.

```json
{
  "tournamentId": 1,
  "contestDate": "2026-07-20T00:00:00Z",
  "name": "Fantasy الأصدقاء",
  "ownerUserId": "current-user-id",
  "isPublic": false,
  "maxMembers": 20,
  "format": 0
}
```

ملاحظات:
- `format = 0` Leaderboard.
- `format = 1` Knockout من البداية.
- احفظ `code` واعرضه لصاحب المسابقة للمشاركة.

### الانضمام

`POST /api/fantasy/contests/join`

```json
{ "userId": "current-user-id", "code": "ABC123" }
```

### جلب اللاعبين المتاحين

`GET /api/fantasy/tournaments/{tournamentId}/today/players?date=2026-07-20`

استخدم الرد لبناء player picker. اعرض:
- اسم اللاعب.
- الفريق.
- المركز.
- رقم القميص لو موجود.

### حفظ تشكيلة 5 لاعبين

`POST /api/fantasy/contests/{contestId}/entries`

```json
{ "userId": "current-user-id", "playerIds": [1, 2, 3, 4, 5] }
```

قواعد الواجهة:
- امنع اختيار أكثر أو أقل من 5.
- امنع تكرار اللاعب.
- اقفل التعديل بعد بداية أول ماتش في اليوم لو هتطبق locking UX؛ الباك إند لا يقفل pick حسب وقت المباراة حاليًا.

### ترتيب الفانتازي

`GET /api/fantasy/contests/{contestId}/leaderboard`

اعرض: rank، userName، totalPoints، picks، knockout state.

### مسابقاتي

`GET /api/fantasy/contests/my?userId=current-user-id`

استخدمه في dashboard يعرض المسابقات التي دخلها المستخدم، picks، points، format، وcode.

### التحويل إلى Knockout

`POST /api/fantasy/contests/{contestId}/convert-to-knockout`

```json
{ "winnerBonusPoints": 100 }
```

يظهر الزر فقط لو:
- المستخدم هو `OwnerUserId` أو أدمن.
- عدد المشاركين 2 أو أكثر.
- المسابقة ليست knockout بالفعل أو لم تبدأ أدوار متقدمة.

### تقدم دور Knockout

`POST /api/fantasy/contests/{contestId}/advance-knockout-round`

اعرضه لصاحب المسابقة بعد انتهاء حساب نقاط الجولة.

### عرض bracket

`GET /api/fantasy/contests/{contestId}/knockout`

ابنِ bracket من `bracket[].round` و `members`، واظهر champion لو موجود.

## 5) Prediction league frontend flow

### إنشاء دوري توقعات

`POST /api/PredictionLeagues`

```json
{
  "ownerUserId": "current-user-id",
  "name": "توقعات صحابي",
  "isPublic": false,
  "format": 0,
  "maxMembers": 50,
  "startsAt": "2026-07-20T00:00:00Z",
  "endsAt": "2026-08-20T00:00:00Z"
}
```

### الانضمام بالكود

`POST /api/PredictionLeagues/join`

```json
{ "userId": "current-user-id", "code": "ABC123" }
```

### مباريات الدوري وإظهار توقع المستخدم

`GET /api/PredictionLeagues/{leagueId}/matches?userId=current-user-id&from=2026-07-20&to=2026-08-03`

الرد يحتوي `lockedForPrediction` و `prediction` لكل ماتش. في الواجهة:
- لو `lockedForPrediction = true` اقفل input.
- لو `prediction` موجود اعرض التوقع الحالي وإمكانية تعديله قبل القفل.

### إرسال توقع

`POST /api/PredictionLeagues/{leagueId}/predict`

```json
{
  "userId": "current-user-id",
  "matchId": 123,
  "predictedHomeScore": 2,
  "predictedAwayScore": 1
}
```

### Leaderboard

`GET /api/PredictionLeagues/{leagueId}/leaderboard`

### Analytics

`GET /api/PredictionLeagues/{leagueId}/analytics`

لو `PremiumAnalyticsUnlocked = false` ستظهر `topMembers` و totals فقط، و`predictionRows` تكون فارغة مع upsell. لو true اعرض breakdown لكل مستخدم.

### تخصيص الدوري وفتح premium analytics

`POST /api/PredictionLeagues/{leagueId}/customization`

```json
{
  "userId": "current-user-id",
  "themePalette": "DerbyNight",
  "coverImageUrl": "https://...",
  "trophyName": "كأس القمة",
  "unlockPremiumAnalytics": true,
  "priceCoins": 50
}
```

### Knockout لدوري التوقعات

- `POST /api/PredictionLeagues/{leagueId}/convert-to-knockout`
- `POST /api/PredictionLeagues/{leagueId}/advance-knockout-round`
- `GET /api/PredictionLeagues/{leagueId}/knockout`

نفس شروط الواجهة: الزر يظهر لصاحب الدوري أو الأدمن فقط، ومع وجود عضوين على الأقل.

## 6) UX checklist مهم

- كل create/join/pick/predict يحتاج Auth token و `userId` مطابق للمستخدم الحالي.
- اعرض code واضح مع زر copy/share.
- اعمل empty states محترمة: لا لاعبين، لا مباريات، لا فيديوهات، لا إحصائيات.
- أظهر badges: Owner، Member، Knockout، Eliminated، Champion.
- لا تعرض زر knockout لكل الأعضاء؛ فقط لصاحب الدوري أو الأدمن.
- في prediction cards اعرض deadline: قبل بداية الماتش أو قبل حالة live/finished.
- في الفانتازي اعرض تحذير أن اللاعبين يعتمدون على آخر lineups/scorers المتاحة وقد لا يكون roster رسمي 100%.

## 7) SignalR للنتيجة اللحظية وربط شاشة الماتش

### الاتصال

الـ Hub متسجل على `/matchHub`. في الفرونت استخدم `@microsoft/signalr`:

```ts
import * as signalR from '@microsoft/signalr';

const connection = new signalR.HubConnectionBuilder()
  .withUrl(`${API_BASE_URL}/matchHub`, {
    accessTokenFactory: () => authToken ?? ''
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

### شاشة قائمة المباريات

استمع للحدث العام:

```ts
connection.on('ReceiveMatchUpdate', payload => {
  // payload.status مثال: Matches Updated / Match Details Updated / Match Videos Updated
  // لو payload.matches موجود، حدّث score/status/time للماتشات المعروضة بدون refresh كامل.
});
```

### شاشة تفاصيل ماتش واحد

بعد فتح صفحة ماتش داخلي `match.id`، ادخل room الماتش:

```ts
await connection.invoke('JoinMatchRoom', match.id);

connection.on('ReceiveMatchScoreUpdate', update => {
  // update.Id هو id الداخلي في قاعدة البيانات.
  // update.MatchId هو external YallaKora match_id.
  // حدّث scoreHome/scoreAway/status/time فورًا.
});

connection.on('ReceiveMatchDetailsUpdate', update => {
  // بعد وصوله اعمل refetch لـ GET /api/SportsData/match-details/{externalMatchId}
  // أو استخدم counts لعرض badge إن فيه events/stats جديدة.
});
```

عند الخروج من الصفحة:

```ts
await connection.invoke('LeaveMatchRoom', match.id);
```

### شات وريأكشن الماتش

نفس room الماتش يستقبل:

- `ReceiveMatchChatMessage` بعد إرسال رسالة من `POST /api/fan-engagement/matches/{matchId}/chat`.
- `ReceiveMatchReaction` بعد إرسال reaction من `POST /api/fan-engagement/matches/{matchId}/reactions`.

مهم: endpoints الشات والريأكشن تستخدم `matchId` الداخلي، بينما `GET /api/SportsData/match-details/{matchId}` و `GET /api/SportsData/match-videos/{matchId}` يستخدمان `MatchId` الخارجي القادم من يلا كورة.

## 8) run_all.py و live_match_updater.py

`run_all.py` يشغل الآن `live_match_updater.py` بعد import مباريات يلا كورة لكل يوم، مع `LIVE_UPDATE_ONCE=1` و`LIVE_UPDATE_DATE` بنفس تاريخ الدورة. الهدف إن نفس process يجلب تفاصيل/إحصائيات الماتش ويعمل import عبر `/api/SportsData/import/match-details` من غير ما يدخل في loop لا نهائي.

- يمكن تعطيله بـ `ENABLE_LIVE_MATCH_UPDATER=0`.
- في profile `full` يترك `LIVE_UPDATE_ENABLE_SELENIUM_STATS=1` افتراضيًا عشان يلتقط إحصائيات AJAX.
- في profile `light` يتم ضبط `LIVE_UPDATE_ENABLE_SELENIUM_STATS=0` افتراضيًا لتفادي تكلفة Chrome في sync الخفيف.
- الـ background worker كان أصلًا يشغل `live_match_updater.py` في live cycle؛ الإضافة هنا لضمان إن التشغيل اليدوي/المجدول عبر `run_all.py` يحدّث الإحصائيات أيضًا.

## 9) مصدر YallaShootApp: هل لازم `YALLASHOOT_APP_API_URL` و`YALLASHOOT_APP_TOKEN`؟

نعم، لو هدفك تجيب فيديوهات من **تطبيق يلاشوت نفسه** فـ `YALLASHOOT_APP_API_URL` لازم يكون موجود؛ من غيره السكريبر يتخطى مصدر `YallaShootApp` ويكمل بالمصادر الأخرى مثل يلا كورة ومواقع الهايلايت العامة. `YALLASHOOT_APP_TOKEN` اختياري ولا تحتاجه إلا لو endpoint التطبيق يطلب Authorization header.

### أجيب القيم دي منين؟

- من فحص قانوني ومصرّح لترافيك التطبيق أو من API documentation/partner access لو متاحة من صاحب التطبيق.
- لو عندك جهاز اختبار وتملك الحق في الفحص، افتح التطبيق عبر proxy/debugging setup وسجل request المسؤول عن قائمة الفيديوهات أو تفاصيل ماتش، ثم خذ URL وحوله لقالب يستخدم placeholders مثل `{match_id}` أو `{home}` و`{away}`.
- لو request فيه header مثل `Authorization: Bearer ...` أو token ثابت/جلسة، ضعه في `YALLASHOOT_APP_TOKEN`. لو الفيديوهات public ولا يوجد authorization، اتركه فارغًا.
- لا تضع التوكن في الكود أو git؛ ضعه في environment variables على السيرفر فقط.

مثال تشغيل:

```bash
export YALLASHOOT_APP_API_URL='https://api.example.com/highlights?match={match_id}&q={query}'
export YALLASHOOT_APP_TOKEN='optional-token-if-required'
python Scrapers/yallakora_video_scraper.py
```

لو لا يوجد endpoint رسمي أو مسموح، الأفضل الاعتماد على يلا كورة ومصادر الهايلايت العامة إلى أن يتوفر مصدر موثوق، لأننا لا نقدر نخمن API تطبيق موبايل من غير فحص فعلي للترافيك. عمليًا السكريبر أصبح يضيف fallback عام بدون مفاتيح عبر YouTube search بكلمات مثل `يلا شوت ملخص` و`يلا شوت أهداف` بجانب FootyRoom/HooFoot/DasFootball، فحتى لو مصدر التطبيق نفسه غير متاح، عندنا محاولة تلقائية أوسع لجلب highlights من الويب العام.

## 6) تحديثات مهمة لتفادي أخطاء الفرونت الحالية

### كل مسابقات المستخدم في endpoint واحد

لو الداشبورد محتاج يعرض كل الدوريات/المسابقات التي دخلها المستخدم سواء توقعات أو فانتازي، استخدم:

`GET /api/users/{userId}/competitions`

الرد يحتوي:
- `predictionLeagues`: دوريات التوقعات التي المستخدم عضو فيها.
- `fantasyContests`: مسابقات الفانتازي التي المستخدم عضو فيها.

وما زالت endpoints التفصيلية متاحة لو الصفحة محتاجة نوع واحد فقط:
- `GET /api/PredictionLeagues/my?userId=current-user-id`
- `GET /api/fantasy/contests/my?userId=current-user-id`

### لاعيبة الفانتازي لازم تيجي من ماتشات اليوم وليس قائمة عامة

استخدم فقط:

`GET /api/fantasy/tournaments/{tournamentId}/today/players?date=YYYY-MM-DD`

مهم للفرونت:
- لا تستخدم endpoint عام للاعبين في شاشة اختيار الفانتازي.
- لو `playerSource = "today-match-lineups"` فالرد مبني على lineups الماتشات الموجودة لنفس التاريخ، وده أفضل وضع.
- لو `playerSource = "recent-team-lineups-and-scorers-fallback"` فهذا fallback لأن lineups ماتش اليوم غير متاحة بعد؛ اعرض تنبيه بسيط للمستخدم أن القائمة مبنية على آخر قوائم الفرق.
- لو `players` فاضية، اعرض رسالة: `لا توجد قوائم لاعبين متاحة لهذه المباريات حتى الآن` بدل اختيار لاعبين عشوائيين.

### الرياضات الأخرى

الصفحة التي كانت تطلب `/api/other-sports/sports` يمكنها الآن استخدام:

`GET /api/other-sports/sports`

ولجلب أحداث يوم معين:

`GET /api/other-sports/events?date=YYYY-MM-DD&sportKey=tennis`

ملاحظات للفرونت:
- لا ترسل مسار مكرر مثل `/api/api/...`.
- لو Vite proxy مضبوط على `/qemma-api -> backend` فالاستدعاء الصحيح من المتصفح يكون `/qemma-api/api/other-sports/sports`، والباك إند يستقبلها كـ `/api/other-sports/sports` بعد إزالة prefix من proxy.
- اعرض رسالة خطأ واضحة لو رجع 503 من قاعدة البيانات؛ ده غالبًا إعداد connection string وليس مشكلة UI.
