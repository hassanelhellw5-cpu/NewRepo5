# تكامل الفرونت مع الرياضات الأخرى والـ Formula 1

## مصادر السحب الحالية

- `Scrapers/other_sports_importer.py` يسحب بيانات موحدة للرياضات الأخرى من ESPN، 365Scores، و Formula 1/Jolpica، ثم يثري Formula 1 بروابط الأخبار والسائقين والفرق من Formula1.com، وبعدها يرسلها إلى `POST /api/other-sports/import/events`.
- `Scrapers/other_sports_live_poller.py` هو حل polling عملي للـ live: يشغل importer كل `LIVE_UPDATE_INTERVAL_SECONDS`، يقرأ الأحداث المباشرة، ثم يرسل تحديثات de-duplicated إلى `POST /api/other-sports/events/{eventId}/live-updates`.
- روابط البث لا تُعرض للمستخدم كرابط خارجي خام إلا لو كانت خلف proxy داخلي أو تم تفعيل `Streams:ExposeRawUrls`. لا تضع روابط m3u8 غير مرخصة في الكود؛ استخدم ملف overrides خاص بالبيئة فقط للمصادر التي تملك حق عرضها.

## Endpoints مهمة للفرونت

### قائمة الرياضات

```http
GET /api/other-sports/sports
```

تعرض `sportKey`, الاسم، عدد الأحداث، وأقرب تاريخ متاح.

### أحداث يوم محدد

```http
GET /api/other-sports/events?sportKey=formula1&date=2026-07-23
GET /api/other-sports/events?sportKey=basketball&date=2026-07-23&liveOnly=true
```

استخدمها في صفحات الجدول اليومي. `sportKey` اختياري.

### الأحداث اللايف فقط

```http
GET /api/other-sports/events/live
GET /api/other-sports/events/live?sportKey=formula1
```

استخدمها في home widgets وصفحة live.

### النتائج

```http
GET /api/other-sports/results?sportKey=formula1&from=2026-07-01&to=2026-07-23
```

تعرض أحداث منتهية أو أحداث لديها results.

### تفاصيل حدث واحد

```http
GET /api/other-sports/events/{eventId}
```

يرجع participants، results، streams، و liveUpdates.

### Scoreboard مختصر

```http
GET /api/other-sports/events/{eventId}/scoreboard
```

مناسب لشاشة النتيجة السريعة لأنه يرجع آخر update بالإضافة للنتائج.

### Live updates فقط

```http
GET /api/other-sports/events/{eventId}/live-updates
```

الفرونت يفتح SignalR room للحدث، ويستخدم endpoint ده كـ initial load أو fallback عند reconnect.

### Formula 1 dashboard

```http
GET /api/other-sports/formula1/dashboard
GET /api/other-sports/formula1/news
GET /api/other-sports/formula1/drivers
GET /api/other-sports/formula1/teams
```

يرجع `dashboard`:

- `events`: سباقات الفترة.
- `live`: السباقات المباشرة حسب status المخزنة.
- `recentResults`: آخر النتائج.
- `standingsMetadataJson`: JSON string يحتوي standings المتاحة من scraper + روابط الأخبار والسائقين والفرق الرسمية.

واستخدم endpoints المنفصلة (`news`, `drivers`, `teams`) لو الفرونت عايز arrays جاهزة بدون ما يعمل parse لـ `standingsMetadataJson`.

### اللاعبين/السائقين والبروفايلات

```http
GET /api/other-sports/participants?sportKey=formula1&q=Max
GET /api/other-sports/participants/Max%20Verstappen?sportKey=formula1
GET /api/other-sports/profiles?sportKey=formula1&q=Max
GET /api/other-sports/profiles/{profileId}
GET /api/other-sports/team-profiles?sportKey=formula1&q=Red
GET /api/other-sports/team-profiles/{teamProfileId}
```

- `participants` يرجع البيانات المشتقة مباشرة من events.
- `profiles` و `team-profiles` يرجعوا بروفايلات مخزنة ومتحدثة وقت import: سائقين/لاعبين/فرق/constructors حسب الرياضة.
- افتح `profiles/{profileId}` أو `team-profiles/{teamProfileId}` لعرض البروفايل مع آخر events/results المرتبطة به.

### لاعبي وفرق كرة القدم

```http
GET /api/players?q=محمد
GET /api/players/{playerId}/card
GET /api/players/{playerId}/matches
GET /api/matches/{matchId}/player-cards
```

دي endpoints كرة القدم الموجودة أصلًا: search، كارت اللاعب، ماتشات اللاعب، وكروت لاعيبة الماتش.

