using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api")]
    public class PlayersController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PlayersController(AppDbContext context) => _context = context;

        [HttpGet("players/{playerId:int}/card")]
        public async Task<IActionResult> PlayerCard(int playerId)
        {
            var player = await _context.Players.Include(p => p.Team).FirstOrDefaultAsync(p => p.Id == playerId);
            if (player == null) return NotFound(new { message = "Player not found." });
            var stats = await _context.PlayerMatchStats
                .Include(s => s.Player)
                .Include(s => s.Match).ThenInclude(m => m.HomeTeam)
                .Include(s => s.Match).ThenInclude(m => m.AwayTeam)
                .Where(s => s.PlayerId == playerId)
                .ToListAsync();
            var teamHistory = BuildTeamHistory(player, stats);
            return Ok(new
            {
                player.Id,
                player.Name,
                team = player.Team?.Name ?? player.TeamName,
                player.TeamName,
                player.Position,
                player.ShirtNumber,
                player.ImageUrl,
                player.ImageSource,
                player.ExternalPlayerId,
                player.Nationality,
                player.BirthDate,
                player.Age,
                player.Height,
                player.PreferredFoot,
                formerTeams = ParseJsonArray(player.FormerTeamsJson),
                profileMetadata = player.ProfileMetadataJson,
                rating = stats.Count == 0 ? 6 : Math.Round(stats.Average(s => (double)s.Rating), 1),
                goals = stats.Sum(s => s.Goals),
                assists = stats.Sum(s => s.Assists),
                yellowCards = stats.Sum(s => s.YellowCards),
                redCards = stats.Sum(s => s.RedCards),
                minutesPlayed = stats.Sum(s => s.MinutesPlayed),
                shots = stats.Sum(s => s.Shots),
                keyPasses = stats.Sum(s => s.KeyPasses),
                saves = stats.Sum(s => s.Saves),
                cleanSheets = stats.Count(s => s.CleanSheet),
                fantasyPoints = stats.Sum(s => s.FantasyPoints),
                matches = stats.Count,
                teamHistory,
                recentMatches = stats.OrderByDescending(s => s.Match.MatchDate).Take(10).Select(s => new { s.MatchId, s.Match.MatchDate, opponent = OpponentName(s), s.MinutesPlayed, s.Goals, s.Assists, s.FantasyPoints, s.Rating }),
                shareImage = $"/api/players/{player.Id}/card/share-image"
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("players/{playerId:int}/image")]
        public async Task<IActionResult> UpdatePlayerImage(int playerId, [FromBody] UpdatePlayerImageRequest request)
        {
            var player = await _context.Players.FindAsync(playerId);
            if (player == null) return NotFound(new { message = "Player not found." });
            player.ImageUrl = request.ImageUrl?.Trim();
            player.ImageSource = request.ImageSource?.Trim();
            player.ExternalPlayerId = request.ExternalPlayerId?.Trim();
            player.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Player image updated.", player.Id, player.ImageUrl, player.ImageSource, player.ExternalPlayerId });
        }

        [HttpGet("matches/{matchId:int}/player-cards")]
        public async Task<IActionResult> MatchPlayerCards(int matchId) => Ok(await _context.PlayerMatchStats.Where(s => s.MatchId == matchId).Include(s => s.Player).Select(s => new { s.PlayerId, s.Player.Name, team = s.Player.TeamName, s.Player.ImageUrl, s.Rating, s.Goals, s.Assists, fantasyPoints = s.FantasyPoints }).ToListAsync());

        private static IEnumerable<object> ParseJsonArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
            try { return System.Text.Json.JsonSerializer.Deserialize<List<object>>(json) ?? Array.Empty<object>(); }
            catch { return Array.Empty<object>(); }
        }

        private static IEnumerable<object> BuildTeamHistory(Models.Sports.Player player, IEnumerable<Models.Sports.PlayerMatchStat> stats)
        {
            var teams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(player.TeamName)) teams.Add(player.TeamName);
            foreach (var stat in stats)
            {
                if (!string.IsNullOrWhiteSpace(player.TeamName)) teams.Add(player.TeamName);
            }
            return teams.Select(name => new { teamName = name, source = "lineups-and-player-profile" });
        }

        private static string? OpponentName(Models.Sports.PlayerMatchStat stat)
        {
            var current = stat.Player.TeamName;
            var home = stat.Match.HomeTeam?.Name;
            var away = stat.Match.AwayTeam?.Name;
            if (!string.IsNullOrWhiteSpace(current) && current.Equals(home, StringComparison.OrdinalIgnoreCase)) return away;
            if (!string.IsNullOrWhiteSpace(current) && current.Equals(away, StringComparison.OrdinalIgnoreCase)) return home;
            return string.Join(" vs ", new[] { home, away }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }
    }
}
