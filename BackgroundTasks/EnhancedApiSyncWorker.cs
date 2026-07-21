using Microsoft.AspNetCore.SignalR;
using QemmaProject.Hubs;
using QemmaProject.Services;

namespace QemmaProject.BackgroundTasks
{
    /// <summary>
    /// Three-tier sync worker.
    ///
    /// WHY THREE TIERS (read before touching IntervalSeconds / LiveIntervalSeconds):
    /// The old version had two tiers -- "full sync" (every IntervalSeconds, e.g. 10 min)
    /// and "live sync" (every LiveIntervalSeconds, e.g. 60s) -- but "full sync" looped
    /// over the ENTIRE SportsSync:FullSyncLookbackDays/FullSyncLookaheadDays range
    /// (730 + 365 + today = 1096 days with the current appsettings.json values) and ran
    /// multisport_selenium_importer.py (a real headless Chrome/Selenium session
    /// hitting ATP/WTA/Sofascore/formula1.com) INSIDE that 1096-day loop, AND live sync
    /// (every 60 seconds!) also launched Selenium again on top of that. Those sites'
    /// data isn't date-parameterized at all -- they only ever return "current" scores
    /// -- so this was launching a full Chrome session dozens of times per 10-minute
    /// window for literally the same result each time, which is exactly what caused
    /// the ERR_INTERNET_DISCONNECTED / renderer-timeout failures seen in production
    /// logs: the sites started rate-limiting/blocking the sheer request volume.
    ///
    /// The fix: split into three tiers so the big day-range backfill and the
    /// non-date-aware Selenium scrape each only run as often as they actually need to:
    ///
    ///   1. Live   (every LiveIntervalSeconds, default 60s)   -- score/stream updates only.
    ///              Never launches Selenium.
    ///   2. Near   (every IntervalSeconds, default 600s/10min) -- scrapes TODAY only via
    ///              yallakora_engine.py / other_sports_importer.py, PLUS runs
    ///              multisport_selenium_importer.py exactly once. This keeps "today"
    ///              and near-live matches fresh without re-scraping a huge date range.
    ///   3. Deep   (every SportsSync:DeepSyncIntervalHours, default 24h) -- the actual
    ///              FullSyncLookbackDays/FullSyncLookaheadDays backfill (today - 730 days
    ///              through today + 365 days by default) so a user can tap ANY date in the app and already find
    ///              matches stored for it. yallakora_engine.py and other_sports_importer.py
    ///              run once PER DAY in that range (they ARE date-aware), but
    ///              multisport_selenium_importer.py still runs only ONCE for the whole
    ///              deep-sync pass, since re-running it per day would just repeat the
    ///              same "current" scrape hundreds of times for nothing.
    /// </summary>
    public class EnhancedApiSyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EnhancedApiSyncWorker> _logger;
        private readonly IHubContext<MatchHub> _hubContext;
        private readonly IConfiguration _configuration;

        public EnhancedApiSyncWorker(IServiceProvider serviceProvider, ILogger<EnhancedApiSyncWorker> logger, IHubContext<MatchHub> hubContext, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_configuration.GetValue("SportsSync:Enabled", true))
            {
                _logger.LogInformation("Sports sync worker is disabled by SportsSync:Enabled=false.");
                return;
            }

            _logger.LogInformation("Enhanced sports sync worker started.");

