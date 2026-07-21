# دليل كامل لكل APIs والـ Endpoints - Qemma Backend

> الشرح ده معمول للفرونت، الداشبورد، ومطورين الباك إند. أي endpoint مكتوب عليه **Import / Admin / Job** مش المفروض المستخدم العادي يستعمله من الويب سايت.

## 0) التشغيل والسحب بيحصلوا إزاي؟

- الباك إند ASP.NET Core بيشتغل من `Program.cs` وبيسجل `EnhancedApiSyncWorker` كـ Hosted Service، يعني لما السيرفر يفتح، عامل السحب بيشتغل في الخلفية تلقائيًا لو `SportsSync:Enabled=true`.
- العامل الخلفي يعمل:
  - Full sync كل `SportsSync:IntervalSeconds`، الافتراضي 600 ثانية.
  - Live sync كل `SportsSync:LiveIntervalSeconds`، الافتراضي 60 ثانية.
- نتيجة الماتش بتتحدث من `live_match_updater.py` داخل الـ live sync. بعد التحديث الباك إند يبعت SignalR event باسم `ReceiveMatchUpdate` على `/matchHub`، فالفرونت يقدر يحدث الشاشة بدون refresh.
- لا، مش المفروض كل مرة تعمل run يدوي. في الإنتاج شغّل الـ backend كـ service دائم، والـ Hosted Worker هيكرر السحب تلقائيًا.

### إعدادات مهمة للإنتاج

```json
{
  "ApiDomain": "https://your-api-domain.com",
  "SportsSync": {
    "Enabled": true,
    "PythonPath": "python3",
    "IntervalSeconds": 600,
    "LiveIntervalSeconds": 60,
    "EnableF1MotorsportImport": true,
    "EnableUnifiedLiveLinker": true,
    "EnableSeleniumMultiSportImport": true
  }
}
```

Environment variables المهمة للسكريبرز:

- `API_DOMAIN`: دومين الباك إند اللي السكريبرز هتبعت عليه.
- `IMPORT_API_KEY`: مفتاح حماية import endpoints لو متفعل.
- `OTHER_SPORTS_SOURCE=thesportsdb`: مصدر الرياضات الأخرى العام.
- `THESPORTSDB_SPORTS=basketball,tennis,motorsport,ice_hockey,baseball,american_football`.
- `STREAM_PROXY_SECRET`: توقيع روابط HLS.
- `STREAM_PROXY_ALLOWED_HOSTS`: الدومينات المسموح للبروكسي يسحب منها.

## 1) حالة المصادر اللي اتطلبت

### كرة القدم / الأخبار العربية
- الموجود فعليًا: YallaKora scraper بيسحب مباريات وأخبار، والتعديل الحالي خلى الأخبار تدخل صفحة الخبر وتسحب تفاصيل كاملة.
- موجود endpoints للتفاصيل، التشكيل، أحداث الماتش، الفيديوهات، الجداول، الهدافين، واللايفات.

### التنس
- الموجود فعليًا في الكود: `other_sports_importer.py` يقدر يسحب live scores من TheSportsDB لو `THESPORTSDB_SPORTS` فيها `tennis`.
- مش معمول direct scraper مخصوص لـ ATP Tour أو WTA في الكود الحالي.
- Flashscore/Sofascore محتاجين Selenium/PuppeteerSharp بسبب Cloudflare/JS؛ مش معمول bypass ليهم في الكود الحالي.

### Formula 1
- الموجود فعليًا: `f1_motorsport_importer.py` يسحب جدول وأخبار/updates من Motorsport.com عند تفعيل `SportsSync:EnableF1MotorsportImport=true`.
- الموقع الرسمي Formula1.com مش معمول له scraper مباشر حاليًا.

### اللايفات للرياضات الأخرى
- النظام مش بيسرق روابط بث من مصادر محمية. اللايفات للرياضات الأخرى بتدخل عن طريق:
  - `other_sports_stream_overrides.json` لو عندك لينكات رسمية/مملوكة.
  - `unified_live_stream_linker.py` لو مفعّل وموصل القنوات بقواعد `other_sports_live_rules.json`.

## 2) Authentication API

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | Web/App | تسجيل مستخدم جديد. |
| POST | `/api/auth/login` | Web/App | تسجيل دخول ويرجع JWT. |
| GET | `/api/auth/me` | Web/App Auth | بيانات المستخدم الحالي من التوكن. |

