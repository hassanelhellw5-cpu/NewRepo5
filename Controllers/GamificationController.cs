using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Sports;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/gamification")]
    [Authorize]
    public class GamificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GamificationController(AppDbContext context) => _context = context;
        [HttpPost("streaks/{userId}/touch")]
        public async Task<IActionResult> Touch(string userId, [FromQuery] string type = "DailyLogin")
        { if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser(); var today = DateTime.UtcNow.Date; var s = await _context.UserStreaks.FirstOrDefaultAsync(x => x.UserId == userId && x.Type == type) ?? new UserStreak { UserId = userId, Type = type }; if (s.LastActivityDate?.Date == today) return Ok(s); s.CurrentCount = s.LastActivityDate?.Date == today.AddDays(-1) ? s.CurrentCount + 1 : 1; s.BestCount = Math.Max(s.BestCount, s.CurrentCount); s.LastActivityDate = today; if (s.Id == 0) _context.UserStreaks.Add(s); await _context.SaveChangesAsync(); return Ok(s); }
        [HttpGet("streaks/{userId}")]
        public async Task<IActionResult> Streaks(string userId) => !this.IsSelfOrAdmin(userId) ? this.ForbiddenUser() : Ok(await _context.UserStreaks.Where(s => s.UserId == userId).ToListAsync());
        [HttpGet("achievements/{userId}")]
        public async Task<IActionResult> Achievements(string userId) => !this.IsSelfOrAdmin(userId) ? this.ForbiddenUser() : Ok(await _context.UserAchievements.Include(a => a.AchievementDefinition).Where(a => a.UserId == userId).Select(a => new { a.UnlockedAt, a.AchievementDefinition.Code, a.AchievementDefinition.Name, a.AchievementDefinition.Description, a.AchievementDefinition.RewardType, a.AchievementDefinition.RewardCoins }).ToListAsync());
    }
}
