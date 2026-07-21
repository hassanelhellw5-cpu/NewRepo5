# ترتيب تشغيل `Scrapers/run_all.py`

## هل يوجد entity لبيانات المستخدم؟

نعم. المشروع يستخدم ASP.NET Identity، والـ entity الأساسية لبيانات المستخدم هي `ApplicationUser` الموروثة من `IdentityUser`. هذا يعني أن بيانات الحساب الأساسية محفوظة في جداول Identity مثل `AspNetUsers`، ومعها حقول المنتج الموجودة في `ApplicationUser` مثل:

- `QemmaCoinsBalance`: رصيد كوينز المستخدم.
- `IsEliteSubscriber` و`EliteSubscriptionEndDate`: حالة اشتراك الداعم/النخبة.
- `CustomThemePalette`: الثيم المختار.
- `ActiveBadgeUrl`: البادج/الأيقونة النشطة.
- `Transactions`: حركات الكوينز.
- `Predictions`: توقعات المستخدم.

لذلك لا نحتاج entity جديدة لأساسيات اليوزر الآن. لو احتجنا لاحقًا بيانات بروفايل كبيرة مثل bio/avatar/social links/privacy settings، الأفضل نعمل entity منفصلة `UserProfile` مرتبطة بـ `ApplicationUser` واحد-لواحد بدل تكبير جدول `AspNetUsers` زيادة.

## ترتيب الران لما تشغل run_all.py

`run_all.py` مصمم أن كل scraper يشتغل process منفصل، ولو scraper اختياري فشل لا يوقف باقي الران.

### 1) تحديد الأيام المستهدفة

الأولوية:

1. `--date` أو `SCRAPER_DATE`: يوم واحد.
2. `--from-date` و`--to-date`: range يدوي.
3. `--full-sync` أو `RUN_ALL_DATE_RANGE_MODE=full_sync`: backfill/lookahead من الإعدادات.
4. بدون أي args: اليوم الحالي فقط.

### 2) تحميل إعدادات البيئة

قبل كل يوم، السكريبت يقرأ `appsettings.json` و`appsettings.Development.json` ويملأ env values المهمة مثل:

- `API_DOMAIN`
- `IMPORT_API_KEY`
- `ConnectionStrings__DefaultConnection`
- flags الخاصة بالسكريبرز الاختيارية.

### 3) football core import

يشغل:

```bash
python Scrapers/yallakora_engine.py matches --date YYYY-MM-DD
```

الغرض:

- جلب مباريات اليوم/التاريخ.
- تحديث النتيجة والحالة والوقت والقناة والفرق والبطولة.
- محاولة جلب تفاصيل إضافية متاحة من يلا كورة.

### 4) live match updater للإحصائيات والتفاصيل القريبة

يشغل `live_match_updater.py` فقط لو التاريخ داخل نافذة اللايف:

- افتراضيًا: اليوم وآخر يومين.
- التحكم عبر:
  - `LIVE_UPDATE_RUN_DAYS_BACK`
  - `LIVE_UPDATE_RUN_DAYS_AHEAD`
  - `ENABLE_LIVE_MATCH_UPDATER=0` للتعطيل.

الغرض:

- تحديث تفاصيل الماتشات اللايف والمنتهية قريبًا.
- إرسال `match-details` للباك إند.
- تشغيل Selenium stats fallback في `full` profile فقط افتراضيًا.

### 5) other sports core import

يشغل:

```bash
python Scrapers/other_sports_importer.py
```

الغرض: سحب الرياضات الأخرى date-aware مثل basketball/tennis/ice hockey حسب الإعدادات.

### 6) multisport selenium importer

يشغل مرة واحدة فقط في الران كله، وليس مرة لكل يوم، لأن مصادره current وليست date-scoped.

يشترط وجود Chrome/Chromium أو `CHROME_BINARY`.

### 7) لو profile = light

يتوقف هنا بعد core imports ويطبع أن fantasy/video/live-linking اتخطوا.

### 8) fantasy roster backfill

في `full` profile فقط:

```bash
python Scrapers/fantasy_roster_backfill.py
```

الغرض: تجهيز لاعبي الفانتازي من آخر lineups/scorers المتاحة.

### 9) Sofascore fantasy lineups

في `full` profile فقط، لو `ENABLE_SOFASCORE_FANTASY_LINEUPS=true`:

```bash
python Scrapers/sofascore_fantasy_lineup_importer.py
```

الغرض: تحسين بيانات lineups/rosters للفانتازي عندما تكون متاحة.

### 10) video/highlights scraping

في `full` profile فقط، لو `ENABLE_VIDEO_SCRAPER=true`:

```bash
python Scrapers/yallakora_video_scraper.py
```

المصادر بالترتيب العملي:

1. `YallaShootApp` لو `YALLASHOOT_APP_API_URL` متاح.
2. YouTube public search fallback بدون API key.
3. FootyRoom.
4. HooFoot.
5. DasFootball.
6. YallaKora match page/videos tab/search.

كل النتائج تدخل إلى:

```http
POST /api/SportsData/import/match-videos
```

### 11) live stream linking

في `full` profile فقط:

- `unified_live_stream_linker.py` لو `ENABLE_UNIFIED_LIVE_LINKER=true`.
- وإلا `live_stream_scraper.py`.

### 12) news scraper

بعد الانتهاء من كل الأيام، لو `ENABLE_NEWS_SCRAPER=true` يشغل:

```bash
python Scrapers/yallakora_engine.py news
```

## أوامر تشغيل مقترحة

### تشغيل سريع لليوم فقط

```bash
python Scrapers/run_all.py --profile light
```

### تشغيل كامل لليوم مع فيديوهات وفانتازي ولايف لينك

```bash
python Scrapers/run_all.py --profile full
```

### backfill كامل

```bash
python Scrapers/run_all.py --full-sync --profile full
```

### تشغيل تاريخ محدد

```bash
python Scrapers/run_all.py --date 2026-07-20 --profile full
```