## 3) SportsData API - كرة قدم وأخبار وفيديوهات

### Public / Frontend

| Method | Endpoint | Query/Body | يستخدم فين |
| --- | --- | --- | --- |
| GET | `/api/SportsData/matches` | `date`, `liveOnly`, `team`, `tournament`, `status`, `q`, `hasVideos`, `hasStreams`, `hasDetails` | صفحة المباريات والبحث والفلترة. |
| GET | `/api/SportsData/tournaments` | — | قائمة البطولات. |
| GET | `/api/SportsData/coverage` | — | إحصائيات تغطية البيانات والصور والفيديوهات. |
| GET | `/api/SportsData/news` | — | قائمة الأخبار. |
| GET | `/api/SportsData/news/{id}` | route id | صفحة تفاصيل الخبر. |
| GET | `/api/SportsData/match-details/{matchId}` | route matchId | صفحة تفاصيل الماتش: أحداث/إحصائيات/تشكيل. |
| GET | `/api/SportsData/match-videos/{matchId}` | route matchId | فيديوهات الماتش. |
| GET | `/api/SportsData/match-videos/{videoId}/playback` | route videoId | تشغيل فيديو عبر الباك إند. |
| GET | `/api/SportsData/match-videos/{videoId}/download` | route videoId | تحميل/تمرير الفيديو. |
| GET | `/api/SportsData/squad/{matchId}` | route matchId | التشكيل والبدلاء والمدربين. |
| GET | `/api/SportsData/standings/{tournamentId}` | route tournamentId | ترتيب بطولة. |
| GET | `/api/SportsData/scorers/{tournamentId}` | route tournamentId | هدافي بطولة. |
| GET | `/api/SportsData/bracket/{tournamentId}` | route tournamentId | أدوار خروج المغلوب. |
| GET | `/api/SportsData/proxy/hls` | `url`, `expires`, `sig`, optional `referer`, `userAgent` | تشغيل HLS من مصادر مسموحة. |

### Admin / Jobs / Scrapers

| Method | Endpoint | يستخدم فين |
| --- | --- | --- |
| GET | `/api/SportsData/check` | Health check وإحصائيات DB للأدمن. |
| POST | `/api/SportsData/import/matches` | يلا كورة/سكريبر المباريات يدخل البطولات والماتشات. |
| POST | `/api/SportsData/import/news` | سكريبر الأخبار يدخل أخبار كاملة. |
| POST | `/api/SportsData/import/streams` | سكريبر اللايفات يربط streams بماتشات كرة القدم. |
| GET | `/api/SportsData/proxy/check` | فحص رابط بروكسي/ستريم. |
| POST | `/api/SportsData/import/match-details` | سكريبر أحداث وإحصائيات الماتش. |
| POST | `/api/SportsData/import/match-videos` | سكريبر فيديوهات الماتش. |
| POST | `/api/SportsData/import/squad` | سكريبر تشكيل الماتش. |
| POST | `/api/SportsData/import/tournament-details/{tournamentId}` | سكريبر ترتيب/هدافين/برacket للبطولة. |

## 4) Other Sports API - تنس / F1 / رياضات أخرى

| Method | Endpoint | Query/Body | مين يستخدمه |
| --- | --- | --- | --- |
| GET | `/api/other-sports/events` | `sportKey`, `date`, `liveOnly` | الفرونت: قائمة تنس/F1/أي رياضة. |
| GET | `/api/other-sports/events/{eventId}` | route eventId | الفرونت: تفاصيل حدث رياضي، نتائج، مشاركين، live updates، streams. |
| POST | `/api/other-sports/import/events` | list of events | Jobs فقط: إدخال أحداث من TheSportsDB أو feed خاص. |
| POST | `/api/other-sports/events/{eventId}/streams` | list streams | Jobs فقط: ربط live streams لحدث رياضي. |
| POST | `/api/other-sports/events/{eventId}/live-updates` | list updates | Jobs فقط: تحديثات live/news للحدث. |

أمثلة:

- تنس اليوم: `GET /api/other-sports/events?sportKey=tennis&date=2026-07-12`
- F1: `GET /api/other-sports/events?sportKey=formula1`
- لايف فقط: `GET /api/other-sports/events?sportKey=tennis&liveOnly=true`

## 5) Admin API

كل endpoints دي للأدمن فقط:

