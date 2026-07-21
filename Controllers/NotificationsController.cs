using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Sports;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NotificationsController(AppDbContext context) => _context = context;

        [HttpPost("api/users/favorite-teams")]
        public async Task<IActionResult> AddFavorite([FromBody] FavoriteTeamRequest request)
        {
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (!await _context.UserFavoriteTeams.AnyAsync(f => f.UserId == request.UserId && f.TeamId == request.TeamId))
            {
                _context.UserFavoriteTeams.Add(new UserFavoriteTeam { UserId = request.UserId, TeamId = request.TeamId });
            }
            await _context.SaveChangesAsync();
            return Ok(new { message = "Favorite team saved." });
        }

        [HttpPost("api/notifications/push-subscriptions")]
        public async Task<IActionResult> SavePushSubscription([FromBody] PushSubscriptionRequest request)
        {
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Endpoint))
            {
                return BadRequest(new { message = "UserId and Endpoint are required." });
            }

            var subscription = await _context.PushSubscriptions.FirstOrDefaultAsync(s => s.UserId == request.UserId && s.Endpoint == request.Endpoint);
            if (subscription == null)
            {
                subscription = new PushSubscription { UserId = request.UserId, Endpoint = request.Endpoint };
                _context.PushSubscriptions.Add(subscription);
            }

            subscription.P256dh = request.P256dh ?? string.Empty;
            subscription.Auth = request.Auth ?? string.Empty;
            subscription.Provider = string.IsNullOrWhiteSpace(request.Provider) ? "WebPush" : request.Provider.Trim();
            subscription.DeviceName = request.DeviceName?.Trim();
            subscription.IsActive = true;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Push subscription saved.", subscription.Id, subscription.Provider });
        }

        [HttpDelete("api/notifications/push-subscriptions/{id:int}")]
        public async Task<IActionResult> DisablePushSubscription(int id)
        {
            var subscription = await _context.PushSubscriptions.FindAsync(id);
            if (subscription == null) return NotFound(new { message = "Push subscription not found." });
            if (!this.IsSelfOrAdmin(subscription.UserId)) return this.ForbiddenUser();
            subscription.IsActive = false;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Push subscription disabled." });
        }

        [HttpGet("api/notifications")]
        public async Task<IActionResult> List([FromQuery] string userId)
        {
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();
            return Ok(await _context.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());
        }

        [HttpPost("api/notifications/{id:int}/read")]
        public async Task<IActionResult> Read(int id)
        {
            var n = await _context.Notifications.FindAsync(id);
            if (n == null) return NotFound();
            if (!this.IsSelfOrAdmin(n.UserId)) return this.ForbiddenUser();
            n.IsRead = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Notification marked as read." });
        }

        [HttpPut("api/notifications/preferences")]
        public async Task<IActionResult> Prefs([FromBody] PreferenceRequest request)
        {
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            var p = await _context.UserNotificationPreferences.FirstOrDefaultAsync(x => x.UserId == request.UserId) ?? new UserNotificationPreference { UserId = request.UserId };
            p.MatchStart = request.MatchStart;
            p.LineupReleased = request.LineupReleased;
            p.Goals = request.Goals;
            p.Summaries = request.Summaries;
            p.FantasyPoints = request.FantasyPoints;
            p.OneHourReminder = request.OneHourReminder;
            p.PushEnabled = request.PushEnabled;
            if (p.Id == 0) _context.UserNotificationPreferences.Add(p);
            await _context.SaveChangesAsync();
            return Ok(p);
        }
    }
}
