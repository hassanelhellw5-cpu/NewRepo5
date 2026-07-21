using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QemmaProject.Services;
using QemmaProject.Models;
using QemmaProject.Models.Sports;
using Microsoft.AspNetCore.SignalR;
using QemmaProject.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;

namespace QemmaProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class SportsDataController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly IHubContext<MatchHub> _hubContext;
        private readonly AppDbContext _context;
        private readonly ILogger<SportsDataController> _logger;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient StreamProxyClient = new HttpClient();
        private static readonly HttpClient VideoDownloadClient = new HttpClient();
        private static readonly HashSet<string> AllowedProxyHosts = BuildAllowedProxyHosts();

        private static HashSet<string> BuildAllowedProxyHosts()
        {
            var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "2.simoplay.com",
                "2.simokora.com",
                "yalla1shooot.online",
                "buz-tv-live.s3.eu-central-1.amazonaws.com",
                "s3.us-east-2.amazonaws.com",
                "a8.kora-plus.app",
                "a10.kora-plus.app",
                "a16.kora-plus.app",
                "a18.kora-plus.app",
                "cm.pororbit.shop"
            };

            var configuredHosts = Environment.GetEnvironmentVariable("STREAM_PROXY_ALLOWED_HOSTS") ?? string.Empty;
            foreach (var host in configuredHosts.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                hosts.Add(host);
            }

            return hosts;
        }

        public SportsDataController(IMatchService matchService, IHubContext<MatchHub> hubContext, AppDbContext context, ILogger<SportsDataController> logger, IConfiguration configuration)
        {
            _matchService = matchService;
            _hubContext = hubContext;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                var matchCount = await _context.Matches.CountAsync();
                var newsCount = await _context.News.CountAsync();
                var tourCount = await _context.Tournaments.CountAsync();
                var teamCount = await _context.Teams.CountAsync();
                var connString = _context.Database.GetDbConnection().ConnectionString;

                var safeConn = connString.Contains("Password=") ? connString.Substring(0, connString.IndexOf("Password=")) + "Password=****" : connString;

                return Ok(new
                {
                    Status = "Running",
                    TimeAtServer = DateTime.Now,
                    DatabaseConnection = safeConn,
                    Statistics = new
                    {
                        TotalMatches = matchCount,
                        TotalNews = newsCount,
                        TotalTournaments = tourCount,
                        TotalTeams = teamCount
                    },
                    LatestMatches = await _context.Matches.OrderByDescending(m => m.Id).Take(5).Select(m => new { m.MatchId, m.Status, m.ScoreHome, m.ScoreAway }).ToListAsync()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("import/matches")]
        public async Task<IActionResult> ImportMatches([FromBody] List<YallaKoraTournamentDto> tournamentsDto)
        {
            if (tournamentsDto == null || !tournamentsDto.Any()) return BadRequest(new { message = "No tournament data provided." });
            try
            {
                var errors = await _matchService.SaveMatchesFromScraperAsync(tournamentsDto);
                var syncDates = await UpsertDateSyncLogsFromImportAsync(tournamentsDto);
                var matchUpdatePayloads = await BuildMatchUpdatePayloadsAsync(tournamentsDto);
                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Matches Updated", time = DateTime.Now, matches = matchUpdatePayloads });
                foreach (var payload in matchUpdatePayloads)
                {
                    await _hubContext.Clients.Group(MatchHub.MatchRoom(payload.Id)).SendAsync("ReceiveMatchScoreUpdate", payload);
                }
                return Ok(new
                {
                    message = errors.Any() ? "Completed with errors" : "Matches imported successfully.",
                    tournamentsCount = tournamentsDto.Count,
                    matchesCount = tournamentsDto.Sum(t => t.matches?.Count ?? 0),
                    syncDates,
                    errors = errors // <-- هنا هتشوف السبب الحقيقي
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [AllowAnonymous]
        [HttpPost("import/news")]
        public async Task<IActionResult> ImportNews([FromBody] List<NewsDto> newsDto)
        {
            if (newsDto == null || !newsDto.Any()) return BadRequest(new { message = "No news data provided." });
            try
            {
                await _matchService.SaveNewsFromScraperAsync(newsDto);
                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "News Updated", time = DateTime.Now });
                return Ok(new { message = "News imported successfully.", newsCount = newsDto.Count });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }



        [AllowAnonymous]
        [HttpPost("sync/date")]
        public async Task<IActionResult> SyncDate([FromQuery] DateTime date)
        {
            var targetDate = date.Date.ToString("yyyy-MM-dd");
            var scraperPath = ResolveScraperPath();
            var runAllPath = Path.Combine(scraperPath, "run_all.py");
            if (!System.IO.File.Exists(runAllPath))
                return NotFound(new { message = "run_all.py was not found.", runAllPath });

            var pythonPath = _configuration["SportsSync:PythonPath"] ?? "python3";
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{runAllPath}\"",
                WorkingDirectory = scraperPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.EnvironmentVariables["SCRAPER_DATE"] = targetDate;
            startInfo.EnvironmentVariables["YALLAKORA_MATCH_DATE"] = targetDate;
            startInfo.EnvironmentVariables["VIDEO_SCRAPER_DATE"] = targetDate;
            startInfo.EnvironmentVariables["RUN_ALL_PROFILE"] = "light";

            var apiDomain = _configuration["ApiDomain"];
            if (!string.IsNullOrWhiteSpace(apiDomain))
                startInfo.EnvironmentVariables["API_DOMAIN"] = apiDomain;

            using var process = Process.Start(startInfo);
            if (process == null)
                return StatusCode(500, new { message = "Could not start sync process." });

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var output = await outputTask;
            var error = await errorTask;

            var hasData = await _context.Matches.AnyAsync(m => m.MatchDate == DateOnly.FromDateTime(date));
            await UpsertDateSyncLogAsync(date.Date, hasData);
            await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Manual Date Sync Completed", date = targetDate, exitCode = process.ExitCode, hasData, time = DateTime.UtcNow });

            return Ok(new
            {
                date = targetDate,
                process.ExitCode,
                succeeded = process.ExitCode == 0,
                hasData,
                output = Tail(output, 12000),
                error = Tail(error, 6000)
            });
        }


        private async Task<List<MatchScoreUpdatePayload>> BuildMatchUpdatePayloadsAsync(List<YallaKoraTournamentDto> tournamentsDto)
        {
            var importedMatches = tournamentsDto
                .SelectMany(t => t.matches ?? new List<YallaKoraMatchDto>())
                .Where(m => !string.IsNullOrWhiteSpace(m.match_id))
                .ToList();
            var externalIds = importedMatches.Select(m => m.match_id).Distinct().ToList();
            var dbIds = await _context.Matches
                .Where(m => externalIds.Contains(m.MatchId))
                .Select(m => new { m.Id, m.MatchId })
                .ToDictionaryAsync(m => m.MatchId, m => m.Id);

            return importedMatches
                .Where(m => dbIds.ContainsKey(m.match_id))
                .Select(m => new MatchScoreUpdatePayload(
                    Id: dbIds[m.match_id],
                    MatchId: m.match_id,
                    HomeTeam: m.home_team,
                    AwayTeam: m.away_team,
                    ScoreHome: m.score_home,
                    ScoreAway: m.score_away,
                    Status: m.status,
                    Time: m.time,
                    MatchDate: m.match_date))
                .ToList();
        }

        private sealed record MatchScoreUpdatePayload(int Id, string MatchId, string HomeTeam, string AwayTeam, string ScoreHome, string ScoreAway, string Status, string Time, string? MatchDate);

        private async Task<object[]> UpsertDateSyncLogsFromImportAsync(List<YallaKoraTournamentDto> tournamentsDto)
        {
            var importedDates = tournamentsDto
                .SelectMany(t => t.matches ?? new List<YallaKoraMatchDto>())
                .Select(m => ParseDateOnly(m.match_date))
                .Where(d => d.HasValue)
                .Select(d => d!.Value.Date)
                .Distinct()
                .ToList();

            foreach (var date in importedDates)
            {
                var existing = await _context.DateSyncLogs.FirstOrDefaultAsync(l => l.Date == date);
                if (existing == null)
                {
                    _context.DateSyncLogs.Add(new QemmaProject.Models.Sports.DateSyncLog
                    {
                        Date = date,
                        LastSyncedAt = DateTime.UtcNow,
                        HasData = true
                    });
                }
                else
                {
                    existing.LastSyncedAt = DateTime.UtcNow;
                    existing.HasData = true;
                }
            }

            if (importedDates.Count > 0)
                await _context.SaveChangesAsync();

            return importedDates.Select(d => new { date = d, hasData = true }).Cast<object>().ToArray();
        }

        private static DateTime? ParseDateOnly(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateTime.TryParse(value, out var parsed)) return parsed.Date;
            return null;
        }


        private async Task UpsertDateSyncLogAsync(DateTime date, bool hasData)
        {
            var existing = await _context.DateSyncLogs.FirstOrDefaultAsync(l => l.Date == date.Date);
            if (existing == null)
            {
                _context.DateSyncLogs.Add(new QemmaProject.Models.Sports.DateSyncLog
                {
                    Date = date.Date,
                    LastSyncedAt = DateTime.UtcNow,
                    HasData = hasData
                });
            }
            else
            {
                existing.LastSyncedAt = DateTime.UtcNow;
                existing.HasData = hasData;
            }
            await _context.SaveChangesAsync();
        }

        private static string Tail(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars) return value ?? string.Empty;
            return value.Substring(value.Length - maxChars);
        }

        private static string ResolveScraperPath()
        {
            var fromBase = Path.Combine(AppContext.BaseDirectory, "Scrapers");
            return Directory.Exists(fromBase) ? fromBase : Path.Combine(Directory.GetCurrentDirectory(), "Scrapers");
        }

        [AllowAnonymous]
        [HttpGet("matches")]
        public async Task<IActionResult> GetMatches(
            [FromQuery] DateTime? date,
            [FromQuery] bool liveOnly = false,
            [FromQuery] string? team = null,
            [FromQuery] string? tournament = null,
            [FromQuery] string? status = null,
            [FromQuery] string? q = null,
            [FromQuery] bool? hasVideos = null,
            [FromQuery] bool? hasStreams = null,
            [FromQuery] bool? hasDetails = null)
        {
            try
            {
                var matches = date.HasValue
                    ? await _matchService.GetMatchesByDateAsync(date.Value.Date)
                    : await _matchService.GetAllMatchesAsync();

                if (liveOnly)
                {
                    matches = matches
                        .Where(m => IsLiveStatus(m.Status))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(team))
                    matches = matches.Where(m => ContainsText(m.HomeTeam?.Name, team) || ContainsText(m.AwayTeam?.Name, team)).ToList();

                if (!string.IsNullOrWhiteSpace(tournament))
                    matches = matches.Where(m => ContainsText(m.Tournament?.Name, tournament)).ToList();

                if (!string.IsNullOrWhiteSpace(status))
                    matches = matches.Where(m => ContainsText(m.Status, status)).ToList();

                if (!string.IsNullOrWhiteSpace(q))
                    matches = matches.Where(m =>
                        ContainsText(m.HomeTeam?.Name, q) ||
                        ContainsText(m.AwayTeam?.Name, q) ||
                        ContainsText(m.Tournament?.Name, q) ||
                        ContainsText(m.Channel, q) ||
                        ContainsText(m.Status, q)).ToList();

                if (hasVideos.HasValue)
                    matches = matches.Where(m => (m.Videos?.Any(v => v.IsAvailable) ?? false) == hasVideos.Value).ToList();

                if (hasStreams.HasValue)
                    matches = matches.Where(m => (m.Streams?.Any() ?? false) == hasStreams.Value).ToList();

                if (hasDetails.HasValue)
                    matches = matches.Where(m => ((m.Events?.Any() ?? false) || !string.IsNullOrWhiteSpace(m.HomeFormation) || !string.IsNullOrWhiteSpace(m.AwayFormation)) == hasDetails.Value).ToList();

                matches = matches
                    .OrderBy(m => m.MatchDate)
                    .ThenBy(m => m.Time)
                    .ThenBy(m => m.Tournament?.Name)
                    .ToList();

                var matchIds = matches.Select(m => m.Id).ToList();
                var statsByMatchId = (await _context.MatchStatistics
                        .Where(s => matchIds.Contains(s.MatchId))
                        .OrderBy(s => s.Name)
                        .ToListAsync())
                    .GroupBy(s => s.MatchId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                return Ok(matches.Select(m => new
                {
                    id = m.Id,
                    matchId = m.MatchId,
                    matchDate = m.MatchDate,
                    status = m.Status,
                    isLive = IsLiveStatus(m.Status),
                    score = new
                    {
                        home = m.ScoreHome,
                        away = m.ScoreAway
                    },
                    time = m.Time,
                    channel = m.Channel,
                    sourceUrl = m.SourceUrl,
                    homeTeam = new
                    {
                        id = m.HomeTeam?.Id,
                        name = m.HomeTeam?.Name,
                        logoUrl = m.HomeTeam?.LogoUrl
                    },
                    awayTeam = new
                    {
                        id = m.AwayTeam?.Id,
                        name = m.AwayTeam?.Name,
                        logoUrl = m.AwayTeam?.LogoUrl
                    },
                    tournament = m.Tournament == null ? null : new
                    {
                        id = m.Tournament.Id,
                        name = m.Tournament.Name,
                        logoUrl = m.Tournament.LogoUrl,
                        type = m.Tournament.Type
                    },
                                        header = new
                    {
                        title = $"{m.HomeTeam?.Name ?? "TBD"} vs {m.AwayTeam?.Name ?? "TBD"}",
                        subtitle = m.Tournament?.Name,
                        date = m.MatchDate,
                        time = m.Time,
                        status = m.Status,
                        isLive = IsLiveStatus(m.Status),
                        scoreText = string.IsNullOrWhiteSpace(m.ScoreHome) && string.IsNullOrWhiteSpace(m.ScoreAway)
                            ? null
                            : $"{m.ScoreHome ?? ""} - {m.ScoreAway ?? ""}".Trim(),
                        channel = m.Channel,
                        home = new { name = m.HomeTeam?.Name, logoUrl = m.HomeTeam?.LogoUrl },
                        away = new { name = m.AwayTeam?.Name, logoUrl = m.AwayTeam?.LogoUrl },
                        hasStreams = m.Streams?.Any() ?? false,
                        hasVideos = m.Videos?.Any(v => v.IsAvailable) ?? false,
                        hasDetails = (m.Events?.Any() ?? false) || !string.IsNullOrWhiteSpace(m.HomeFormation) || !string.IsNullOrWhiteSpace(m.AwayFormation)
                    },
                    streams = (m.Streams ?? new List<MatchStream>())
                        .Select(ToPublicStreamResponse)
                        .ToList(),
                    events = (m.Events ?? new List<MatchEvent>())
                        .OrderBy(e => e.PublishedAt)
                        .Select(e => new
                        {
                            e.Id,
                            e.Minute,
                            e.Type,
                            e.PlayerName,
                            e.AssistPlayerName,
                            e.Detail,
                            e.TeamSide,
                            e.PublishedAt
                        })
                        .ToList(),
                    stats = (statsByMatchId.TryGetValue(m.Id, out var stats)
                            ? stats
                            : new List<QemmaProject.Models.Sports.MatchStatistic>())
                        .Select(s => new
                        {
                            s.Id,
                            s.Name,
                            s.HomeValue,
                            s.AwayValue,
                            s.UpdatedAt
                        })
                        .ToList(),
                    videos = (m.Videos ?? new List<QemmaProject.Models.Sports.MatchVideo>())
                        .Where(v => v.IsAvailable)
                        .OrderBy(v => v.Type)
                        .ThenByDescending(v => v.PublishedAt ?? v.CreatedAt)
                        .Select(v => new
                        {
                            v.Id,
                            v.Title,
                            v.Description,
                            v.VideoUrl,
                            v.EmbedUrl,
                            v.ThumbnailUrl,
                            v.Source,
                            v.Type,
                            v.PublishedAt,
                            canDownload = IsLikelyDirectVideoUrl(v.VideoUrl),
                            downloadUrl = Url.Action(nameof(DownloadMatchVideo), new { videoId = v.Id }),
                            playbackUrl = Url.Action(nameof(GetMatchVideoPlayback), new { videoId = v.Id })
                        })
                        .ToList()
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving matches", error = ex.Message });
            }
        }



        [AllowAnonymous]
        [HttpGet("tournaments")]
        public async Task<IActionResult> GetTournaments([FromQuery] string? q = null, [FromQuery] string? type = null)
        {
            try
            {
                var tournaments = await _context.Tournaments
                    .Include(t => t.Matches)
                    .OrderBy(t => t.Name)
                    .ToListAsync();

                if (!string.IsNullOrWhiteSpace(q))
                    tournaments = tournaments.Where(t => ContainsText(t.Name, q)).ToList();

                if (!string.IsNullOrWhiteSpace(type))
                    tournaments = tournaments.Where(t => ContainsText(t.Type, type)).ToList();

                return Ok(tournaments.Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.YallaKoraId,
                    t.LogoUrl,
                    t.Type,
                    matchesCount = t.Matches?.Count ?? 0
                }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving tournaments", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("coverage")]
        public async Task<IActionResult> GetCoverage([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = (date ?? DateTime.UtcNow.AddHours(3)).Date;
                var matches = await _context.Matches
                    .Include(m => m.HomeTeam)
                    .Include(m => m.AwayTeam)
                    .Include(m => m.Videos)
                    .Include(m => m.Streams)
                    .Include(m => m.Events)
                    .Where(m => m.MatchDate == DateOnly.FromDateTime(targetDate))
                    .ToListAsync();

                var finished = matches.Where(m => IsFinishedStatus(m.Status) || HasScore(m)).ToList();
                var withVideos = finished.Count(m => m.Videos?.Any(v => v.IsAvailable) ?? false);
                var withStreams = matches.Count(m => m.Streams?.Any() ?? false);
                var withDetails = matches.Count(m => (m.Events?.Any() ?? false) || !string.IsNullOrWhiteSpace(m.HomeFormation) || !string.IsNullOrWhiteSpace(m.AwayFormation));

                return Ok(new
                {
                    date = targetDate,
                    generatedAt = DateTime.UtcNow,
                    totalMatches = matches.Count,
                    finishedMatches = finished.Count,
                    withVideos,
                    withStreams,
                    withDetails,
                    videoCoveragePercent = Percent(withVideos, finished.Count),
                    streamCoveragePercent = Percent(withStreams, matches.Count),
                    detailsCoveragePercent = Percent(withDetails, matches.Count),
                    missingVideos = finished
                        .Where(m => !(m.Videos?.Any(v => v.IsAvailable) ?? false))
                        .Select(m => new { m.MatchId, homeTeam = m.HomeTeam?.Name, awayTeam = m.AwayTeam?.Name, m.Status, m.MatchDate })
                        .Take(100)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving coverage", error = ex.Message });
            }
        }

        private static bool ContainsText(string? source, string? value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value)) return false;
            return source.Contains(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }


        private static bool IsFinishedStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Contains("انته")
                || status.Contains("Finished", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Full Time", StringComparison.OrdinalIgnoreCase)
                || status.Contains("FT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasScore(QemmaProject.Models.Sports.Match match)
        {
            return !string.IsNullOrWhiteSpace(match.ScoreHome)
                && !string.IsNullOrWhiteSpace(match.ScoreAway)
                && match.ScoreHome != "-"
                && match.ScoreAway != "-";
        }

        private static int Percent(int value, int total)
        {
            return total <= 0 ? 0 : (int)Math.Round(value * 100.0 / total);
        }

        private static bool IsLiveStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Contains("مباشر")
                || status.Contains("الشوط")
                || status.Contains("جارية")
                || status.Contains("Live", StringComparison.OrdinalIgnoreCase);
        }

        [AllowAnonymous]
        [HttpPost("import/streams")]
        public async Task<IActionResult> ImportStreams([FromBody] List<LiveStreamDto> streamsDto)
        {
            if (streamsDto == null || !streamsDto.Any())
                return BadRequest(new { message = "No stream data provided." });

            try
            {
                // CHANGED: SaveLiveStreamsFromScraperAsync now returns a List<string> of
                // per-item errors/skips instead of void, so we can actually see whether
                // streams were linked or silently skipped (previously this always
                // returned "success" even when 0 rows were written).
                var errors = await _matchService.SaveLiveStreamsFromScraperAsync(streamsDto);

                await _hubContext.Clients.All.SendAsync(
                    "ReceiveMatchUpdate",
                    new
                    {
                        status = "Streams Updated",
                        time = DateTime.Now
                    });

                return Ok(new
                {
                    message = errors.Any() ? "Completed with errors" : "Streams imported successfully",
                    count = streamsDto.Count,
                    linked = streamsDto.Count - errors.Count,
                    errors = errors
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Database Error",
                    error = ex.Message,
                    detail = ex.InnerException?.Message
                });
            }
        }

        [AllowAnonymous]
        [HttpGet("proxy/hls")]
        public async Task<IActionResult> ProxyHls([FromQuery] string url, [FromQuery] string? referer, [FromQuery] string? userAgent, [FromQuery] long? expires, [FromQuery] string? sig)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri) ||
                (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(new { message = "Invalid stream URL." });
            }

            if (!AllowedProxyHosts.Contains(targetUri.Host))
            {
                return BadRequest(new { message = $"Host '{targetUri.Host}' is not allowed for stream proxying." });
            }

            if (!IsAuthorizedProxyRequest(url, expires, sig, out var authError))
            {
                return Unauthorized(new { message = authError });
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    string.IsNullOrWhiteSpace(userAgent)
                        ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
                        : userAgent);
                request.Headers.TryAddWithoutValidation("Accept", "*/*");

                if (!string.IsNullOrWhiteSpace(referer))
                {
                    request.Headers.TryAddWithoutValidation("Referer", referer);
                }

                using var response = await StreamProxyClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new
                    {
                        message = "Upstream stream request failed.",
                        status = (int)response.StatusCode
                    });
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var bytes = await response.Content.ReadAsByteArrayAsync();

                if (IsM3U8(targetUri, contentType, bytes))
                {
                    var playlist = Encoding.UTF8.GetString(bytes);
                    var rewritten = RewriteM3U8Playlist(playlist, targetUri, referer, userAgent);
                    DisableStreamCaching();
                    return Content(rewritten, "application/vnd.apple.mpegurl", Encoding.UTF8);
                }

                DisableStreamCaching();
                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying HLS stream {Url}", url);
                return StatusCode(500, new { message = "Error proxying stream.", error = ex.Message });
            }
        }

        [HttpGet("proxy/check")]
        public async Task<IActionResult> CheckHlsProxy([FromQuery] string url, [FromQuery] string? referer, [FromQuery] string? userAgent, [FromQuery] long? expires, [FromQuery] string? sig)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var targetUri) ||
                (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(new { playable = false, message = "Invalid stream URL." });
            }

            if (!AllowedProxyHosts.Contains(targetUri.Host))
            {
                return BadRequest(new
                {
                    playable = false,
                    message = $"Host '{targetUri.Host}' is not allowed for stream proxying."
                });
            }

            if (!IsAuthorizedProxyRequest(url, expires, sig, out var authError))
            {
                return Unauthorized(new { playable = false, message = authError });
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUri);
                request.Headers.TryAddWithoutValidation("User-Agent",
                    string.IsNullOrWhiteSpace(userAgent)
                        ? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36"
                        : userAgent);
                request.Headers.TryAddWithoutValidation("Accept", "*/*");

                if (!string.IsNullOrWhiteSpace(referer))
                {
                    request.Headers.TryAddWithoutValidation("Referer", referer);
                }

                using var response = await StreamProxyClient.SendAsync(request);
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                var bytes = await response.Content.ReadAsByteArrayAsync();
                var preview = Encoding.UTF8.GetString(bytes.Take(Math.Min(bytes.Length, 120)).ToArray());
                var isM3U8 = IsM3U8(targetUri, contentType, bytes);

                return Ok(new
                {
                    playable = response.IsSuccessStatusCode && isM3U8,
                    status = (int)response.StatusCode,
                    contentType,
                    isM3U8,
                    firstBytes = preview
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking HLS stream {Url}", url);
                return StatusCode(500, new
                {
                    playable = false,
                    message = "Error checking stream.",
                    error = ex.Message
                });
            }
        }

        private void DisableStreamCaching()
        {
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, proxy-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }

        private object ToPublicStreamResponse(QemmaProject.Models.Sports.MatchStream stream)
        {
            var exposeRawUrls = _configuration.GetValue("Streams:ExposeRawUrls", false);
            var playbackUrl = IsInternalProxyUrl(stream.M3U8Url) ? stream.M3U8Url : IsInternalProxyUrl(stream.StreamUrl) ? stream.StreamUrl : string.Empty;

            return new
            {
                id = stream.Id,
                source = stream.Source,
                playbackUrl,
                streamUrl = exposeRawUrls ? stream.StreamUrl : string.Empty,
                m3u8Url = exposeRawUrls ? stream.M3U8Url : string.Empty,
                altM3U8Url = exposeRawUrls ? stream.AltM3U8Url : string.Empty
            };
        }

        private static bool IsInternalProxyUrl(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.StartsWith("/api/SportsData/proxy/hls", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsM3U8(Uri targetUri, string contentType, byte[] bytes)
        {
            if (targetUri.AbsolutePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)) return true;
            if (contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase)) return true;
            return Encoding.UTF8.GetString(bytes.Take(Math.Min(bytes.Length, 32)).ToArray()).TrimStart().StartsWith("#EXTM3U");
        }

        private static string RewriteM3U8Playlist(string playlist, Uri playlistUri, string? referer, string? userAgent)
        {
            var lines = playlist.Replace("\r\n", "\n").Split('\n');
            var rewritten = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                {
                    rewritten.Add(line);
                    continue;
                }

                var absoluteSegment = new Uri(playlistUri, trimmed).ToString();
                rewritten.Add(BuildProxyUrl(absoluteSegment, referer, userAgent));
            }

            return string.Join("\n", rewritten);
        }

        private static string BuildProxyUrl(string targetUrl, string? referer, string? userAgent)
        {
            var query = $"url={WebUtility.UrlEncode(targetUrl)}";
            var secret = Environment.GetEnvironmentVariable("STREAM_PROXY_SECRET");
            if (!string.IsNullOrWhiteSpace(secret))
            {
                var expires = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds();
                query += $"&expires={expires}&sig={CreateProxySignature(targetUrl, expires, secret)}";
            }
            if (!string.IsNullOrWhiteSpace(referer)) query += $"&referer={WebUtility.UrlEncode(referer)}";
            if (!string.IsNullOrWhiteSpace(userAgent)) query += $"&userAgent={WebUtility.UrlEncode(userAgent)}";
            return $"/api/SportsData/proxy/hls?{query}";
        }

        private static bool IsAuthorizedProxyRequest(string targetUrl, long? expires, string? signature, out string message)
        {
            message = string.Empty;
            var secret = Environment.GetEnvironmentVariable("STREAM_PROXY_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
            {
                message = "Stream proxy is not configured.";
                return false;
            }

            if (!expires.HasValue || string.IsNullOrWhiteSpace(signature))
            {
                message = "Missing stream proxy signature.";
                return false;
            }

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires.Value)
            {
                message = "Stream proxy signature expired.";
                return false;
            }

            var expected = CreateProxySignature(targetUrl, expires.Value, secret);
            var providedBytes = Encoding.UTF8.GetBytes(signature);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            if (providedBytes.Length != expectedBytes.Length || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                message = "Invalid stream proxy signature.";
                return false;
            }

            return true;
        }

        private static string CreateProxySignature(string targetUrl, long expires, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{targetUrl}|{expires}"));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        [AllowAnonymous]
        [HttpGet("news")]
        public async Task<IActionResult> GetNews([FromQuery] string? category = null, [FromQuery] string? sportKey = null, [FromQuery] int take = 20)
        {
            try
            {
                var news = await _matchService.GetLatestNewsAsync(take, category, sportKey);
                return Ok(news.Select(ToNewsListResponse));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving news", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("news/{id:int}")]
        public async Task<IActionResult> GetNewsDetails(int id)
        {
            try
            {
                var item = await _context.News.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
                if (item == null) return NotFound(new { message = "News item not found." });
                var related = await BuildRelatedNewsQuery(item)
                    .OrderByDescending(n => n.PublishedAt)
                    .Take(8)
                    .ToListAsync();
                var latest = await _context.News.AsNoTracking()
                    .Where(n => n.Id != id)
                    .OrderByDescending(n => n.PublishedAt)
                    .Take(8)
                    .ToListAsync();
                return Ok(ToNewsDetailsResponse(item, related, latest));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving news details", error = ex.Message });
            }
        }


        [AllowAnonymous]
        [HttpGet("news/latest")]
        public async Task<IActionResult> GetLatestNews([FromQuery] int take = 10, [FromQuery] string? category = null, [FromQuery] string? sportKey = null)
        {
            try
            {
                var news = await _matchService.GetLatestNewsAsync(take, category, sportKey);
                return Ok(news.Select(ToNewsListResponse));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving latest news", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("news/{id:int}/related")]
        public async Task<IActionResult> GetRelatedNews(int id, [FromQuery] int take = 8)
        {
            try
            {
                take = Math.Clamp(take, 1, 50);
                var item = await _context.News.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
                if (item == null) return NotFound(new { message = "News item not found." });
                var related = await BuildRelatedNewsQuery(item)
                    .OrderByDescending(n => n.PublishedAt)
                    .Take(take)
                    .ToListAsync();
                return Ok(related.Select(ToNewsListResponse));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving related news", error = ex.Message });
            }
        }

        private IQueryable<QemmaProject.Models.Sports.News> BuildRelatedNewsQuery(QemmaProject.Models.Sports.News item)
        {
            var query = _context.News.AsNoTracking().Where(n => n.Id != item.Id);
            if (!string.IsNullOrWhiteSpace(item.Category))
                return query.Where(n => n.Category == item.Category);
            if (!string.IsNullOrWhiteSpace(item.SportKey))
                return query.Where(n => n.SportKey == item.SportKey);
            return query;
        }

        private static object ToNewsListResponse(QemmaProject.Models.Sports.News n) => new
        {
            n.Id,
            n.Title,
            description = BuildNewsSummary(n.Content),
            n.ImageUrl,
            n.PublishedAt,
            n.Url,
            n.Category,
            n.SportKey,
            n.Source,
            tags = SplitNewsTags(n.Tags),
            relatedEndpoint = $"/api/SportsData/news/{n.Id}/related",
            hasDetails = !string.IsNullOrWhiteSpace(n.Content) && n.Content.Trim().Length > 80,
            detailsEndpoint = $"/api/SportsData/news/{n.Id}"
        };

        private static object ToNewsDetailsResponse(QemmaProject.Models.Sports.News n, IEnumerable<QemmaProject.Models.Sports.News>? related = null, IEnumerable<QemmaProject.Models.Sports.News>? latest = null) => new
        {
            n.Id,
            n.Title,
            content = n.Content ?? string.Empty,
            paragraphs = SplitNewsParagraphs(n.Content),
            n.ImageUrl,
            images = string.IsNullOrWhiteSpace(n.ImageUrl) ? Array.Empty<string>() : new[] { n.ImageUrl },
            n.PublishedAt,
            n.Url,
            n.Category,
            n.SportKey,
            n.Source,
            tags = SplitNewsTags(n.Tags),
            n.TournamentId,
            n.MatchId,
            relatedEndpoint = $"/api/SportsData/news/{n.Id}/related",
            latestEndpoint = "/api/SportsData/news/latest",
            relatedNews = (related ?? Array.Empty<QemmaProject.Models.Sports.News>()).Select(ToNewsListResponse),
            latestNews = (latest ?? Array.Empty<QemmaProject.Models.Sports.News>()).Select(ToNewsListResponse)
        };

        private static string BuildNewsSummary(string? content)
        {
            var value = (content ?? string.Empty).Trim();
            return value.Length <= 180 ? value : value.Substring(0, 180).Trim() + "...";
        }

        private static string[] SplitNewsParagraphs(string? content) => (content ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToArray();

        private static string[] SplitNewsTags(string? tags) => (tags ?? string.Empty)
            .Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();


        [AllowAnonymous]
        [HttpPost("import/match-details")]
        public async Task<IActionResult> ImportMatchDetails([FromBody] MatchDetailsDto detailsDto)
        {
            if (detailsDto == null) return BadRequest(new { message = "No match details provided." });
            try
            {
                var error = await _matchService.SaveMatchDetailsFromScraperAsync(detailsDto);
                if (error != null) return NotFound(new { message = error });
                var match = await _context.Matches.FirstOrDefaultAsync(m => m.MatchId == detailsDto.match_id);
                var detailsPayload = new { status = "Match Details Updated", matchId = detailsDto.match_id, time = DateTime.Now, eventsCount = detailsDto.events?.Count ?? 0, statsCount = detailsDto.stats?.Count ?? 0 };
                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", detailsPayload);
                if (match != null)
                {
                    await _hubContext.Clients.Group(MatchHub.MatchRoom(match.Id)).SendAsync("ReceiveMatchDetailsUpdate", detailsPayload);
                }
                return Ok(new { message = "Match details imported successfully.", eventsCount = detailsDto.events?.Count ?? 0, statsCount = detailsDto.stats?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("match-details/{matchId}")]
        public async Task<IActionResult> GetMatchDetails(string matchId)
        {
            var match = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Events)
                .Include(m => m.Streams)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            if (match == null) return NotFound(new { message = $"No match found with match_id={matchId}" });

            var stats = await _context.MatchStatistics
                .Where(s => s.MatchId == match.Id)
                .OrderBy(s => s.Name)
                .ToListAsync();

            return Ok(new
            {
                match.MatchId,
                HomeTeam = match.HomeTeam?.Name,
                AwayTeam = match.AwayTeam?.Name,
                Score = new { Home = match.ScoreHome, Away = match.ScoreAway },
                match.Status,
                match.Time,
                match.Channel,
                Events = match.Events.OrderBy(e => e.PublishedAt).ThenBy(e => e.Id),
                Stats = stats
            });
        }


        [AllowAnonymous]
        [HttpPost("import/match-videos")]
        public async Task<IActionResult> ImportMatchVideos([FromBody] MatchVideosImportDto videosDto)
        {
            if (videosDto == null) return BadRequest(new { message = "No match videos provided." });
            try
            {
                var error = await _matchService.SaveMatchVideosFromScraperAsync(videosDto);
                if (error != null) return NotFound(new { message = error });
                await _hubContext.Clients.All.SendAsync("ReceiveMatchUpdate", new { status = "Match Videos Updated", matchId = videosDto.match_id, time = DateTime.Now });
                return Ok(new { message = "Match videos imported successfully.", videosCount = videosDto.videos?.Count ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("match-videos/{matchId}")]
        public async Task<IActionResult> GetMatchVideos(string matchId)
        {
            var videos = await _matchService.GetMatchVideosAsync(matchId);
            var response = videos.Select(v => new
            {
                v.Id,
                v.Title,
                v.Description,
                v.VideoUrl,
                v.EmbedUrl,
                v.ThumbnailUrl,
                v.Source,
                v.Type,
                v.PublishedAt,
                canDownload = IsLikelyDirectVideoUrl(v.VideoUrl),
                downloadUrl = Url.Action(nameof(DownloadMatchVideo), new { videoId = v.Id }),
                playbackUrl = Url.Action(nameof(GetMatchVideoPlayback), new { videoId = v.Id })
            }).ToList();

            return Ok(new
            {
                MatchId = matchId,
                Summary = response.Where(v => v.Type == "Summary"),
                Goals = response.Where(v => v.Type == "Goals" || v.Type == "SingleGoal" || v.Type == "Penalties"),
                All = response
            });
        }



        [AllowAnonymous]
        [HttpGet("match-videos/{videoId:int}/playback")]
        public async Task<IActionResult> GetMatchVideoPlayback(int videoId)
        {
            var video = await _context.MatchVideos
                .Where(v => v.Id == videoId && v.IsAvailable)
                .Select(v => new
                {
                    v.Id,
                    v.MatchId,
                    v.Title,
                    v.Description,
                    v.VideoUrl,
                    v.EmbedUrl,
                    v.ThumbnailUrl,
                    v.Source,
                    v.Type,
                    v.PublishedAt
                })
                .FirstOrDefaultAsync();

            if (video == null) return NotFound(new { message = "Video not found." });

            return Ok(new
            {
                video.Id,
                video.MatchId,
                video.Title,
                video.Description,
                video.VideoUrl,
                video.EmbedUrl,
                video.ThumbnailUrl,
                video.Source,
                video.Type,
                video.PublishedAt,
                canDownload = IsLikelyDirectVideoUrl(video.VideoUrl),
                downloadUrl = Url.Action(nameof(DownloadMatchVideo), new { videoId = video.Id })
            });
        }

        [AllowAnonymous]
        [HttpGet("match-videos/{videoId:int}/download")]
        public async Task<IActionResult> DownloadMatchVideo(int videoId)
        {
            var video = await _context.MatchVideos.FirstOrDefaultAsync(v => v.Id == videoId && v.IsAvailable);
            if (video == null) return NotFound(new { message = "Video not found." });
            if (string.IsNullOrWhiteSpace(video.VideoUrl)) return BadRequest(new { message = "Video URL is missing." });

            if (!Uri.TryCreate(video.VideoUrl, UriKind.Absolute, out var targetUri) ||
                (targetUri.Scheme != Uri.UriSchemeHttp && targetUri.Scheme != Uri.UriSchemeHttps))
            {
                return BadRequest(new { message = "Invalid video URL." });
            }

            if (!IsLikelyDirectVideoUrl(video.VideoUrl))
            {
                return BadRequest(new
                {
                    message = "This source is not a direct downloadable video file. Use playbackUrl/embedUrl for watching.",
                    video.Id,
                    video.VideoUrl,
                    video.EmbedUrl
                });
            }

            try
            {
                using var response = await VideoDownloadClient.GetAsync(targetUri);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, new { message = "Upstream video request failed.", status = (int)response.StatusCode });
                }

                var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                if (!contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) &&
                    !contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { message = "The upstream URL did not return a downloadable video file.", contentType });
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var extension = Path.GetExtension(targetUri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(extension)) extension = ".mp4";
                var safeTitle = MakeSafeFileName(string.IsNullOrWhiteSpace(video.Title) ? $"match-video-{video.Id}" : video.Title);
                return File(bytes, contentType, $"{safeTitle}{extension}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading match video {VideoId}", videoId);
                return StatusCode(500, new { message = "Error downloading video.", error = ex.Message });
            }
        }

        private static bool IsLikelyDirectVideoUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            var path = uri.AbsolutePath;
            return path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webm", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".mov", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase);
        }

        private static string MakeSafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray();
            var safe = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(safe) ? "match-video" : safe;
        }


        // ---------------- NEW: squad / lineup ----------------

        [AllowAnonymous]
        [HttpPost("import/squad")]
        public async Task<IActionResult> ImportSquad([FromBody] MatchSquadDto squadDto)
        {
            if (squadDto == null) return BadRequest(new { message = "No squad data provided." });
            try
            {
                var error = await _matchService.SaveMatchSquadFromScraperAsync(squadDto);
                if (error != null) return NotFound(new { message = error });
                return Ok(new { message = "Squad imported successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("squad/{matchId}")]
        public async Task<IActionResult> GetSquad(string matchId)
        {
            var match = await _matchService.GetMatchSquadAsync(matchId);
            if (match == null) return NotFound(new { message = $"No match found with match_id={matchId}" });

            return Ok(new
            {
                match.MatchId,
                HomeTeam = match.HomeTeam?.Name,
                AwayTeam = match.AwayTeam?.Name,
                match.HomeFormation,
                match.AwayFormation,
                match.HomeCoach,
                match.AwayCoach,
                Lineups = match.Lineups
            });
        }

        // ---------------- NEW: tournament details (standings / scorers / bracket) ----------------

        [AllowAnonymous]
        [HttpPost("import/tournament-details/{tournamentId}")]
        public async Task<IActionResult> ImportTournamentDetails(string tournamentId, [FromBody] TournamentDetailsDto detailsDto)
        {
            if (detailsDto == null) return BadRequest(new { message = "No data provided." });
            try
            {
                await _matchService.SaveTournamentDetailsFromScraperAsync(tournamentId, detailsDto);
                return Ok(new { message = "Tournament details imported successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Database Error", error = ex.Message, detail = ex.InnerException?.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("standings/{tournamentId}")]
        public async Task<IActionResult> GetStandings(int tournamentId)
        {
            try
            {
                return Ok(await _matchService.GetStandingsAsync(tournamentId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving standings", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("scorers/{tournamentId}")]
        public async Task<IActionResult> GetScorers(int tournamentId)
        {
            try
            {
                return Ok(await _matchService.GetScorersAsync(tournamentId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving scorers", error = ex.Message });
            }
        }

        [AllowAnonymous]
        [HttpGet("bracket/{tournamentId}")]
        public async Task<IActionResult> GetBracket(int tournamentId)
        {
            try
            {
                return Ok(await _matchService.GetBracketAsync(tournamentId));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error retrieving bracket", error = ex.Message });
            }
        }
    }
}