| Method | Endpoint | الغرض |
| --- | --- | --- |
| GET | `/api/admin/scraping/status` | حالة السحب والتغطية. |
| GET | `/api/admin/scraping/video-coverage` | تغطية الفيديوهات. |
| GET | `/api/admin/scraping/logo-coverage` | تغطية لوجوهات الفرق/البطولات. |
| GET | `/api/admin/payments/stats` | إحصائيات الدفع. |
| GET | `/api/admin/payments/manual` | طلبات الدفع اليدوي. |
| POST | `/api/admin/payments/manual/{requestId}/approve` | قبول دفع يدوي. |
| POST | `/api/admin/payments/manual/{requestId}/reject` | رفض دفع يدوي. |
| GET | `/api/admin/users` | قائمة المستخدمين. |
| POST | `/api/admin/users/{userId}/coins/adjust` | تعديل رصيد كوينز. |
| POST | `/api/admin/users/{userId}/roles` | تعديل أدوار مستخدم. |
| GET | `/api/admin/cosmetics` | إدارة cosmetics. |
| POST | `/api/admin/cosmetics` | إنشاء cosmetic. |
| PUT | `/api/admin/cosmetics/{cosmeticId}` | تعديل cosmetic. |
| GET | `/api/admin/custom-tournaments/requests` | طلبات بطولات مخصصة. |
| POST | `/api/admin/custom-tournaments/requests/{requestId}/status` | تغيير حالة طلب بطولة. |
| POST | `/api/admin/cosmetics/limited-events` | إنشاء limited store event. |

## 6) Coins / Payments API

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| GET | `/api/coins/wallet/{userId}` | Auth | محفظة المستخدم. |
| GET | `/api/coins/transactions/{userId}` | Auth | سجل معاملات الكوينز. |
| POST | `/api/coins/payments/manual` | Auth | رفع طلب دفع يدوي. |
| GET | `/api/coins/payments/manual` | Admin | طلبات الدفع اليدوي. |
| POST | `/api/coins/payments/manual/{requestId}/approve` | Admin | قبول دفع. |
| POST | `/api/coins/payments/manual/{requestId}/reject` | Admin | رفض دفع. |
| POST | `/api/coins/rewards/video/complete` | Public | مكافأة مشاهدة فيديو. |

## 7) Fan Engagement API

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| GET | `/api/fan-engagement/cosmetics` | Public | عرض cosmetics. |
| GET | `/api/fan-engagement/events/limited-store` | Public | عروض المتجر المحدودة. |
| POST | `/api/fan-engagement/supporter/subscribe` | Auth | اشتراك supporter. |
| GET | `/api/fan-engagement/profile/{userId}/premium` | Public | حالة premium/profile. |
| POST | `/api/fan-engagement/matches/{matchId}/fan-pass` | Auth | شراء/تفعيل fan pass. |
| POST | `/api/fan-engagement/custom-tournaments/requests` | Auth | طلب بطولة مخصصة. |
| POST | `/api/fan-engagement/cosmetics/{cosmeticId}/purchase` | Auth | شراء cosmetic. |
| POST | `/api/fan-engagement/cosmetics/{cosmeticId}/equip` | Auth | تجهيز cosmetic. |
| GET | `/api/fan-engagement/cheers` | Public | قائمة cheers. |
| POST | `/api/fan-engagement/matches/{matchId}/chat` | Auth | إرسال رسالة شات ماتش. |
| GET | `/api/fan-engagement/matches/{matchId}/chat` | Public | قراءة شات ماتش. |
| GET | `/api/fan-engagement/matches/{matchId}/live-readiness` | Public | جاهزية اللايف/الشات/الفان features. |

## 8) Fantasy API

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| POST | `/api/fantasy/players/import` | Admin | استيراد لاعبين fantasy. |
| POST | `/api/fantasy/contests` | Admin | إنشاء contest. |
| POST | `/api/fantasy/contests/join` | Auth | انضمام contest. |
| GET | `/api/fantasy/tournaments/{tournamentId}/today/players` | Public | لاعبين بطولة اليوم. |
| POST | `/api/fantasy/contests/{contestId}/entries` | Auth | إرسال تشكيلة. |
| POST | `/api/fantasy/contests/{contestId}/score` | Admin | حساب نقاط contest. |
| GET | `/api/fantasy/contests/{contestId}/leaderboard` | Public | ترتيب contest. |
| GET | `/api/fantasy/contests/code/{code}` | Public | contest بالكود. |
| POST | `/api/fantasy/contests/{contestId}/convert-to-knockout` | Auth | تحويل knockout. |
| POST | `/api/fantasy/contests/{contestId}/advance-knockout-round` | Auth | تقدم دور knockout. |
| GET | `/api/fantasy/contests/{contestId}/knockout` | Public | عرض knockout bracket. |

