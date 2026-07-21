using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Sports;

namespace QemmaProject.Services
{
    public class FantasyScoringService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<FantasyScoringService> _logger;

        public FantasyScoringService(AppDbContext context, ILogger<FantasyScoringService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> ScoreContestAsync(int contestId, CancellationToken cancellationToken = default)
        {
            var contest = await _context.FantasyContests.Include(c => c.Tournament).FirstOrDefaultAsync(c => c.Id == contestId, cancellationToken);
            if (contest == null) return -1;

            var day = DateOnly.FromDateTime(contest.ContestDate.Date);
            var matchIds = await _context.Matches
                .Where(m => m.TournamentId == contest.TournamentId && m.MatchDate == day)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken);

            await BuildPlayerStatsAsync(matchIds, cancellationToken);

            var entries = await _context.FantasyEntries
                .Include(e => e.Picks)
                .Where(e => e.FantasyContestId == contestId)
                .ToListAsync(cancellationToken);

            foreach (var entry in entries)
            {
                foreach (var pick in entry.Picks)
                {
                    pick.Points = await _context.PlayerMatchStats
                        .Where(s => s.PlayerId == pick.PlayerId && matchIds.Contains(s.MatchId))
                        .SumAsync(s => s.FantasyPoints, cancellationToken);
                }

                entry.TotalPoints = entry.Picks.Sum(p => p.Points);
                entry.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return entries.Count;
        }

        public async Task<int> ScoreRecentlyPlayableContestsAsync(CancellationToken cancellationToken = default)
        {
            var from = DateTime.UtcNow.Date.AddDays(-2);
            var to = DateTime.UtcNow.Date.AddDays(1);
            var contests = await _context.FantasyContests
                .Where(c => c.ContestDate >= from && c.ContestDate < to && !c.KnockoutCompletedAt.HasValue)
                .Select(c => new { c.Id, c.TournamentId, c.ContestDate })
                .ToListAsync(cancellationToken);

            var scored = 0;
            foreach (var contest in contests)
            {
                var day = DateOnly.FromDateTime(contest.ContestDate.Date);
                var hasScoreableMatch = await _context.Matches.AnyAsync(m =>
                    m.TournamentId == contest.TournamentId &&
                    m.MatchDate == day &&
                    !string.IsNullOrWhiteSpace(m.ScoreHome) &&
                    !string.IsNullOrWhiteSpace(m.ScoreAway) &&
                    ((m.Status ?? "").Contains("Finished") || (m.Status ?? "").Contains("FT") || (m.Status ?? "").Contains("انته")), cancellationToken);

                if (!hasScoreableMatch) continue;

                var entries = await ScoreContestAsync(contest.Id, cancellationToken);
                if (entries >= 0) scored++;
            }

            if (scored > 0) _logger.LogInformation("Auto-scored {ContestCount} fantasy contests after match sync.", scored);
            return scored;
        }

        private async Task<int> UpsertPlayerAsync(string name, string? teamName, string? position, string? number, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return 0;
            var norm = name.Trim().ToLowerInvariant();
            var player = await _context.Players.FirstOrDefaultAsync(p => p.NormalizedName == norm && p.TeamName == teamName, cancellationToken);
            if (player != null) { player.Position ??= position; player.ShirtNumber ??= number; return 0; }
            _context.Players.Add(new Player { Name = name.Trim(), NormalizedName = norm, TeamName = teamName, Position = position, ShirtNumber = number });
            return 1;
        }

        private async Task BuildPlayerStatsAsync(List<int> matchIds, CancellationToken cancellationToken)
        {
            foreach (var matchId in matchIds)
            {
                var lineups = await _context.MatchLineups.Include(l => l.Match).ThenInclude(m => m.HomeTeam).Include(l => l.Match).ThenInclude(m => m.AwayTeam).Where(l => l.MatchId == matchId).ToListAsync(cancellationToken);
                foreach (var l in lineups)
                {
                    var teamName = l.TeamSide == "Home" ? l.Match.HomeTeam?.Name : l.Match.AwayTeam?.Name;
                    await UpsertPlayerAsync(l.PlayerName, teamName, l.Position, l.Number, cancellationToken);
                }
                await _context.SaveChangesAsync(cancellationToken);
                var events = await _context.MatchEvents.Where(e => e.MatchId == matchId).ToListAsync(cancellationToken);
                foreach (var l in lineups)
                {
                    var teamName = l.TeamSide == "Home" ? l.Match.HomeTeam?.Name : l.Match.AwayTeam?.Name;
                    var player = await _context.Players.FirstAsync(p => p.NormalizedName == l.PlayerName.Trim().ToLowerInvariant() && p.TeamName == teamName, cancellationToken);
                    var stat = await _context.PlayerMatchStats.FirstOrDefaultAsync(s => s.PlayerId == player.Id && s.MatchId == matchId, cancellationToken) ?? new PlayerMatchStat { PlayerId = player.Id, MatchId = matchId };
                    stat.Started = !l.IsSubstitute; stat.Substitute = l.IsSubstitute; stat.MinutesPlayed = stat.Started ? 90 : 25;
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
            var points = (stat.MinutesPlayed >= 60 ? 2 : 1) + stat.Goals * GoalWeight(position) + stat.Assists * 3 + stat.Shots + stat.KeyPasses + stat.Saves + stat.PenaltiesScored * 2 + (stat.CleanSheet ? CleanSheetWeight(position) : 0) - stat.YellowCards - stat.RedCards * 3 - stat.PenaltiesMissed * 2;
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
