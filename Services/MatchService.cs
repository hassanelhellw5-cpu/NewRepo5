using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models;
using QemmaProject.Models.Sports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace QemmaProject.Services
{
    public class MatchService : IMatchService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MatchService> _logger;

        public MatchService(AppDbContext context, ILogger<MatchService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<string>> SaveMatchesFromScraperAsync(List<YallaKoraTournamentDto> tournamentsDto)
        {
            var errors = new List<string>();
            var fallbackDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));

            if (tournamentsDto == null || !tournamentsDto.Any()) return errors;

            foreach (var tourDto in tournamentsDto)
            {
                try
                {
                    // FIXED: the old query was
                    //   t.YallaKoraId == tourDto.tournament_id || t.Name == tourDto.tournament_name
                    // The scraper very often sends tournament_id == "" (it only fills it in
                    // when it manages to parse a /tour/{id}/ link off the match-center page).
                    // With an OR, once ANY tournament in the DB already has YallaKoraId == "",
                    // that first condition matches it regardless of name, so a brand-new
                    // tournament with a different name could get silently merged into an
                    // unrelated existing one. Now the ID comparison only applies when we
                    // actually have a non-empty ID from the scraper; otherwise we fall back
                    // to matching by name only.
                    var tournament = await _context.Tournaments.FirstOrDefaultAsync(t =>
                        (!string.IsNullOrEmpty(tourDto.tournament_id) && t.YallaKoraId == tourDto.tournament_id)
                        || t.Name == tourDto.tournament_name);

                    if (tournament == null)
                    {
                        tournament = new Tournament
                        {
                            Name = tourDto.tournament_name,
                            YallaKoraId = tourDto.tournament_id ?? "",
                            Type = "League",
                            LogoUrl = tourDto.tournament_logo ?? ""
                        };
                        _context.Tournaments.Add(tournament);
                        await _context.SaveChangesAsync();
                    }

                    if (tourDto.matches == null) continue;

                    foreach (var mDto in tourDto.matches)
                    {
                        try
                        {
                            var homeTeam = await EnsureTeamExists(mDto.home_team, mDto.home_team_logo);
                            var awayTeam = await EnsureTeamExists(mDto.away_team, mDto.away_team_logo);

                            var matchDate = ParseScraperMatchDate(mDto.match_date) ?? fallbackDate;

                            var match = await _context.Matches.FirstOrDefaultAsync(m => m.MatchId == mDto.match_id);
                            if (match == null)
                            {
                                match = new Match
                                {
                                    MatchId = mDto.match_id,
                                    HomeTeamId = homeTeam.Id,
                                    AwayTeamId = awayTeam.Id,
                                    MatchDate = matchDate,
                                    TournamentId = tournament.Id,
                                    Status = mDto.status,
                                    ScoreHome = mDto.score_home,
                                    ScoreAway = mDto.score_away,
                                    Time = mDto.time,
                                    Channel = mDto.channel,
                                    SourceUrl = mDto.match_href ?? "",
                                    // FIXED (confirmed from real run): the DB column AwayCoach (and
                                    // likely HomeCoach/HomeFormation/AwayFormation the same way)
                                    // is NOT NULL, but these are only known later from the squad
                                    // import — leaving them unset here made every brand-new match
                                    // insert fail with "Cannot insert the value NULL into column
                                    // 'AwayCoach'", which in turn made every later squad import
                                    // 404 because the match row never actually got created.
                                    HomeCoach = "",
                                    AwayCoach = "",
                                    HomeFormation = "",
                                    AwayFormation = ""
                                };
                                _context.Matches.Add(match);
                            }
                            else
                            {
                                match.MatchDate = matchDate;
                                match.ScoreHome = mDto.score_home;
                                match.ScoreAway = mDto.score_away;
                                match.Status = mDto.status;
                                match.Time = mDto.time;
                                match.Channel = mDto.channel;
                                if (!string.IsNullOrWhiteSpace(mDto.match_href))
                                    match.SourceUrl = mDto.match_href;
                            }
                            await _context.SaveChangesAsync();
                        }
                        catch (Exception ex)
                        {
                            var msg = $"Match {mDto.home_team} vs {mDto.away_team}: {ex.Message} | Inner: {ex.InnerException?.Message}";
                            _logger.LogError(msg);
                            errors.Add(msg);
                            _context.ChangeTracker.Clear();
                        }
                    }
                }
                catch (Exception ex)
                {
                    var msg = $"Tournament {tourDto.tournament_name}: {ex.Message} | Inner: {ex.InnerException?.Message}";
                    _logger.LogError(msg);
                    errors.Add(msg);
                    _context.ChangeTracker.Clear();
                }
            }
            return errors;
        }


        private static DateOnly? ParseScraperMatchDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (DateOnly.TryParse(value, out var dateOnly)) return dateOnly;
            if (DateTime.TryParse(value, out var parsed)) return DateOnly.FromDateTime(parsed);
            return null;
        }

        private async Task<Team> EnsureTeamExists(string name, string? logoUrl = null)
        {
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Name == name);
            var cleanLogoUrl = logoUrl?.Trim() ?? string.Empty;
            if (team == null)
            {
                team = new Team { Name = name, LogoUrl = cleanLogoUrl };
                _context.Teams.Add(team);
                await _context.SaveChangesAsync();
            }
            else if (string.IsNullOrWhiteSpace(team.LogoUrl) && !string.IsNullOrWhiteSpace(cleanLogoUrl))
            {
                team.LogoUrl = cleanLogoUrl;
                await _context.SaveChangesAsync();
            }
            return team;
        }

        public async Task SaveNewsFromScraperAsync(List<NewsDto> newsDto)
        {
            if (newsDto == null) return;
            foreach (var n in newsDto)
            {
                try
                {
                    var title = (n.title ?? string.Empty).Trim();
                    var url = (n.url ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) continue;

                    var incomingContent = FirstNotEmpty(n.content, n.description, title);
                    var publishedAt = ParseNewsDate(n.published_at) ?? DateTime.UtcNow;
                    var existing = await _context.News.FirstOrDefaultAsync(x => x.Url == url || x.Title == title);

                    var category = FirstNotEmpty(n.category, InferNewsCategory(title + " " + incomingContent, url));
                    var sportKey = FirstNotEmpty(n.sport_key, InferSportKey(category, title + " " + incomingContent));
                    var source = FirstNotEmpty(n.source, "YallaKora");
                    var tags = FirstNotEmpty(n.tags, BuildNewsTags(title, incomingContent, category));

                    if (existing == null)
                    {
                        _context.News.Add(new News
                        {
                            Title = title,
                            Content = incomingContent,
                            ImageUrl = FirstNotEmpty(n.image_url, "https://www.yallakora.com/images/yk-logo.png"),
                            Url = url,
                            PublishedAt = publishedAt,
                            Category = category,
                            SportKey = sportKey,
                            Source = source,
                            Tags = tags
                        });
                    }
                    else
                    {
                        existing.Title = title;
                        existing.Url = url;
                        existing.ImageUrl = FirstNotEmpty(n.image_url, existing.ImageUrl);
                        existing.PublishedAt = publishedAt;
                        existing.Category = category;
                        existing.SportKey = sportKey;
                        existing.Source = source;
                        existing.Tags = tags;
                        if (IsRicherContent(incomingContent, existing.Content))
                            existing.Content = incomingContent;
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save news item from scraper: {Title}", n?.title);
                    _context.ChangeTracker.Clear();
                }
            }
        }


        private static string InferNewsCategory(string text, string url)
        {
            var haystack = ((text ?? string.Empty) + " " + (url ?? string.Empty)).ToLowerInvariant();
            if (haystack.Contains("world-cup") || haystack.Contains("كأس العالم")) return "كأس العالم";
            if (haystack.Contains("transfers") || haystack.Contains("انتقالات")) return "الانتقالات";
            if (haystack.Contains("egyptian-league") || haystack.Contains("الدوري المصري")) return "الدوري المصري";
            if (haystack.Contains("premier-league") || haystack.Contains("الدوري الإنجليزي")) return "الدوري الإنجليزي";
            if (haystack.Contains("champions-league") || haystack.Contains("دوري أبطال")) return "دوري أبطال أوروبا";
            if (haystack.Contains("germany") || haystack.Contains("bundesliga") || haystack.Contains("الدوري الألماني")) return "الدوري الألماني";
            if (haystack.Contains("كرة السلة") || haystack.Contains("basketball")) return "كرة السلة";
            if (haystack.Contains("تنس") || haystack.Contains("tennis")) return "تنس";
            return "أخبار";
        }

        private static string InferSportKey(string? category, string text)
        {
            var haystack = ((category ?? string.Empty) + " " + (text ?? string.Empty)).ToLowerInvariant();
            if (haystack.Contains("تنس") || haystack.Contains("tennis")) return "tennis";
            if (haystack.Contains("basketball") || haystack.Contains("كرة السلة")) return "basketball";
            return "football";
        }

        private static string BuildNewsTags(string title, string content, string? category)
        {
            var tags = new List<string>();
            void Add(string? value)
            {
                value = (value ?? string.Empty).Trim();
                if (value.Length > 1 && !tags.Contains(value, StringComparer.OrdinalIgnoreCase)) tags.Add(value);
            }
            Add(category);
            var text = $"{title} {content}";
            foreach (var token in new[] { "الأهلي", "الزمالك", "بيراميدز", "منتخب مصر", "كأس العالم", "محمد صلاح", "ميسي", "ريال مدريد", "برشلونة" })
                if (text.Contains(token, StringComparison.OrdinalIgnoreCase)) Add(token);
            return string.Join(",", tags.Take(8));
        }

        private static bool IsRicherContent(string incoming, string existing)
        {
            if (string.IsNullOrWhiteSpace(incoming)) return false;
            if (string.IsNullOrWhiteSpace(existing)) return true;
            return incoming.Trim().Length > existing.Trim().Length;
        }

        private static string FirstNotEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return string.Empty;
        }

        private static DateTime? ParseNewsDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var parsed) ? parsed : null;
        }

        // Returns per-stream errors/skips instead of void, so the controller (and you)
        // can actually tell whether streams were linked or not instead of always seeing
        // "success" even when nothing got saved.
        public async Task<List<string>> SaveLiveStreamsFromScraperAsync(List<LiveStreamDto> streamsDto)
        {
            var errors = new List<string>();
            var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(3));
            var todayMatches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Where(m => m.MatchDate == targetDate)
                .ToListAsync();

            var refreshedMatchIds = new HashSet<int>();

            foreach (var sDto in streamsDto)
            {
                try
                {
                    // 1) Match by the external match_id first — this is exact and doesn't
                    // depend on team-name spelling/formatting matching perfectly.
                    Match match = null;
                    if (!string.IsNullOrWhiteSpace(sDto.match_id))
                    {
                        match = todayMatches.FirstOrDefault(m => m.MatchId == sDto.match_id);
                    }

                    // 2) Fallback: normalized team-name matching (kept for safety/back-compat)
                    if (match == null)
                    {
                        match = todayMatches.FirstOrDefault(m =>
                            (Normalize(m.HomeTeam.Name) == Normalize(sDto.home_team) &&
                             Normalize(m.AwayTeam.Name) == Normalize(sDto.away_team))
                            ||
                            (Normalize(m.HomeTeam.Name) == Normalize(sDto.away_team) &&
                             Normalize(m.AwayTeam.Name) == Normalize(sDto.home_team)));
                    }

                    if (match == null)
                    {
                        var msg = $"No match found for stream (match_id={sDto.match_id}, {sDto.home_team} vs {sDto.away_team})";
                        _logger.LogWarning(msg);
                        errors.Add(msg);
                        continue;
                    }

                    // The scraper sends one payload per checked match. Clear previous
                    // streams for that match before saving the fresh result, otherwise an
                    // old one-minute/ended playlist can stay in the DB after today's live
                    // source is closed.
                    if (refreshedMatchIds.Add(match.Id))
                    {
                        var existingStreams = await _context.MatchStreams
                            .Where(ms => ms.MatchId == match.Id)
                            .ToListAsync();
                        if (existingStreams.Any())
                        {
                            _context.MatchStreams.RemoveRange(existingStreams);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(sDto.stream_url) || string.IsNullOrWhiteSpace(sDto.m3u8_url))
                    {
                        _logger.LogInformation(
                            "Live stream closed for match_id={MatchId}: {Reason}",
                            sDto.match_id,
                            sDto.status_message ?? sDto.source);
                        await _context.SaveChangesAsync();
                        continue;
                    }

                    var stream = new MatchStream
                    {
                        MatchId = match.Id,
                        StreamUrl = sDto.stream_url,
                        home_team = sDto.home_team,
                        away_team = sDto.away_team,
                        M3U8Url = sDto.m3u8_url,
                        AltM3U8Url = sDto.alt_m3u8_url,
                        Source = sDto.source
                    };
                    _context.MatchStreams.Add(stream);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    var msg = $"Stream for match_id={sDto.match_id} ({sDto.home_team} vs {sDto.away_team}): {ex.Message} | Inner: {ex.InnerException?.Message}";
                    _logger.LogError(msg);
                    errors.Add(msg);
                    _context.ChangeTracker.Clear();
                }
            }

            return errors;
        }

        public async Task SaveTournamentDetailsFromScraperAsync(string tournamentId, TournamentDetailsDto detailsDto)
        {
            var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.YallaKoraId == tournamentId);
            if (tournament == null) return;

            if (detailsDto.standings != null)
            {
                foreach (var sDto in detailsDto.standings)
                {
                    try
                    {
                        var entry = await _context.StandingEntries.FirstOrDefaultAsync(s => s.TournamentId == tournament.Id && s.TeamName == sDto.team);
                        if (entry == null) { entry = new StandingEntry { TournamentId = tournament.Id, TeamName = sDto.team, GroupName = sDto.group ?? "Default" }; _context.StandingEntries.Add(entry); }
                        entry.Rank = sDto.rank; entry.Played = sDto.played; entry.Points = sDto.points;
                        await _context.SaveChangesAsync();
                    }
                    catch { _context.ChangeTracker.Clear(); }
                }
            }

            if (detailsDto.scorers != null)
            {
                foreach (var scDto in detailsDto.scorers)
                {
                    try
                    {
                        var scorer = await _context.PlayerScorers.FirstOrDefaultAsync(s =>
                            s.TournamentId == tournament.Id && s.PlayerName == scDto.player && s.TeamName == scDto.team);
                        if (scorer == null)
                        {
                            scorer = new PlayerScorer { TournamentId = tournament.Id, PlayerName = scDto.player, TeamName = scDto.team };
                            _context.PlayerScorers.Add(scorer);
                        }
                        scorer.Goals = scDto.goals;
                        scorer.Assists = scDto.assists;
                        await UpsertPlayerAsync(scDto.player, scDto.team, "FW", null);
                        await _context.SaveChangesAsync();
                    }
                    catch { _context.ChangeTracker.Clear(); }
                }
            }

            if (detailsDto.bracket != null)
            {
                foreach (var bDto in detailsDto.bracket)
                {
                    try
                    {
                        var bracketMatch = await _context.TournamentBrackets.FirstOrDefaultAsync(b =>
                            b.TournamentId == tournament.Id && b.RoundName == bDto.round && b.MatchOrder == bDto.order);
                        if (bracketMatch == null)
                        {
                            bracketMatch = new TournamentBracket { TournamentId = tournament.Id, RoundName = bDto.round, MatchOrder = bDto.order };
                            _context.TournamentBrackets.Add(bracketMatch);
                        }
                        bracketMatch.TeamHomeName = bDto.home_team;
                        bracketMatch.TeamAwayName = bDto.away_team;
                        bracketMatch.Score = bDto.score;
                        bracketMatch.WinnerName = bDto.winner;
                        await _context.SaveChangesAsync();
                    }
                    catch { _context.ChangeTracker.Clear(); }
                }
            }
        }


        public async Task<string> SaveMatchDetailsFromScraperAsync(MatchDetailsDto detailsDto)
        {
            if (detailsDto == null || string.IsNullOrWhiteSpace(detailsDto.match_id))
                return "No match details or match_id provided.";

            var match = await _context.Matches
                .Include(m => m.Events)
                .FirstOrDefaultAsync(m => m.MatchId == detailsDto.match_id);

            if (match == null)
                return $"No match found with match_id={detailsDto.match_id}";

            if (detailsDto.events != null)
            {
                var incomingKeys = detailsDto.events
                    .Where(e => !string.IsNullOrWhiteSpace(e.external_id) || !string.IsNullOrWhiteSpace(e.detail))
                    .Select(e => !string.IsNullOrWhiteSpace(e.external_id) ? e.external_id : $"{e.minute}|{e.detail}")
                    .ToHashSet();

                foreach (var eDto in detailsDto.events)
                {
                    var key = !string.IsNullOrWhiteSpace(eDto.external_id) ? eDto.external_id : $"{eDto.minute}|{eDto.detail}";
                    var existing = await _context.MatchEvents.FirstOrDefaultAsync(e =>
                        e.MatchId == match.Id && (e.ExternalId == key || (e.ExternalId == null && e.Minute == eDto.minute && e.Detail == eDto.detail)));

                    if (existing == null)
                    {
                        existing = new MatchEvent { MatchId = match.Id, ExternalId = key };
                        _context.MatchEvents.Add(existing);
                    }

                    existing.Minute = eDto.minute ?? "";
                    existing.Type = string.IsNullOrWhiteSpace(eDto.type) ? "MinuteByMinute" : eDto.type;
                    existing.PlayerName = eDto.player_name ?? "";
                    existing.AssistPlayerName = eDto.assist_player_name ?? "";
                    existing.Detail = eDto.detail ?? "";
                    existing.TeamSide = eDto.team_side ?? "";
                    if (DateTime.TryParse(eDto.published_at, out var publishedAt))
                    {
                        existing.PublishedAt = publishedAt;
                    }
                }
            }

            if (detailsDto.stats != null)
            {
                foreach (var sDto in detailsDto.stats.Where(s => !string.IsNullOrWhiteSpace(s.name)))
                {
                    var stat = await _context.MatchStatistics.FirstOrDefaultAsync(s => s.MatchId == match.Id && s.Name == sDto.name);
                    if (stat == null)
                    {
                        stat = new MatchStatistic { MatchId = match.Id, Name = sDto.name };
                        _context.MatchStatistics.Add(stat);
                    }
                    stat.HomeValue = sDto.home_value ?? "";
                    stat.AwayValue = sDto.away_value ?? "";
                    stat.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
            return null;
        }


        public async Task<string> SaveMatchVideosFromScraperAsync(MatchVideosImportDto videosDto)
        {
            if (videosDto == null || string.IsNullOrWhiteSpace(videosDto.match_id))
                return "No match videos or match_id provided.";

            var match = await _context.Matches
                .Include(m => m.Videos)
                .FirstOrDefaultAsync(m => m.MatchId == videosDto.match_id);

            if (match == null)
                return $"No match found with match_id={videosDto.match_id}";

            if (videosDto.videos == null || !videosDto.videos.Any())
                return null;

            foreach (var vDto in videosDto.videos.Where(v => !string.IsNullOrWhiteSpace(v.video_url)))
            {
                var externalId = !string.IsNullOrWhiteSpace(vDto.external_id)
                    ? vDto.external_id
                    : Normalize(vDto.video_url);

                var video = await _context.MatchVideos.FirstOrDefaultAsync(v =>
                    v.MatchId == match.Id && v.ExternalId == externalId);

                if (video == null)
                {
                    video = new MatchVideo
                    {
                        MatchId = match.Id,
                        ExternalId = externalId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.MatchVideos.Add(video);
                }

                video.Title = vDto.title ?? "";
                video.Description = vDto.description ?? "";
                video.VideoUrl = vDto.video_url ?? "";
                video.EmbedUrl = vDto.embed_url ?? "";
                video.ThumbnailUrl = vDto.thumbnail_url ?? "";
                video.Source = string.IsNullOrWhiteSpace(vDto.source) ? "YallaKora" : vDto.source;
                video.Type = string.IsNullOrWhiteSpace(vDto.type) ? "Other" : vDto.type;
                video.IsAvailable = true;
                video.UpdatedAt = DateTime.UtcNow;
                if (DateTime.TryParse(vDto.published_at, out var publishedAt))
                {
                    video.PublishedAt = publishedAt;
                }
            }

            await _context.SaveChangesAsync();
            return null;
        }

        // Save a match's formation, coaches, and full squad (starting XI + subs)
        public async Task<string> SaveMatchSquadFromScraperAsync(MatchSquadDto squadDto)
        {
            if (squadDto == null || string.IsNullOrWhiteSpace(squadDto.match_id))
                return "No squad data or match_id provided.";

            var match = await _context.Matches
                .Include(m => m.Lineups)
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.MatchId == squadDto.match_id);

            if (match == null)
                return $"No match found with match_id={squadDto.match_id}";

            match.HomeFormation = squadDto.home_formation;
            match.AwayFormation = squadDto.away_formation;
            match.HomeCoach = squadDto.home_coach;
            match.AwayCoach = squadDto.away_coach;

            // Simplest safe approach: wipe old lineup rows for this match and re-insert,
            // so re-imports never leave stale/duplicate players behind.
            if (match.Lineups != null && match.Lineups.Any())
                _context.MatchLineups.RemoveRange(match.Lineups);

            async Task AddPlayersAsync(List<SquadPlayerDto> players, string teamSide, string? teamName, bool isSub)
            {
                if (players == null) return;
                foreach (var p in players)
                {
                    if (string.IsNullOrWhiteSpace(p.name)) continue;

                    _context.MatchLineups.Add(new MatchLineup
                    {
                        MatchId = match.Id,
                        TeamSide = teamSide,
                        PlayerName = p.name.Trim(),
                        Number = p.number,
                        Position = p.position,
                        IsSubstitute = isSub
                    });

                    await UpsertPlayerAsync(p.name, teamName, p.position, p.number);
                }
            }

            var homeTeamName = match.HomeTeam?.Name;
            var awayTeamName = match.AwayTeam?.Name;

            await AddPlayersAsync(squadDto.home_main, "Home", homeTeamName, false);
            await AddPlayersAsync(squadDto.home_sub, "Home", homeTeamName, true);
            await AddPlayersAsync(squadDto.away_main, "Away", awayTeamName, false);
            await AddPlayersAsync(squadDto.away_sub, "Away", awayTeamName, true);

            await _context.SaveChangesAsync();
            return null; // null == success, no error
        }

        private async Task UpsertPlayerAsync(string? name, string? teamName, string? position, string? number)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            var normalizedName = name.Trim().ToLowerInvariant();
            var normalizedTeamName = teamName?.Trim();
            var player = _context.Players.Local.FirstOrDefault(p => p.NormalizedName == normalizedName && p.TeamName == normalizedTeamName)
                ?? await _context.Players.FirstOrDefaultAsync(p => p.NormalizedName == normalizedName && p.TeamName == normalizedTeamName);

            if (player == null)
            {
                _context.Players.Add(new Player
                {
                    Name = name.Trim(),
                    NormalizedName = normalizedName,
                    TeamName = normalizedTeamName,
                    Position = position,
                    ShirtNumber = number
                });
                return;
            }

            player.Position ??= position;
            player.ShirtNumber ??= number;
            player.UpdatedAt = DateTime.UtcNow;
        }

        public async Task<List<Match>> GetMatchesByDateAsync(DateTime date) => await MatchesWithIncludes().Where(m => m.MatchDate == DateOnly.FromDateTime(date)).ToListAsync();

        public async Task<List<Match>> GetAllMatchesAsync() => await MatchesWithIncludes().ToListAsync();

        private IQueryable<Match> MatchesWithIncludes() => _context.Matches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.Tournament)
            .Include(m => m.Streams)
            .Include(m => m.Events)
            .Include(m => m.Videos);
        public async Task<List<News>> GetLatestNewsAsync(int take = 20, string? category = null, string? sportKey = null)
        {
            take = Math.Clamp(take, 1, 100);
            var query = _context.News.AsQueryable();
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(n => n.Category == category);
            if (!string.IsNullOrWhiteSpace(sportKey))
                query = query.Where(n => n.SportKey == sportKey);
            return await query.OrderByDescending(n => n.PublishedAt).Take(take).ToListAsync();
        }
        public async Task<List<StandingEntry>> GetStandingsAsync(int tournamentId) => await _context.StandingEntries.Where(s => s.TournamentId == tournamentId).OrderBy(s => s.GroupName).ThenBy(s => s.Rank).ToListAsync();
        public async Task<List<PlayerScorer>> GetScorersAsync(int tournamentId) => await _context.PlayerScorers.Where(s => s.TournamentId == tournamentId).OrderByDescending(s => s.Goals).ToListAsync();

        public async Task<List<TournamentBracket>> GetBracketAsync(int tournamentId) =>
            await _context.TournamentBrackets.Where(b => b.TournamentId == tournamentId).OrderBy(b => b.RoundName).ThenBy(b => b.MatchOrder).ToListAsync();

        public async Task<Match> GetMatchSquadAsync(string matchId) =>
            await _context.Matches.Include(m => m.Lineups).Include(m => m.HomeTeam).Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

        public async Task<List<MatchVideo>> GetMatchVideosAsync(string matchId)
        {
            var match = await _context.Matches.FirstOrDefaultAsync(m => m.MatchId == matchId);
            if (match == null) return new List<MatchVideo>();
            return await _context.MatchVideos
                .Where(v => v.MatchId == match.Id && v.IsAvailable)
                .OrderBy(v => v.Type)
                .ThenByDescending(v => v.PublishedAt ?? v.CreatedAt)
                .ToListAsync();
        }

        private string Normalize(string text)
        {
            return text?
                .ToLower()
                .Replace(" ", "")
                .Replace("-", "")
                .Trim() ?? "";
        }
    }
}