## 9) Prediction Leagues API

Route base: `/api/PredictionLeagues`

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| POST | `/api/PredictionLeagues` | Auth | إنشاء دوري توقعات. |
| POST | `/api/PredictionLeagues/join` | Auth | انضمام دوري. |
| GET | `/api/PredictionLeagues/{leagueId}/leaderboard` | Public | ترتيب الدوري. |
| POST | `/api/PredictionLeagues/{leagueId}/customization` | Auth | تخصيص الدوري. |
| GET | `/api/PredictionLeagues/{leagueId}/analytics` | Public | تحليلات الدوري. |
| POST | `/api/PredictionLeagues/{leagueId}/convert-to-knockout` | Auth | تحويل knockout. |
| POST | `/api/PredictionLeagues/{leagueId}/advance-knockout-round` | Auth | تقدم دور knockout. |
| GET | `/api/PredictionLeagues/{leagueId}/knockout` | Public | عرض bracket. |

## 10) Watch Parties API

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| POST | `/api/watch-parties` | Auth | إنشاء watch party. |
| GET | `/api/watch-parties/{code}` | Public | فتح watch party بالكود. |
| POST | `/api/watch-parties/{code}/join` | Auth | الانضمام. |
| POST | `/api/watch-parties/{code}/messages` | Auth | إرسال رسالة. |
| GET | `/api/watch-parties/{code}/messages` | Public | قراءة الرسائل. |

## 11) Notifications API

> بعض routes هنا مكتوبة كاملة داخل attributes ومش تحت route base موحد.

| Method | Endpoint | الغرض |
| --- | --- | --- |
| POST | `/api/users/favorite-teams` | إضافة/تحديث الفرق المفضلة. |
| POST | `/api/notifications/push-subscriptions` | تسجيل push subscription. |
| DELETE | `/api/notifications/push-subscriptions/{id}` | حذف push subscription. |
| GET | `/api/notifications` | إشعارات المستخدم. |
| POST | `/api/notifications/{id}/read` | تعليم إشعار كمقروء. |
| PUT | `/api/notifications/preferences` | تفضيلات الإشعارات. |

## 12) Players / Analytics / Summaries / Uploads / Gamification

| Method | Endpoint | مين يستخدمه | الغرض |
| --- | --- | --- | --- |
| GET | `/api/players/{playerId}/card` | Public | بطاقة لاعب. |
| PUT | `/api/players/{playerId}/image` | Admin | تعديل صورة لاعب. |
| GET | `/api/matches/{matchId}/player-cards` | Public | كروت لاعبين ماتش. |
| GET | `/api/analytics/matches/{matchId}/momentum` | Public | momentum للماتش. |
| GET | `/api/matches/{matchId}/summaries` | Public | ملخصات ماتش. |
| POST | `/api/matches/{matchId}/summaries/generate` | Admin | توليد ملخص. |
| POST | `/api/uploads/receipts` | Auth | رفع إيصال دفع. |
| POST | `/api/gamification/streaks/{userId}/touch` | Auth | تحديث streak. |
| GET | `/api/gamification/streaks/{userId}` | Auth | قراءة streak. |
| GET | `/api/gamification/achievements/{userId}` | Auth | إنجازات المستخدم. |

## 13) SignalR

- Hub: `/matchHub`
- Events مهمة للفرونت:
  - `ReceiveMatchUpdate`: تحديث مباريات/نتائج/سحب كامل أو live.
  - `ReceiveOtherSportUpdate`: تحديث رياضات أخرى.
  - `ReceiveOtherSportLiveUpdate`: تحديث live event لرياضة أخرى.

## 14) إيه الناقص من وجهة نظري كمستخدم للنظام؟

1. Scraper مباشر ومخصص لـ ATP/WTA لو عايز تغطية تنس احترافية بدل الاعتماد على TheSportsDB فقط.
2. مصدر بيانات مدفوع أو رسمي للنتائج live لو الدقة مهمة جدًا؛ scraping من مواقع محمية ممكن يتكسر.
3. Dashboard تعرض حالة آخر sync، آخر خطأ، عدد الأخبار الفاضية، وعدد الأحداث بدون streams.
4. Queue/Job dashboard زي Hangfire أو Quartz لو عايز تحكم أفضل من HostedService فقط.
5. Monitoring/alerts: لو scraper فشل أو عدد الأخبار صفر يتبعت تنبيه.
6. Caching للـ public endpoints وتقليل ضغط DB.
7. E2E tests للـ frontend مع SignalR عشان تتأكد النتيجة بتتحدث بدون refresh.