## SignalR المقترح

- عند فتح صفحة حدث: join room الخاص بالحدث في `MatchHub` إن كان الفرونت يدعم ذلك.
- استمع إلى:
  - `ReceiveOtherSportUpdate`: تغييرات عامة/streams/import.
  - `ReceiveOtherSportLiveUpdate`: updates جديدة للحدث.
- عند وصول signal، اعمل refetch لـ `scoreboard` أو أضف update محليًا لو payload كافي.

## التشغيل المقترح للسيرفر

### Import عادي كل عدة دقائق

```bash
python Scrapers/run_all.py
```

### Polling live كل 30 ثانية

```bash
API_DOMAIN=https://your-api.example IMPORT_API_KEY=*** LIVE_UPDATE_INTERVAL_SECONDS=30 python Scrapers/other_sports_live_poller.py
```

### One-shot للتجربة

```bash
API_DOMAIN=http://localhost:5000 LIVE_UPDATE_ONCE=1 python Scrapers/other_sports_live_poller.py
```

## ملاحظات مهمة

- الـ polling ليس live push حقيقي من المصدر؛ هو near-live حسب سرعة المصدر الخارجي والـ interval، ولو المصدر لم يرجع lap-by-lap فلن نخترع تفاصيل غير موجودة.
- تفاصيل Formula 1 الدقيقة لحظة بلحظة مثل live timing الكامل، tyre history، sectors، radio، telemetry أو بث الفيديو الرسمي تحتاج مزود بيانات/اشتراك مرخص؛ الكود الحالي يجهز endpoints ويعرض البيانات المتاحة قانونيًا من المصادر العامة/المهيأة.
- لو مطلوب live فعلي لحظة بلحظة، لازم مزود بيانات رسمي يدعم WebSocket أو webhook أو API مرخص سريع.
- أي بث فيديو يجب أن يكون من مصدر تملك حق عرضه. التطبيق سيخفي raw URLs افتراضيًا لحماية المستخدمين والمنصة.
- لا تضف روابط HLS غير مرخصة أو روابط عليها bypass headers داخل الكود أو الـ repo. لو عندك بث Formula 1 مرخص، ضعه في `Scrapers/other_sports_stream_overrides.json` كـ internal proxy URL أو stream مصرح به، وبعدها `GET /api/other-sports/events/{eventId}` سيرجع `streams[].playbackUrl` للفرونت.

## تشغيل بث Formula 1 مرخص عبر نفس فكرة الكورة

لو عندك رابط HLS مرخص، لا تكتبه في الكود. ضعه كـ environment variables على السيرفر:

```bash
FORMULA1_LICENSED_HLS_URL="https://licensed-provider.example/main.m3u8"
FORMULA1_LICENSED_ALT_HLS_URL="https://licensed-provider.example/audio.m3u8"
FORMULA1_LICENSED_STREAM_REFERER="https://licensed-provider.example/"
FORMULA1_LICENSED_STREAM_USER_AGENT="Mozilla/5.0 ..."
STREAM_PROXY_SECRET="your-secret"
STREAM_PROXY_ALLOWED_HOSTS="licensed-provider.example"
```

بعد تشغيل `python Scrapers/run_all.py` سيضيف importer البث إلى سباقات Formula 1 الحالية/القادمة كـ `streams`، والفرونت يقرأ:

```http
GET /api/other-sports/events/{eventId}
```

ثم يشغل `streams[0].playbackUrl` باستخدام HLS player. مثال فرونت مختصر:

```js
import Hls from 'hls.js';

const event = await fetch(`/api/other-sports/events/${eventId}`).then(r => r.json());
const stream = event.streams?.find(s => s.playbackUrl);
if (stream && Hls.isSupported()) {
  const hls = new Hls();
  hls.loadSource(stream.playbackUrl);
  hls.attachMedia(document.querySelector('video'));
}
```

## هل live poller داخل run_all؟

- `run_all.py` يشغل `other_sports_importer.py` تلقائيًا، ويشغل `multisport_selenium_importer.py` تلقائيًا مرة واحدة لو Chrome/Chromium متاح.
- `other_sports_live_poller.py` معمول أساسًا كسيرفس منفصل يفضل شغال كل 30 ثانية أثناء اللايف.
- لو عايز `run_all.py` يشغله one-shot بعد السكريبتات الثقيلة، فعّل:

```bash
ENABLE_OTHER_SPORTS_LIVE_POLLER_ONCE=true python Scrapers/run_all.py
```