            // Start with a near/live sync so today's active matches update as soon
            // as the API starts. Delay the heavier backfill so it does not block
            // live score scraping on startup.
            var initialDeepSyncDelayMinutes = _configuration.GetValue("SportsSync:InitialDeepSyncDelayMinutes", 30);
            var nextDeepSyncAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(0, initialDeepSyncDelayMinutes));
            var nextNearSyncAt = DateTimeOffset.MinValue;

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow;

                if (now >= nextDeepSyncAt)
                {
                    await RunDeepSyncCycle(stoppingToken);
                    var deepIntervalHours = _configuration.GetValue("SportsSync:DeepSyncIntervalHours", 24);
                    nextDeepSyncAt = DateTimeOffset.UtcNow.AddHours(Math.Max(1, deepIntervalHours));
                }
                else if (now >= nextNearSyncAt)
                {
                    await RunNearSyncCycle(stoppingToken);
                    var nearIntervalSeconds = _configuration.GetValue("SportsSync:IntervalSeconds", 600);
                    nextNearSyncAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, nearIntervalSeconds));
                }
                else
                {
                    await RunLiveSyncCycle(stoppingToken);
                }

                var liveIntervalSeconds = _configuration.GetValue("SportsSync:LiveIntervalSeconds", 5);
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, liveIntervalSeconds)), stoppingToken);
            }
        }

        // ---------------------------------------------------------------
        // Tier 2: Near sync -- today only, every SportsSync:IntervalSeconds.
        // This is where multisport_selenium_importer.py runs (once), since
        // its sources aren't date-scoped anyway; running it here keeps
        // tennis/basketball/F1 "current" data reasonably fresh without
        // launching Selenium on every single live tick.
        // ---------------------------------------------------------------
        private async Task RunNearSyncCycle(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var fantasyScoring = scope.ServiceProvider.GetRequiredService<FantasyScoringService>();
                var scraperPath = ResolveScraperPath();
                if (!Directory.Exists(scraperPath))
                {
                    _logger.LogWarning("Scraper directory not found at {ScraperPath}", scraperPath);
                    return;
                }

                var pythonPath = _configuration["SportsSync:PythonPath"] ?? "python3";
                var isoToday = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var todayEnvironment = DateEnvironment(isoToday);

                _logger.LogInformation("Running near sports sync for {SyncDate}", isoToday);
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "yallakora_engine.py"), string.Empty, stoppingToken, todayEnvironment);
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "other_sports_importer.py"), string.Empty, stoppingToken, todayEnvironment);

                if (_configuration.GetValue("SportsSync:EnableMultisportSeleniumImport", true))
                {
                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "multisport_selenium_importer.py"), string.Empty, stoppingToken, todayEnvironment);
                }

                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "fantasy_roster_backfill.py"), string.Empty, stoppingToken, todayEnvironment);
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "yallakora_video_scraper.py"), string.Empty, stoppingToken, todayEnvironment);

                var scoredContests = await fantasyScoring.ScoreRecentlyPlayableContestsAsync(stoppingToken);
                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Near Sync Completed", scoredContests, time = DateTime.UtcNow }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in near sports sync worker cycle");
            }
        }

        // ---------------------------------------------------------------
        // FullSyncLookaheadDays backfill (730/365 by default = 1096 days), so
        // scrape hundreds of times for nothing.
        // that any date the user taps in the app already has matches
        // stored for it. Runs once per SportsSync:DeepSyncIntervalHours
        // (default 24h) instead of every IntervalSeconds like before.
        //
        // multisport_selenium_importer.py is intentionally called ONCE,
        // after the day loop -- not once per day -- since its sources
        // (ATP/WTA/Sofascore/formula1.com) aren't date-parameterized and
        // re-running it per day would just repeat the same "current"
        // scrape 61 times for nothing.
        // ---------------------------------------------------------------
        private async Task RunDeepSyncCycle(CancellationToken stoppingToken)
        {
            try
            {
                var scraperPath = ResolveScraperPath();
                if (!Directory.Exists(scraperPath))
                {
                    _logger.LogWarning("Scraper directory not found at {ScraperPath}", scraperPath);
                    return;
                }

                var pythonPath = _configuration["SportsSync:PythonPath"] ?? "python3";
                var deepDates = GetDeepSyncDates().ToList();
                _logger.LogInformation("Running deep sports sync for {DayCount} day(s): {From} .. {To}",
                    deepDates.Count, deepDates.First().ToString("yyyy-MM-dd"), deepDates.Last().ToString("yyyy-MM-dd"));

                foreach (var day in deepDates)
                {
                    var isoDay = day.ToString("yyyy-MM-dd");
                    var datedEnvironment = DateEnvironment(isoDay);

                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "yallakora_engine.py"), string.Empty, stoppingToken, datedEnvironment);
                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "other_sports_importer.py"), string.Empty, stoppingToken, datedEnvironment);
                }

                // Once for the whole deep-sync pass, not once per day (see class doc comment).
                if (_configuration.GetValue("SportsSync:EnableMultisportSeleniumImport", true))
                {
                    var todayEnvironment = DateEnvironment(DateTime.UtcNow.ToString("yyyy-MM-dd"));
                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "multisport_selenium_importer.py"), string.Empty, stoppingToken, todayEnvironment);
                }

                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Deep Sync Completed", days = deepDates.Count, time = DateTime.UtcNow }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in deep sports sync worker cycle");
            }
        }

        // ---------------------------------------------------------------
        // Tier 1: Live sync -- score/stream updates only, every
        // SportsSync:LiveIntervalSeconds (default 60s). NEVER launches
        // Selenium/Chrome here; that's reserved for Near/Deep sync.
        // ---------------------------------------------------------------
        private async Task RunLiveSyncCycle(CancellationToken stoppingToken)
        {
            try
            {
                var scraperPath = ResolveScraperPath();
                if (!Directory.Exists(scraperPath))
                {
                    _logger.LogWarning("Scraper directory not found at {ScraperPath}", scraperPath);
                    return;
                }

                var pythonPath = _configuration["SportsSync:PythonPath"] ?? "python3";
                var liveIntervalSeconds = _configuration.GetValue("SportsSync:LiveIntervalSeconds", 5).ToString();
                var todayEnvironment = DateEnvironment(DateTime.UtcNow.ToString("yyyy-MM-dd"));

                var yallaKoraLoopSeconds = _configuration.GetValue("SportsSync:YallaKoraEngineLoopSeconds", 5);
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "yallakora_engine.py"), $"matches --today --loop-seconds {Math.Max(5, yallaKoraLoopSeconds)}", stoppingToken, todayEnvironment);
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "live_match_updater.py"), string.Empty, stoppingToken, new Dictionary<string, string?>(todayEnvironment)
                {
                    ["LIVE_UPDATE_ONCE"] = "1",
                    ["LIVE_UPDATE_INTERVAL_SECONDS"] = liveIntervalSeconds
                });
                await RunPythonScript(pythonPath, Path.Combine(scraperPath, "live_stream_scraper.py"), string.Empty, stoppingToken);

                if (_configuration.GetValue("SportsSync:EnableSofascoreFantasyLineups", true))
                {
                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "sofascore_fantasy_lineup_importer.py"), string.Empty, stoppingToken);
                }

                if (_configuration.GetValue("SportsSync:EnableUnifiedLiveLinker", false))
                {
                    await RunPythonScript(pythonPath, Path.Combine(scraperPath, "unified_live_stream_linker.py"), string.Empty, stoppingToken);
                }

                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Live Sync Completed", time = DateTime.UtcNow }, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in live sports sync worker cycle");
            }
        }

        /// <summary>
        /// (730/365 in the current appsettings.json -> 1096 days total).
        /// Reads SportsSync:FullSyncLookbackDays / SportsSync:FullSyncLookaheadDays
        /// (30/30 in the current appsettings.json -> 61 days total).
        /// </summary>
        private IEnumerable<DateTime> GetDeepSyncDates()
        {
            var lookbackDays = Math.Max(0, _configuration.GetValue("SportsSync:FullSyncLookbackDays", 1));
            var lookaheadDays = Math.Max(0, _configuration.GetValue("SportsSync:FullSyncLookaheadDays", 7));
            var today = DateTime.UtcNow.Date;

            for (var offset = -lookbackDays; offset <= lookaheadDays; offset++)
            {
                yield return today.AddDays(offset);
            }
        }

        private static Dictionary<string, string?> DateEnvironment(string isoDay) => new()
        {
            ["YALLAKORA_MATCH_DATE"] = isoDay,
            ["SCRAPER_DATE"] = isoDay,
            ["VIDEO_SCRAPER_DATE"] = isoDay,
            ["FANTASY_ROSTER_DATE"] = isoDay,
            ["SOFASCORE_FANTASY_DATE"] = isoDay
        };

        private static string ResolveScraperPath()
        {
            var fromBase = Path.Combine(AppContext.BaseDirectory, "Scrapers");
            return Directory.Exists(fromBase) ? fromBase : Path.Combine(Directory.GetCurrentDirectory(), "Scrapers");
        }

        private async Task RunPythonScript(string pythonPath, string scriptPath, string arguments, CancellationToken stoppingToken, Dictionary<string, string?>? environment = null)
        {
            if (!File.Exists(scriptPath))
            {
                _logger.LogWarning("Python script not found: {ScriptPath}", scriptPath);
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = string.IsNullOrWhiteSpace(arguments) ? $"\"{scriptPath}\"" : $"\"{scriptPath}\" {arguments}",
                WorkingDirectory = Path.GetDirectoryName(scriptPath) ?? Directory.GetCurrentDirectory(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var apiDomain = _configuration["ApiDomain"];
            if (!string.IsNullOrWhiteSpace(apiDomain))
                startInfo.EnvironmentVariables["API_DOMAIN"] = apiDomain;

            // Same key the OtherSportsController checks against X-Import-Key.
            // Passed automatically now so scripts don't need it set manually
            // in the shell every time (e.g. via $env:IMPORT_API_KEY in PowerShell).
            var importApiKey = _configuration["ImportApiKey"];
            if (!string.IsNullOrWhiteSpace(importApiKey))
                startInfo.EnvironmentVariables["IMPORT_API_KEY"] = importApiKey;

            // OtherSports:* config -> the matching env vars other_sports_importer.py
            // reads, so appsettings.json actually drives its behavior instead of
            // just documenting it.
            var iceHockeyEnabled = _configuration["OtherSports:IceHockeyEnabled"];
            if (!string.IsNullOrWhiteSpace(iceHockeyEnabled))
                startInfo.EnvironmentVariables["ICE_HOCKEY_ENABLED"] = iceHockeyEnabled;

            var iceHockeySourceUrl = _configuration["OtherSports:IceHockeySourceUrl"];
            if (!string.IsNullOrWhiteSpace(iceHockeySourceUrl))
                startInfo.EnvironmentVariables["ICE_HOCKEY_SOURCE_URL"] = iceHockeySourceUrl;

            var iceHockeySourceName = _configuration["OtherSports:IceHockeySourceName"];
            if (!string.IsNullOrWhiteSpace(iceHockeySourceName))
                startInfo.EnvironmentVariables["ICE_HOCKEY_SOURCE_NAME"] = iceHockeySourceName;

            var streamOverridesFile = _configuration["OtherSports:StreamOverridesFile"];
            if (!string.IsNullOrWhiteSpace(streamOverridesFile))
                startInfo.EnvironmentVariables["OTHER_SPORTS_STREAM_OVERRIDES_FILE"] = streamOverridesFile;

            if (environment != null)
                foreach (var item in environment)
                    if (item.Value != null) startInfo.EnvironmentVariables[item.Key] = item.Value;

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            try
            {
                await process.WaitForExitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                throw;
            }

            var error = await errorTask;
            if (process.ExitCode != 0) _logger.LogError("Python script {ScriptPath} failed with exit code {ExitCode}. Error: {Error}", scriptPath, process.ExitCode, error);
            else _logger.LogInformation("Python script {ScriptPath} finished. {Output}", scriptPath, await outputTask);
        }
    }
}