## 15) سكريبر Selenium للمصادر اللي اتطلبت

اتضاف سكريبر جديد: `Scrapers/selenium_multisport_importer.py`.

### بيحاول يسحب منين؟

- Tennis:
  - ATP Tour من `ATP_TOUR_URL` أو الافتراضي `https://www.atptour.com/en/scores/current`.
  - WTA من `WTA_URL` أو الافتراضي `https://www.wtatennis.com/scores`.
  - Flashscore Tennis من `FLASHSCORE_TENNIS_URL`.
  - Sofascore Tennis من `SOFASCORE_TENNIS_URL`.
- Formula 1:
  - Formula1.com من `FORMULA1_URL`.
  - Motorsport.com من `MOTORSPORT_F1_URL` للأخبار.
- أخبار عربي:
  - YallaKora.
  - FilGoal.
  - Sky News Arabia Sport.
  - Kooora.

### بيتبعت فين؟

- أحداث التنس و F1 بتتطبع على شكل `OtherSportEvent` وتتبعت إلى:
  - `POST /api/other-sports/import/events`
- الأخبار العربية وأخبار Motorsport بتتبعت إلى:
  - `POST /api/SportsData/import/news`

### تشغيله تلقائيًا

في `appsettings` أو environment:

```json
{
  "SportsSync": {
    "EnableSeleniumMultiSportImport": true
  }
}
```

أو من pipeline يدوي/cron:

```bash
export ENABLE_SELENIUM_MULTISPORT_IMPORT=true
python Scrapers/run_all.py
```

### ملاحظات مهمة جدًا

- السكريبر بيستخدم Selenium عشان يرندر JavaScript، لكنه مش بيكسر Cloudflare ولا login walls ولا paywalls.
- Flashscore وSofascore ممكن يقفلوا automation حسب البيئة/IP. لو حصل كده هيطلع warning ويكمل باقي المصادر.
- لازم تثبت dependencies:
  - `selenium`
  - `webdriver-manager`
  - Chrome/Chromium على السيرفر أو `CHROME_BINARY` و `CHROMEDRIVER_PATH` لو عندك paths ثابتة.

## 16) إجابات مباشرة على أسئلتك عن جاهزية البيانات

### لو الفرونت داس على الدوري/البطولة هل هيطلع كل تفاصيلها؟

- يقدر يجيب قائمة البطولات من `GET /api/SportsData/tournaments`.
- يقدر يجيب ماتشات البطولة بفلتر `GET /api/SportsData/matches?tournament=...` أو يعتمد على `tournamentId` من response.
- يقدر يجيب ترتيب البطولة من `GET /api/SportsData/standings/{tournamentId}`.
- يقدر يجيب الهدافين من `GET /api/SportsData/scorers/{tournamentId}`.
- يقدر يجيب bracket من `GET /api/SportsData/bracket/{tournamentId}`.

لكن كلمة "كل حاجة" مش مضمونة 100% لكل بطولة إلا لو السكريبر لقى البيانات على المصدر. بعض البطولات ممكن يبقى ناقصها standings/scorers/bracket لو المصدر نفسه مش موفرها أو selector اتغير.

### هل الفانتازي مظبوطة لكل دوري؟

الفانتازي شغالة بالمنطق الحالي كده:

- `GET /api/fantasy/tournaments/{tournamentId}/today/players` بيجيب ماتشات البطولة في اليوم المختار.
- بعد كده بيجيب الفرق اللي لاعبة النهارده.
- لو مفيش لاعبين جاهزين، بيعمل import للاعبين من تشكيلات YallaKora الحديثة والهدافين.
- فيه سكريبر `fantasy_roster_backfill.py` بيرجع لحد `FANTASY_ROSTER_BACKFILL_DAYS` يوم ويدور على آخر تشكيلات معلنة للفرق دي.

المهم: ده best-effort من التشكيلات السابقة، مش squad رسمي مضمون لكل فريق وكل يوم. لو الفريق مالوش تشكيل سابق متسحب، أو YallaKora مش معلن التشكيل، اللاعبين ممكن يبقوا ناقصين.

