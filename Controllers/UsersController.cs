using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{userId}/stats")]
        public async Task<IActionResult> GetUserStats(string userId)
        {
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var predictions = await _context.Predictions.Where(p => p.UserId == userId).ToListAsync();
            var leagueRows = await _context.PredictionLeagueMembers.Where(m => m.UserId == userId).ToListAsync();
            var fantasyEntries = await _context.FantasyEntries.Where(e => e.UserId == userId).ToListAsync();
            var achievements = await _context.UserAchievements.Where(a => a.UserId == userId).CountAsync();
            var streaks = await _context.UserStreaks.Where(s => s.UserId == userId).ToListAsync();
            var watchParties = await _context.WatchPartyMembers.Where(m => m.UserId == userId).CountAsync();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.QemmaCoinsBalance,
                user.IsEliteSubscriber,
                user.EliteSubscriptionEndDate,
                predictions = new
                {
                    total = predictions.Count,
                    processed = predictions.Count(p => p.IsProcessed),
                    correct = predictions.Count(p => p.IsProcessed && p.PointsEarned > 0),
                    totalPoints = predictions.Sum(p => p.PointsEarned),
                    exactScoreAttempts = predictions.Count(p => p.PredictedHomeScore >= 0 && p.PredictedAwayScore >= 0)
                },
                predictionLeagues = new
                {
                    joined = leagueRows.Count,
                    totalPoints = leagueRows.Sum(l => l.TotalPoints),
                    bestPoints = leagueRows.Count == 0 ? 0 : leagueRows.Max(l => l.TotalPoints),
                    knockoutRuns = leagueRows.Count(l => l.KnockoutRound > 0),
                    eliminated = leagueRows.Count(l => l.IsKnockoutEliminated)
                },
                fantasy = new
                {
                    contests = fantasyEntries.Count,
                    totalPoints = fantasyEntries.Sum(e => e.TotalPoints),
                    bestContestPoints = fantasyEntries.Count == 0 ? 0 : fantasyEntries.Max(e => e.TotalPoints),
                    knockoutRuns = fantasyEntries.Count(e => e.KnockoutRound > 0)
                },
                engagement = new
                {
                    achievements,
                    watchParties,
                    bestStreak = streaks.Count == 0 ? 0 : streaks.Max(s => s.BestCount),
                    currentStreaks = streaks.Select(s => new { s.Type, s.CurrentCount, s.BestCount, s.LastActivityDate })
                }
            });
        }
    }
}
