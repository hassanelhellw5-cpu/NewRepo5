using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Sports;
using QemmaProject.Models.Requests;
using QemmaProject.Services;
using System.Security.Cryptography;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/fantasy")]
    [Authorize]
    public class FantasyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly FantasyScoringService _fantasyScoring;

        public FantasyController(AppDbContext context, FantasyScoringService fantasyScoring)
        {
            _context = context;
            _fantasyScoring = fantasyScoring;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("players/import")]
        public async Task<IActionResult> ImportPlayers()
        {
            var imported = 0;
            var lineups = await _context.MatchLineups.Include(l => l.Match).ThenInclude(m => m.HomeTeam).Include(l => l.Match).ThenInclude(m => m.AwayTeam).ToListAsync();
            foreach (var l in lineups)
            {
                var teamName = l.TeamSide == "Home" ? l.Match.HomeTeam?.Name : l.Match.AwayTeam?.Name;
                imported += await UpsertPlayerAsync(l.PlayerName, teamName, l.Position, l.Number);
            }
            var scorers = await _context.PlayerScorers.ToListAsync();
            foreach (var s in scorers) imported += await UpsertPlayerAsync(s.PlayerName, s.TeamName, "FW", null);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Players imported from lineups and scorers.", imported });
        }

        [Authorize]
        [HttpPost("contests")]
        public async Task<IActionResult> CreateContest([FromBody] CreateContestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OwnerUserId)) return BadRequest(new { message = "OwnerUserId is required." });
            if (!this.IsSelfOrAdmin(request.OwnerUserId)) return this.ForbiddenUser();

            var date = request.ContestDate.Date == default ? DateTime.UtcNow.Date : request.ContestDate.Date;
            var tournament = await _context.Tournaments.FindAsync(request.TournamentId);
            if (tournament == null) return NotFound(new { message = "Tournament not found." });
            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.OwnerUserId);
            if (owner == null) return NotFound(new { message = "Owner user not found." });

            var strategy = _context.Database.CreateExecutionStrategy();

            FantasyContest contest = null!;
            await strategy.ExecuteAsync(async () =>
            {
                await using var dbTransaction = await _context.Database.BeginTransactionAsync();
                contest = new FantasyContest
                {
                    TournamentId = request.TournamentId,
                    ContestDate = date,
                    Name = string.IsNullOrWhiteSpace(request.Name) ? $"{tournament.Name} Fantasy {date:yyyy-MM-dd}" : request.Name.Trim(),
                    Code = await GenerateUniqueContestCodeAsync(),
                    OwnerUserId = owner.Id,
                    IsPublic = request.IsPublic,
                    MaxMembers = Math.Clamp(request.MaxMembers <= 0 ? 20 : request.MaxMembers, 2, 500),
                    Format = request.Format,
                    KnockoutCurrentRound = request.Format == PredictionLeagueFormat.Knockout ? 1 : 0,
                    KnockoutActivatedAt = request.Format == PredictionLeagueFormat.Knockout ? DateTime.UtcNow : null
                };
                _context.FantasyContests.Add(contest);
                await _context.SaveChangesAsync();
                _context.FantasyEntries.Add(new FantasyEntry
                {
                    FantasyContestId = contest.Id,
                    UserId = owner.Id,
                    Role = PredictionLeagueRole.Owner,
                    KnockoutSeed = contest.Format == PredictionLeagueFormat.Knockout ? 1 : 0,
                    KnockoutRound = contest.Format == PredictionLeagueFormat.Knockout ? 1 : 0
                });
                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();
            });

            return Ok(ToContestResponse(contest));
        }

        [HttpPost("contests/join")]
        public async Task<IActionResult> JoinContest([FromBody] JoinFantasyContestRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { message = "Code is required." });

            var contest = await _context.FantasyContests.Include(c => c.Entries).FirstOrDefaultAsync(c => c.Code == request.Code.Trim().ToUpper());
            if (contest == null || !contest.IsOpen) return NotFound(new { message = "Fantasy contest not found or closed." });
            if (IsKnockoutJoinLocked(contest)) return BadRequest(new { message = "Fantasy contest join is locked because knockout rounds have started." });
            if (contest.Entries.Any(e => e.UserId == request.UserId)) return Ok(new { message = "User already joined this fantasy contest.", contest.Id, contest.Name, contest.Code });
            if (contest.Entries.Count >= contest.MaxMembers) return BadRequest(new { message = "Fantasy contest is full." });
            if (!await _context.Users.AnyAsync(u => u.Id == request.UserId)) return NotFound(new { message = "User not found." });

            var entry = new FantasyEntry { FantasyContestId = contest.Id, UserId = request.UserId, Role = PredictionLeagueRole.Member, KnockoutSeed = contest.Format == PredictionLeagueFormat.Knockout ? contest.Entries.Count + 1 : 0, KnockoutRound = contest.Format == PredictionLeagueFormat.Knockout ? Math.Max(1, contest.KnockoutCurrentRound) : 0 };
            _context.FantasyEntries.Add(entry);
            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Joined fantasy contest successfully.",
                contestId = contest.Id,
                contest.Name,
                contest.Code,
                entryId = entry.Id
            });
        }

        [AllowAnonymous]
        [HttpGet("tournaments/{tournamentId:int}/today/players")]
        public async Task<IActionResult> GetAvailablePlayers(int tournamentId, [FromQuery] DateTime? date = null)
        {
            var day = (date ?? DateTime.UtcNow).Date;
            var matchDay = DateOnly.FromDateTime(day);
            var matches = await _context.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).Where(m => m.TournamentId == tournamentId && m.MatchDate == matchDay).ToListAsync();
            var teamNames = matches
                .SelectMany(m => new[] { m.HomeTeam?.Name, m.AwayTeam?.Name })
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var playerImport = teamNames.Count == 0
                ? CurrentPlayerImportResult.Empty
                : await EnsureCurrentPlayersForTeamsAsync(tournamentId, teamNames, day);
            if (playerImport.Imported > 0) await _context.SaveChangesAsync();

            var players = teamNames.Count == 0 || playerImport.PlayerKeys.Count == 0
                ? new List<Player>()
                : (await _context.Players
                    .Where(p => p.TeamName != null && teamNames.Contains(p.TeamName))
                    .OrderBy(p => p.TeamName)
                    .ThenBy(p => p.Name)
                    .ToListAsync())
                    .Where(p => playerImport.PlayerKeys.Contains(BuildPlayerKey(p.NormalizedName ?? p.Name, p.TeamName)))
                    .ToList();

            return Ok(new
            {
                tournamentId,
                date = day,
                playerSource = playerImport.Imported > 0 ? "imported-from-recent-yallakora-lineups-and-scorers" : "recent-yallakora-lineups-and-scorers",
                imported = playerImport.Imported,
                currentRosterLookbackDays = CurrentRosterLookbackDays,
                matches = matches.Select(m => new { m.Id, home = m.HomeTeam.Name, away = m.AwayTeam.Name, m.MatchDate }),
                players
            });
        }

        [HttpPost("contests/{contestId:int}/entries")]
        public async Task<IActionResult> PickFive(int contestId, [FromBody] PickPlayersRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (request.PlayerIds == null) return BadRequest(new { message = "PlayerIds is required." });
            var ids = request.PlayerIds.Distinct().ToList();
            if (ids.Count != 5) return BadRequest(new { message = "Pick exactly 5 unique players." });
            var contest = await _context.FantasyContests.Include(c => c.Entries).FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });
            if (IsKnockoutJoinLocked(contest) && !contest.Entries.Any(e => e.UserId == request.UserId)) return BadRequest(new { message = "Fantasy contest join is locked because knockout rounds have started." });
            if (contest.Entries.Count >= contest.MaxMembers && !contest.Entries.Any(e => e.UserId == request.UserId)) return BadRequest(new { message = "Fantasy contest is full." });
            var entry = await _context.FantasyEntries.Include(e => e.Picks).FirstOrDefaultAsync(e => e.FantasyContestId == contestId && e.UserId == request.UserId);
            if (entry == null) { entry = new FantasyEntry { FantasyContestId = contestId, UserId = request.UserId, KnockoutSeed = contest.Format == PredictionLeagueFormat.Knockout ? contest.Entries.Count + 1 : 0, KnockoutRound = contest.Format == PredictionLeagueFormat.Knockout ? Math.Max(1, contest.KnockoutCurrentRound) : 0 }; _context.FantasyEntries.Add(entry); }
            _context.FantasyEntryPicks.RemoveRange(entry.Picks);
            entry.Picks = ids.Select(id => new FantasyEntryPick { PlayerId = id }).ToList();
            entry.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Fantasy lineup saved.", entry.Id, picked = ids.Count });
        }


        [HttpPost("contests/{contestId:int}/transfers")]
        public async Task<IActionResult> TransferPlayer(int contestId, [FromBody] FantasyTransferRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (request.OutPlayerId == request.InPlayerId) return BadRequest(new { message = "OutPlayerId and InPlayerId must be different." });

            var contest = await _context.FantasyContests.FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });

            var entry = await _context.FantasyEntries.Include(e => e.Picks).FirstOrDefaultAsync(e => e.FantasyContestId == contestId && e.UserId == request.UserId);
            if (entry == null) return NotFound(new { message = "Save your five-player lineup before making transfers." });
            var outgoing = entry.Picks.FirstOrDefault(p => p.PlayerId == request.OutPlayerId);
            if (outgoing == null) return BadRequest(new { message = "Outgoing player is not in this lineup." });
            if (entry.Picks.Any(p => p.PlayerId == request.InPlayerId)) return BadRequest(new { message = "Incoming player is already in this lineup." });
            if (!await _context.Players.AnyAsync(p => p.Id == request.InPlayerId)) return NotFound(new { message = "Incoming player not found." });

            var currentRound = Math.Max(1, contest.KnockoutCurrentRound == 0 ? 1 : contest.KnockoutCurrentRound);
            if (entry.LastTransferRound != currentRound)
            {
                var rolled = entry.LastTransferRound == 0 ? entry.FreeTransfersBanked : entry.FreeTransfersBanked + 1;
                entry.FreeTransfersBanked = Math.Clamp(rolled, 1, 5);
                entry.TransfersMadeThisRound = 0;
                entry.LastTransferRound = currentRound;
            }

            entry.TransfersMadeThisRound += 1;
            if (entry.FreeTransfersBanked > 0)
            {
                entry.FreeTransfersBanked -= 1;
            }
            else
            {
                entry.TransferPenaltyPoints += 4;
                entry.TotalPoints -= 4;
            }

            outgoing.PlayerId = request.InPlayerId;
            outgoing.Points = 0;
            entry.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Fantasy transfer saved.", entry.Id, entry.FreeTransfersBanked, entry.TransfersMadeThisRound, entry.TransferPenaltyPoints, entry.TotalPoints });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("contests/{contestId:int}/score")]
        public async Task<IActionResult> ScoreContest(int contestId)
        {
            var entries = await _fantasyScoring.ScoreContestAsync(contestId);
            if (entries < 0) return NotFound(new { message = "Contest not found." });
            return Ok(new { message = "Contest scored.", entries });
        }


        [HttpGet("contests/my")]
        public async Task<IActionResult> MyContests([FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();

            var contests = await _context.FantasyEntries
                .Include(e => e.FantasyContest).ThenInclude(c => c.Tournament)
                .Include(e => e.Picks).ThenInclude(p => p.Player)
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.UpdatedAt)
                .Select(e => new
                {
                    entryId = e.Id,
                    e.TotalPoints,
                    e.FreeTransfersBanked,
                    e.TransfersMadeThisRound,
                    e.TransferPenaltyPoints,
                    e.Role,
                    e.KnockoutSeed,
                    e.KnockoutRound,
                    e.IsKnockoutEliminated,
                    e.CreatedAt,
                    e.UpdatedAt,
                    contest = new
                    {
                        e.FantasyContest.Id,
                        e.FantasyContest.Name,
                        e.FantasyContest.Code,
                        e.FantasyContest.TournamentId,
                        tournament = e.FantasyContest.Tournament.Name,
                        e.FantasyContest.ContestDate,
                        e.FantasyContest.IsPublic,
                        e.FantasyContest.IsOpen,
                        e.FantasyContest.Format,
                        e.FantasyContest.KnockoutCurrentRound
                    },
                    lineupRules = new { requiredPlayers = 5, freeTransferPerRound = 1, bankedTransferCap = 5, extraTransferPenalty = 4 },
                    picks = e.Picks.Select(p => new { p.PlayerId, p.Player.Name, p.Player.TeamName, p.Player.Position, p.Player.ShirtNumber, p.Player.ImageUrl, p.Points })
                })
                .ToListAsync();

            return Ok(contests);
        }

        [AllowAnonymous]
        [HttpGet("contests/{contestId:int}/leaderboard")]
        public async Task<IActionResult> Leaderboard(int contestId)
        {
            var contest = await _context.FantasyContests.FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });
            var rows = await _context.FantasyEntries
                .Where(e => e.FantasyContestId == contestId)
                .OrderBy(e => e.IsKnockoutEliminated)
                .ThenByDescending(e => e.KnockoutRound)
                .ThenByDescending(e => e.TotalPoints)
                .ThenBy(e => e.CreatedAt)
                .Select(e => new { e.Id, e.UserId, userName = e.User.UserName, e.Role, e.TotalPoints, e.KnockoutSeed, e.KnockoutRound, e.IsKnockoutEliminated, transferSummary = new { e.FreeTransfersBanked, e.TransfersMadeThisRound, e.TransferPenaltyPoints }, picks = e.Picks.Select(p => new { p.PlayerId, p.Player.Name, p.Player.TeamName, p.Player.Position, p.Player.ImageUrl, p.Points }) })
                .ToListAsync();
            return Ok(new { contest.Id, contest.Name, contest.Code, contest.Format, contest.KnockoutCurrentRound, leaderboard = rows });
        }

        [AllowAnonymous]
        [HttpGet("contests/code/{code}")]
        public async Task<IActionResult> GetContestByCode(string code)
        {
            var contest = await _context.FantasyContests.Include(c => c.Tournament).FirstOrDefaultAsync(c => c.Code == code.Trim().ToUpper());
            return contest == null ? NotFound(new { message = "Fantasy contest not found." }) : Ok(ToContestResponse(contest));
        }

        [HttpPost("contests/{contestId:int}/convert-to-knockout")]
        public async Task<IActionResult> ConvertContestToKnockout(int contestId, [FromBody] ConvertToKnockoutRequest request)
        {
            request ??= new ConvertToKnockoutRequest();
            var contest = await _context.FantasyContests.Include(c => c.Entries).ThenInclude(e => e.User).FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });
            if (!IsContestOwnerOrAdmin(contest)) return this.ForbiddenUser();
            if (contest.Entries.Count < 2) return BadRequest(new { message = "Knockout needs at least 2 members." });
            SeedFantasyKnockout(contest, request.WinnerBonusPoints);
            contest.IsOpen = false;
            await _context.SaveChangesAsync();
            return Ok(BuildFantasyKnockoutResponse(contest, contest.Entries));
        }

        [HttpPost("contests/{contestId:int}/advance-knockout-round")]
        public async Task<IActionResult> AdvanceContestKnockoutRound(int contestId)
        {
            var contest = await _context.FantasyContests.Include(c => c.Entries).ThenInclude(e => e.User).FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });
            if (!IsContestOwnerOrAdmin(contest)) return this.ForbiddenUser();
            if (contest.Format != PredictionLeagueFormat.Knockout) return BadRequest(new { message = "Fantasy contest is not in knockout format." });

            var activeEntries = contest.Entries.Where(e => !e.IsKnockoutEliminated && e.KnockoutRound == contest.KnockoutCurrentRound).OrderByDescending(e => e.TotalPoints).ThenBy(e => e.KnockoutSeed).ToList();
            if (activeEntries.Count <= 1)
            {
                var champion = activeEntries.FirstOrDefault() ?? contest.Entries.Where(e => !e.IsKnockoutEliminated).OrderByDescending(e => e.TotalPoints).ThenBy(e => e.KnockoutSeed).FirstOrDefault();
                if (champion == null) return BadRequest(new { message = "No active knockout entry found." });
                champion.TotalPoints += (int)contest.KnockoutWinnerBonusPoints;
                contest.IsOpen = false;
                contest.KnockoutCompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Fantasy knockout completed.", champion = ToFantasyKnockoutMember(champion), contest.KnockoutWinnerBonusPoints });
            }

            var winners = new List<FantasyEntry>();
            var eliminated = new List<FantasyEntry>();
            for (var i = 0; i < activeEntries.Count; i += 2)
            {
                if (i == activeEntries.Count - 1) { winners.Add(activeEntries[i]); continue; }
                var first = activeEntries[i];
                var second = activeEntries[i + 1];
                var winner = first.TotalPoints > second.TotalPoints ? first : second.TotalPoints > first.TotalPoints ? second : first.KnockoutSeed <= second.KnockoutSeed ? first : second;
                var loser = winner.Id == first.Id ? second : first;
                winners.Add(winner);
                loser.IsKnockoutEliminated = true;
                eliminated.Add(loser);
            }
            contest.KnockoutCurrentRound += 1;
            contest.IsOpen = false;
            foreach (var winner in winners) winner.KnockoutRound = contest.KnockoutCurrentRound;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Fantasy knockout round advanced.", contest.Id, contest.KnockoutCurrentRound, winners = winners.Select(ToFantasyKnockoutMember), eliminated = eliminated.Select(ToFantasyKnockoutMember), knockout = BuildFantasyKnockoutResponse(contest, contest.Entries) });
        }

        [AllowAnonymous]
        [HttpGet("contests/{contestId:int}/knockout")]
        public async Task<IActionResult> GetContestKnockout(int contestId)
        {
            var contest = await _context.FantasyContests.Include(c => c.Entries).ThenInclude(e => e.User).FirstOrDefaultAsync(c => c.Id == contestId);
            if (contest == null) return NotFound(new { message = "Contest not found." });
            return Ok(BuildFantasyKnockoutResponse(contest, contest.Entries));
        }

        private bool IsContestOwnerOrAdmin(FantasyContest contest) => User.IsInRole("Admin") || this.IsSelfOrAdmin(contest.OwnerUserId);
        private static bool IsKnockoutJoinLocked(FantasyContest contest) => contest.Format == PredictionLeagueFormat.Knockout && (contest.KnockoutCurrentRound > 1 || contest.KnockoutActivatedAt.HasValue && !contest.IsOpen);

        private async Task<string> GenerateUniqueContestCodeAsync()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var bytes = RandomNumberGenerator.GetBytes(6);
                var code = new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
                if (!await _context.FantasyContests.AnyAsync(c => c.Code == code)) return code;
            }
            return Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        }

        private static object ToContestResponse(FantasyContest contest) => new
        {
            contest.Id,
            contest.Name,
            contest.Code,
            contest.TournamentId,
            contest.ContestDate,
            contest.OwnerUserId,
            contest.IsPublic,
            contest.MaxMembers,
            contest.Format,
            contest.KnockoutCurrentRound,
            contest.KnockoutWinnerBonusPoints,
            contest.KnockoutActivatedAt,
            contest.KnockoutCompletedAt,
            contest.IsOpen
        };

        private static void SeedFantasyKnockout(FantasyContest contest, decimal winnerBonusPoints)
        {
            var seededEntries = contest.Entries.OrderByDescending(e => e.TotalPoints).ThenBy(e => e.CreatedAt).ToList();
            for (var i = 0; i < seededEntries.Count; i++)
            {
                seededEntries[i].KnockoutSeed = i + 1;
                seededEntries[i].KnockoutRound = 1;
                seededEntries[i].IsKnockoutEliminated = false;
            }
            contest.Format = PredictionLeagueFormat.Knockout;
            contest.KnockoutCurrentRound = 1;
            contest.KnockoutWinnerBonusPoints = winnerBonusPoints <= 0 ? 100m : winnerBonusPoints;
            contest.KnockoutActivatedAt = DateTime.UtcNow;
            contest.KnockoutCompletedAt = null;
        }

        private static object BuildFantasyKnockoutResponse(FantasyContest contest, IEnumerable<FantasyEntry> entries)
        {
            var bracket = entries.GroupBy(e => Math.Max(1, e.KnockoutRound)).OrderBy(g => g.Key).Select(g => new
            {
                round = g.Key,
                members = g.OrderBy(e => e.IsKnockoutEliminated).ThenBy(e => e.KnockoutSeed == 0 ? int.MaxValue : e.KnockoutSeed).Select(ToFantasyKnockoutMember).ToList()
            }).ToList();
            var champion = !contest.IsOpen && contest.KnockoutCompletedAt.HasValue
                ? entries.Where(e => !e.IsKnockoutEliminated).OrderByDescending(e => e.TotalPoints).ThenBy(e => e.KnockoutSeed == 0 ? int.MaxValue : e.KnockoutSeed).Select(ToFantasyKnockoutMember).FirstOrDefault()
                : null;
            return new { contest.Id, contest.Name, contest.Code, contest.IsOpen, contest.Format, contest.KnockoutCurrentRound, contest.KnockoutWinnerBonusPoints, contest.KnockoutActivatedAt, contest.KnockoutCompletedAt, champion, bracket };
        }

        private static object ToFantasyKnockoutMember(FantasyEntry entry) => new
        {
            entry.Id,
            entry.UserId,
            userName = entry.User?.UserName,
            entry.Role,
            entry.TotalPoints,
            entry.KnockoutSeed,
            entry.KnockoutRound,
            entry.IsKnockoutEliminated,
            entry.CreatedAt
        };

        private const int CurrentRosterLookbackDays = 180;
        private const int CurrentRosterRecentMatchLimit = 8;

        private async Task<CurrentPlayerImportResult> EnsureCurrentPlayersForTeamsAsync(int tournamentId, IReadOnlyCollection<string> teamNames, DateTime contestDay)
        {
            var imported = 0;
            var playerKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normalizedTeamNames = teamNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedTeamNames.Count == 0) return new CurrentPlayerImportResult(imported, playerKeys);

            var earliestCurrentRosterDate = DateOnly.FromDateTime(contestDay.AddDays(-CurrentRosterLookbackDays));
            var contestMatchDay = DateOnly.FromDateTime(contestDay);
            var recentLineups = await _context.MatchLineups
                .Include(l => l.Match).ThenInclude(m => m.HomeTeam)
                .Include(l => l.Match).ThenInclude(m => m.AwayTeam)
                .Where(l => l.Match.MatchDate >= earliestCurrentRosterDate && l.Match.MatchDate <= contestMatchDay &&
                    ((l.TeamSide == "Home" && l.Match.HomeTeam != null && normalizedTeamNames.Contains(l.Match.HomeTeam.Name)) ||
                    (l.TeamSide == "Away" && l.Match.AwayTeam != null && normalizedTeamNames.Contains(l.Match.AwayTeam.Name))))
                .ToListAsync();

            var currentLineups = recentLineups
                .GroupBy(l => ResolveLineupTeamName(l) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .SelectMany(teamGroup => teamGroup
                    .GroupBy(l => l.MatchId)
                    .OrderByDescending(matchGroup => matchGroup.Max(l => l.Match.MatchDate))
                    .Take(CurrentRosterRecentMatchLimit)
                    .SelectMany(matchGroup => matchGroup))
                .ToList();

            foreach (var lineup in currentLineups)
            {
                var teamName = ResolveLineupTeamName(lineup);
                if (string.IsNullOrWhiteSpace(teamName)) continue;

                playerKeys.Add(BuildPlayerKey(lineup.PlayerName, teamName));
                imported += await UpsertPlayerAsync(lineup.PlayerName, teamName, lineup.Position, lineup.Number);
            }

            var scorerPlayers = await _context.PlayerScorers
                .Where(s => s.TournamentId == tournamentId && normalizedTeamNames.Contains(s.TeamName))
                .ToListAsync();

            foreach (var scorer in scorerPlayers)
            {
                playerKeys.Add(BuildPlayerKey(scorer.PlayerName, scorer.TeamName));
                imported += await UpsertPlayerAsync(scorer.PlayerName, scorer.TeamName, "FW", null);
            }

            return new CurrentPlayerImportResult(imported, playerKeys);
        }

        private sealed record CurrentPlayerImportResult(int Imported, HashSet<string> PlayerKeys)
        {
            public static CurrentPlayerImportResult Empty { get; } = new(0, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static string BuildPlayerKey(string? playerName, string? teamName) => $"{playerName?.Trim().ToLowerInvariant()}|{teamName?.Trim().ToLowerInvariant()}";

        private static string? ResolveLineupTeamName(MatchLineup lineup)
        {
            return lineup.TeamSide == "Home"
                ? lineup.Match.HomeTeam?.Name
                : lineup.TeamSide == "Away"
                    ? lineup.Match.AwayTeam?.Name
                    : null;
        }

        private async Task<int> UpsertPlayerAsync(string name, string? teamName, string? position, string? number)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;

            var normalizedName = name.Trim().ToLowerInvariant();
            var normalizedTeamName = teamName?.Trim();
            var player = _context.Players.Local.FirstOrDefault(p => p.NormalizedName == normalizedName && p.TeamName == normalizedTeamName)
                ?? await _context.Players.FirstOrDefaultAsync(p => p.NormalizedName == normalizedName && p.TeamName == normalizedTeamName);

            if (player != null)
            {
                player.Position ??= position;
                player.ShirtNumber ??= number;
                player.UpdatedAt = DateTime.UtcNow;
                return 0;
            }

            _context.Players.Add(new Player
            {
                Name = name.Trim(),
                NormalizedName = normalizedName,
                TeamName = normalizedTeamName,
                Position = position,
                ShirtNumber = number
            });
            return 1;
        }

        private async Task BuildPlayerStatsAsync(List<int> matchIds)
        {
            foreach (var matchId in matchIds)
            {
                var lineups = await _context.MatchLineups.Include(l => l.Match).ThenInclude(m => m.HomeTeam).Include(l => l.Match).ThenInclude(m => m.AwayTeam).Where(l => l.MatchId == matchId).ToListAsync();
                foreach (var l in lineups)
                {
                    var teamName = l.TeamSide == "Home" ? l.Match.HomeTeam?.Name : l.Match.AwayTeam?.Name;
                    await UpsertPlayerAsync(l.PlayerName, teamName, l.Position, l.Number);
                }
                await _context.SaveChangesAsync();
                var events = await _context.MatchEvents.Where(e => e.MatchId == matchId).ToListAsync();
                foreach (var l in lineups)
                {
                    var teamName = l.TeamSide == "Home" ? l.Match.HomeTeam?.Name : l.Match.AwayTeam?.Name;
                    var player = await _context.Players.FirstAsync(p => p.NormalizedName == l.PlayerName.Trim().ToLowerInvariant() && p.TeamName == teamName);
                    var stat = await _context.PlayerMatchStats.FirstOrDefaultAsync(s => s.PlayerId == player.Id && s.MatchId == matchId) ?? new PlayerMatchStat { PlayerId = player.Id, MatchId = matchId };
                    stat.Started = !l.IsSubstitute; stat.Substitute = l.IsSubstitute;
                    stat.MinutesPlayed = stat.Started ? 90 : 25;
                    stat.Goals = events.Count(e => Contains(e.Type, "Goal") && SamePlayer(e.PlayerName, l.PlayerName));
                    stat.YellowCards = events.Count(e => Contains(e.Type, "Card") && SamePlayer(e.PlayerName, l.PlayerName) && !Contains(e.Detail, "red"));
                    stat.RedCards = events.Count(e => Contains(e.Type, "Card") && SamePlayer(e.PlayerName, l.PlayerName) && Contains(e.Detail, "red"));
                    stat.Assists = events.Count(e => SamePlayer(e.AssistPlayerName, l.PlayerName));
                    stat.Shots = events.Count(e => SamePlayer(e.PlayerName, l.PlayerName) && (Contains(e.Type, "Shot") || Contains(e.Detail, "shot") || Contains(e.Detail, "تسديد")));
                    stat.KeyPasses = stat.Assists + events.Count(e => SamePlayer(e.PlayerName, l.PlayerName) && (Contains(e.Detail, "key pass") || Contains(e.Detail, "فرصة")));
                    stat.Saves = Contains(l.Position, "GK") ? events.Count(e => e.TeamSide != l.TeamSide && (Contains(e.Detail, "save") || Contains(e.Detail, "تصدي"))) : 0;
                    stat.PenaltiesScored = events.Count(e => SamePlayer(e.PlayerName, l.PlayerName) && Contains(e.Detail, "penalty") && Contains(e.Type, "Goal"));
                    stat.PenaltiesMissed = events.Count(e => SamePlayer(e.PlayerName, l.PlayerName) && Contains(e.Detail, "penalty") && !Contains(e.Type, "Goal"));
                    stat.CleanSheet = IsDefensivePosition(l.Position) && IsCleanSheet(l.TeamSide, l.Match);
                    stat.FantasyPoints = CalculateFantasyPoints(stat, l.Position);
                    stat.Rating = Math.Clamp(6 + stat.Goals + stat.Assists * .5m + stat.KeyPasses * .1m + stat.Saves * .15m - stat.YellowCards * .2m - stat.RedCards - stat.PenaltiesMissed * .7m, 1, 10);
                    if (stat.Id == 0) _context.PlayerMatchStats.Add(stat);
                }
            }
        }

        private static int CalculateFantasyPoints(PlayerMatchStat stat, string? position)
        {
            var points = (stat.MinutesPlayed >= 60 ? 2 : 1)
                + stat.Goals * GoalWeight(position)
                + stat.Assists * 3
                + stat.Shots
                + stat.KeyPasses
                + stat.Saves
                + stat.PenaltiesScored * 2
                + (stat.CleanSheet ? CleanSheetWeight(position) : 0)
                - stat.YellowCards
                - stat.RedCards * 3
                - stat.PenaltiesMissed * 2;
            return Math.Max(0, points);
        }

        private static int GoalWeight(string? position) => IsDefensivePosition(position) ? 6 : Contains(position, "MF") ? 5 : 4;
        private static int CleanSheetWeight(string? position) => Contains(position, "GK") ? 4 : Contains(position, "DF") ? 4 : Contains(position, "MF") ? 1 : 0;
        private static bool IsDefensivePosition(string? position) => Contains(position, "GK") || Contains(position, "DF") || Contains(position, "Defender");
        private static bool IsCleanSheet(string? teamSide, Match match) => teamSide == "Home" ? match.ScoreAway == "0" : teamSide == "Away" && match.ScoreHome == "0";
        private static bool Contains(string? value, string needle) => !string.IsNullOrWhiteSpace(value) && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
        private static bool SamePlayer(string? left, string? right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && left.Trim().Equals(right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