### هل تفاصيل الخبر هتظهر لما أدوس عليه؟

نعم بشرط إن الخبر اتسحب بالمحتوى الكامل. الفرونت يستخدم:

1. `GET /api/SportsData/news` عشان يعرض القائمة وياخد `id` و `hasDetails`.
2. `GET /api/SportsData/news/{id}` لما المستخدم يدوس على الخبر.

لو `hasDetails=true` يبقى المفروض يرجع `content`, `paragraphs`, `imageUrl`, و `images`. لو مصدر الخبر قفل المقال أو رجع HTML فاضي، الخبر هيظهر بس بتفاصيل أقل، وده محتاج monitoring في الداشبورد.

## 17) تطوير الفانتازي بسحب تشكيلات Sofascore

اتضاف سكريبر جديد مخصوص للفانتازي: `Scrapers/sofascore_fantasy_lineup_importer.py`.

### هدفه

- يجيب ماتشات اليوم من `GET /api/SportsData/matches?date=...`.
- يشتغل على الفرق اللي لاعبة النهارده بس، مش كل فرق الدوري.
- يحاول يلاقي نفس الفريق/الماتش على Sofascore.
- يفضّل تشكيلات نفس اليوم لو Sofascore معلنها.
- لو تشكيل اليوم لسه مش نازل، يرجع لتشكيلات حديثة فقط خلال `SOFASCORE_LINEUP_LOOKBACK_DAYS`، الافتراضي 45 يوم، عشان الفانتازي يبقى عنده لاعبين من بدري ومش يعتمد على تشكيلات قديمة جدًا.
- يبعت التشكيلة على `POST /api/SportsData/import/squad` بنفس شكل التشكيلة الحالي، فتدخل في `MatchLineups` والـ Fantasy API يقدر يستخدمها.

### تشغيله تلقائيًا

الـ backend worker بيشغله تلقائيًا افتراضيًا قبل `other_sports_importer.py` لو:

```json
{
  "SportsSync": {
    "Enabled": true,
    "EnableSofascoreFantasyLineups": true
  }
}
```

في `run_all.py` بيتشغل افتراضيًا، ولو عايز توقفه:

```bash
export ENABLE_SOFASCORE_FANTASY_LINEUPS=false
python Scrapers/run_all.py
```

### هل ده يخلي التشكيلات مضمونة؟

ده أحسن من الاعتماد على YallaKora فقط، لكنه برضه مش ضمان 100% لأن Sofascore ممكن:

- ما ينزلش التشكيل الرسمي بدري.
- يقفل endpoint أو يغير response.
- يختلف اسم الفريق عنده عن الاسم الموجود عندنا، فيحتاج matching يتحسن بجدول aliases.

لكن عمليًا المنطق بقى أقوى: الأول Sofascore لتشكيلات اليوم/القريبة، وبعده fallback YallaKora historical backfill.

### هل نقاط الفانتازي بتتحسب لوحدها؟

نعم، الـ Hosted Worker بينادي `FantasyScoringService.ScoreRecentlyPlayableContestsAsync` بعد الـ full sync. الخدمة بتدور على contests قريبة، ولو في ماتش انتهى وله score، بتبني `PlayerMatchStats` من التشكيلة والأحداث وتحسب points لكل pick وتحدث `TotalPoints`.

المهم: النقاط تعتمد على وجود 3 حاجات في الداتا:

1. الماتش في نفس البطولة ونفس اليوم.
2. التشكيلة موجودة في `MatchLineups`.
3. أحداث الماتش/الأهداف/الكروت/الأسيست متسحبة في `MatchEvents`.

لو الأحداث ناقصة، النقاط هتتحسب لكن ممكن تبقى أقل من الحقيقي.

## 18) Sofascore Tennis + Basketball

سكريبر Selenium العام اتوسع عشان يقرأ:

- Sofascore Tennis من `SOFASCORE_TENNIS_URL`.
- Sofascore Basketball من `SOFASCORE_BASKETBALL_URL`.

الاتنين بيتبعتوا كـ `OtherSportEvent` على `POST /api/other-sports/import/events`، والفرونت يقرأهم من:

- `GET /api/other-sports/events?sportKey=tennis`
- `GET /api/other-sports/events?sportKey=basketball`

لو عايزهم يشتغلوا لوحدهم مع السيرفر:

```json
{
  "SportsSync": {
    "EnableSeleniumMultiSportImport": true
  }
}
```