using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using QemmaProject.Data;
using QemmaProject.Models.Sports;
using QemmaProject.Hubs;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/watch-parties")]
    [Authorize]
    public class WatchPartiesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<MatchHub> _hubContext;
        public WatchPartiesController(AppDbContext context, IHubContext<MatchHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWatchPartyRequest request)
        {
            if (!this.IsSelfOrAdmin(request.HostUserId)) return this.ForbiddenUser();
            if (!await _context.Matches.AnyAsync(m => m.Id == request.MatchId)) return NotFound(new { message = "Match not found." });
            var room = new WatchPartyRoom { MatchId = request.MatchId, HostUserId = request.HostUserId, Title = request.Title ?? "Watch Party", Code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() };
            room.Members.Add(new WatchPartyMember { UserId = request.HostUserId, IsHost = true });
            _context.WatchPartyRooms.Add(room); await _context.SaveChangesAsync();
            return Ok(new { room.Id, room.Code, room.Title, inviteLink = $"/watch-party/{room.Code}" });
        }
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var rooms = await _context.WatchPartyRooms
                .Include(r => r.Match).ThenInclude(m => m.HomeTeam)
                .Include(r => r.Match).ThenInclude(m => m.AwayTeam)
                .Include(r => r.Members)
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.Code,
                    r.Title,
                    r.MatchId,
                    homeTeam = r.Match.HomeTeam.Name,
                    awayTeam = r.Match.AwayTeam.Name,
                    r.Match.Status,
                    r.Match.Time,
                    r.Match.ScoreHome,
                    r.Match.ScoreAway,
                    r.HostUserId,
                    members = r.Members.Count,
                    r.CreatedAt
                })
                .ToListAsync();
            return Ok(rooms);
        }

        [AllowAnonymous]
        [HttpGet("{code}")]
        public async Task<IActionResult> Get(string code)
        {
            var room = await _context.WatchPartyRooms.Include(r => r.Match).Include(r => r.Members)
                .Where(r => r.Code == code)
                .Select(r => new { r.Code, r.Title, r.MatchId, r.HostUserId, members = r.Members.Count, r.IsActive })
                .FirstOrDefaultAsync();
            return room == null ? NotFound(new { message = "Room not found." }) : Ok(room);
        }
        [HttpPost("{code}/join")]
        public async Task<IActionResult> Join(string code, [FromBody] JoinWatchPartyRequest request)
        {
            var room = await _context.WatchPartyRooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Code == code); if (room == null) return NotFound(new { message = "Room not found." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (!room.Members.Any(m => m.UserId == request.UserId)) room.Members.Add(new WatchPartyMember { UserId = request.UserId });
            await _context.SaveChangesAsync(); return Ok(new { message = "Joined watch party.", room.Code });
        }
        [HttpPost("{code}/sync")]
        public async Task<IActionResult> SyncPlayback(string code, [FromBody] WatchPartySyncRequest request)
        {
            var room = await _context.WatchPartyRooms.Include(r => r.Members).FirstOrDefaultAsync(r => r.Code == code);
            if (room == null) return NotFound(new { message = "Room not found." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (!room.Members.Any(m => m.UserId == request.UserId)) return Forbid();

            var payload = new
            {
                room.Code,
                room.MatchId,
                request.UserId,
                currentTime = Math.Max(0, request.CurrentTime),
                request.IsPlaying,
                playbackRate = request.PlaybackRate <= 0 ? 1 : request.PlaybackRate,
                request.Source,
                serverTime = DateTime.UtcNow
            };
            await _hubContext.Clients.Group(MatchHub.WatchPartyRoom(code)).SendAsync("ReceiveWatchPartySync", payload);
            return Ok(payload);
        }

        [HttpPost("{code}/messages")]
        public async Task<IActionResult> AddMessage(string code, [FromBody] WatchPartyMessageRequest request)
        {
            var room = await _context.WatchPartyRooms.FirstOrDefaultAsync(r => r.Code == code); if (room == null) return NotFound(new { message = "Room not found." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            var msg = new WatchPartyMessage { WatchPartyRoomId = room.Id, UserId = request.UserId, Message = request.Message ?? string.Empty, Reaction = request.Reaction, Prediction = request.Prediction };
            _context.WatchPartyMessages.Add(msg); await _context.SaveChangesAsync();
            var payload = new { msg.Id, msg.UserId, msg.Message, msg.Reaction, msg.Prediction, msg.CreatedAt };
            await _hubContext.Clients.Group(MatchHub.WatchPartyRoom(code)).SendAsync("ReceiveWatchPartyMessage", payload);
            return Ok(payload);
        }
        [AllowAnonymous]
        [HttpGet("{code}/messages")]
        public async Task<IActionResult> Messages(string code) => Ok(await _context.WatchPartyMessages.Where(m => m.WatchPartyRoom.Code == code).OrderBy(m => m.CreatedAt).Select(m => new { m.UserId, m.Message, m.Reaction, m.Prediction, m.CreatedAt }).ToListAsync());
    }
}
