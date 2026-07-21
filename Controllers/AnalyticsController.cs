using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AnalyticsController(AppDbContext context) => _context = context;

        [HttpGet("matches/{matchId:int}/momentum")]
        public async Task<IActionResult> Momentum(int matchId)
        {
            var match = await _context.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) return NotFound(new { message = "Match not found." });
            var events = await _context.MatchEvents.Where(e => e.MatchId == matchId).ToListAsync();
            var stats = await _context.MatchStatistics.Where(s => s.MatchId == matchId).ToListAsync();
            var points = Enumerable.Range(1, 18).Select(i => i * 5).Select(minute =>
            {
                var home = 50; var away = 50; var reasons = new List<string>();
                foreach (var e in events.Where(e => ParseMinute(e.Minute) <= minute && ParseMinute(e.Minute) > minute - 5))
                {
                    var weight = e.Type.Contains("Goal", StringComparison.OrdinalIgnoreCase) ? 18 : e.Type.Contains("Card", StringComparison.OrdinalIgnoreCase) ? -7 : e.Type.Contains("Sub", StringComparison.OrdinalIgnoreCase) ? 3 : 5;
                    if (e.TeamSide == "Home") home += weight; else if (e.TeamSide == "Away") away += weight;
                    if (!string.IsNullOrWhiteSpace(e.Detail)) reasons.Add(e.Detail);
                }
                foreach (var s in stats.Where(s => s.Name.Contains("possession", StringComparison.OrdinalIgnoreCase) || s.Name.Contains("shot", StringComparison.OrdinalIgnoreCase)))
                {
                    home += ParseNumber(s.HomeValue) / 10; away += ParseNumber(s.AwayValue) / 10;
                }
                home = Math.Clamp(home, 0, 100); away = Math.Clamp(away, 0, 100); var total = Math.Max(1, home + away);
                return new { minute, homeMomentum = home * 100 / total, awayMomentum = away * 100 / total, reason = reasons.Count == 0 ? "Recent pressure from events and statistics" : string.Join(" + ", reasons.Take(2)) };
            });
            return Ok(new { matchId, homeTeam = match.HomeTeam.Name, awayTeam = match.AwayTeam.Name, points });
        }
        private static int ParseMinute(string? value) => int.TryParse(new string((value ?? "0").TakeWhile(char.IsDigit).ToArray()), out var m) ? m : 0;
        private static int ParseNumber(string? value) => int.TryParse(new string((value ?? "0").Where(char.IsDigit).ToArray()), out var n) ? n : 0;
    }
}
