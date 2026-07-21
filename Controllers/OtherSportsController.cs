using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Hubs;
using QemmaProject.Models.Sports;
using System.Security.Cryptography;
using System.Text;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/other-sports")]
    public class OtherSportsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<MatchHub> _hubContext;
        private readonly IConfiguration _configuration;

        public OtherSportsController(AppDbContext context, IHubContext<MatchHub> hubContext, IConfiguration configuration)
        {
            _context = context;
            _hubContext = hubContext;
            _configuration = configuration;
        }

        [HttpGet("events")]
        public async Task<IActionResult> GetEvents([FromQuery] string? sportKey = null, [FromQuery] DateTime? date = null, [FromQuery] bool liveOnly = false)
        {
            var dayStart = (date ?? DateTime.UtcNow).Date;
            var dayEnd = dayStart.AddDays(1);
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();

            try
            {
                var query = _context.OtherSportEvents
                    .AsNoTracking()
                    .Include(e => e.Participants)
                    .Include(e => e.Results)
                    .Include(e => e.Streams)
                    .Where(e => e.EventDate >= dayStart && e.EventDate < dayEnd);

                if (!string.IsNullOrWhiteSpace(normalizedSportKey))
                {
                    query = query.Where(e => e.SportKey == normalizedSportKey);
                }

                if (liveOnly)
                {
                    query = query.Where(e => e.Status.Contains("Live") || e.Status.Contains("مباشر") || e.Status.Contains("جارية"));
                }

                var events = await query.OrderBy(e => e.EventDate).ToListAsync();
                return Ok(events.Select(e => ToEventResponse(e)));
            }
            catch (SqlException ex) when (ex.Number == 18456)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Database login failed. Set a valid ConnectionStrings:DefaultConnection value via environment variables or user secrets, then restart the API.",
                    detail = "SQL Server rejected the configured database user.",
                    sportKey = normalizedSportKey,
                    date = dayStart.ToString("yyyy-MM-dd")
                });
            }
        }

        [HttpGet("events/{eventId:int}")]
        public async Task<IActionResult> GetEvent(int eventId)
        {
            var item = await _context.OtherSportEvents.Include(e => e.Participants).Include(e => e.Results).Include(e => e.LiveUpdates).Include(e => e.Streams).FirstOrDefaultAsync(e => e.Id == eventId);
            if (item == null) return NotFound(new { message = "Other sport event not found." });
            return Ok(ToEventResponse(item, includeLiveUpdates: true));
        }

        [HttpPost("import/events")]
        public async Task<IActionResult> ImportEvents([FromBody] List<OtherSportEvent> events)
        {
            if (!IsImportAuthorized()) return Unauthorized(new { message = "Invalid or missing import key." });
            if (events == null || events.Count == 0) return BadRequest(new { message = "No events provided." });
            foreach (var incoming in events)
            {
                incoming.SportKey = (incoming.SportKey ?? string.Empty).Trim().ToLower();
                var existing = await _context.OtherSportEvents.Include(e => e.Participants).Include(e => e.Results).Include(e => e.Streams).FirstOrDefaultAsync(e => e.SportKey == incoming.SportKey && e.ExternalId == incoming.ExternalId);
                if (existing == null)
                {
                    incoming.UpdatedAt = DateTime.UtcNow;
                    _context.OtherSportEvents.Add(incoming);
                }
                else
                {
                    existing.Title = incoming.Title; existing.CompetitionName = incoming.CompetitionName; existing.EventDate = incoming.EventDate; existing.Status = incoming.Status; existing.Time = incoming.Time; existing.Venue = incoming.Venue; existing.Country = incoming.Country; existing.Source = incoming.Source; existing.SourceUrl = incoming.SourceUrl; existing.UpdatedAt = DateTime.UtcNow;
                    ReplaceChildren(existing, incoming);
                }
            }
            await _context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("ReceiveOtherSportUpdate", new { status = "Other sports updated", count = events.Count, time = DateTime.UtcNow });
            return Ok(new { message = "Other sport events imported.", count = events.Count });
        }


        [HttpPost("events/{eventId:int}/streams")]
        public async Task<IActionResult> ImportEventStreams(int eventId, [FromBody] List<OtherSportStream> streams)
        {
            if (!IsImportAuthorized()) return Unauthorized(new { message = "Invalid or missing import key." });
            var item = await _context.OtherSportEvents.Include(e => e.Streams).FirstOrDefaultAsync(e => e.Id == eventId);
            if (item == null) return NotFound(new { message = "Other sport event not found." });

            _context.OtherSportStreams.RemoveRange(item.Streams);
            foreach (var stream in streams ?? new List<OtherSportStream>())
            {
                stream.Id = 0;
                stream.OtherSportEventId = eventId;
                stream.OtherSportEvent = item;
                stream.UpdatedAt = DateTime.UtcNow;
                _context.OtherSportStreams.Add(stream);
            }

            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group(MatchHub.OtherSportRoom(eventId)).SendAsync("ReceiveOtherSportUpdate", new { eventId, streams = streams?.Count ?? 0, time = DateTime.UtcNow });
            await _hubContext.Clients.All.SendAsync("ReceiveOtherSportUpdate", new { status = "Other sport streams updated", eventId, streams = streams?.Count ?? 0, time = DateTime.UtcNow });
            return Ok(new { message = "Other sport streams imported.", eventId, count = streams?.Count ?? 0 });
        }

        [HttpPost("events/{eventId:int}/live-updates")]
        public async Task<IActionResult> ImportLiveUpdates(int eventId, [FromBody] List<OtherSportLiveUpdate> updates)
        {
            if (!IsImportAuthorized()) return Unauthorized(new { message = "Invalid or missing import key." });
            var exists = await _context.OtherSportEvents.AnyAsync(e => e.Id == eventId);
            if (!exists) return NotFound(new { message = "Other sport event not found." });
            foreach (var update in updates ?? new List<OtherSportLiveUpdate>())
            {
                update.OtherSportEventId = eventId;
                if (!string.IsNullOrWhiteSpace(update.ExternalId) && await _context.OtherSportLiveUpdates.AnyAsync(u => u.OtherSportEventId == eventId && u.ExternalId == update.ExternalId)) continue;
                _context.OtherSportLiveUpdates.Add(update);
            }
            await _context.SaveChangesAsync();
            await _hubContext.Clients.Group(MatchHub.OtherSportRoom(eventId)).SendAsync("ReceiveOtherSportLiveUpdate", new { eventId, updates });
            return Ok(new { message = "Live updates imported.", count = updates?.Count ?? 0 });
        }

        private bool IsImportAuthorized()
        {
            var importKey = Environment.GetEnvironmentVariable("IMPORT_API_KEY");
            if (string.IsNullOrWhiteSpace(importKey)) return true;

            var provided = Request.Headers["X-Import-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(provided)) return false;

            var expectedBytes = Encoding.UTF8.GetBytes(importKey);
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            return expectedBytes.Length == providedBytes.Length && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }

        private void ReplaceChildren(OtherSportEvent existing, OtherSportEvent incoming)
        {
            _context.OtherSportParticipants.RemoveRange(existing.Participants);
            _context.OtherSportResults.RemoveRange(existing.Results);
            _context.OtherSportStreams.RemoveRange(existing.Streams);
            foreach (var participant in incoming.Participants) participant.OtherSportEvent = existing;
            foreach (var result in incoming.Results) result.OtherSportEvent = existing;
            foreach (var stream in incoming.Streams) stream.OtherSportEvent = existing;
            existing.Participants = incoming.Participants;
            existing.Results = incoming.Results;
            existing.Streams = incoming.Streams;
        }

        private object ToEventResponse(OtherSportEvent e, bool includeLiveUpdates = false) => new
        {
            e.Id,
            e.SportKey,
            e.ExternalId,
            e.Title,
            e.CompetitionName,
            e.EventDate,
            e.Status,
            e.Time,
            e.Venue,
            e.Country,
            e.Source,
            e.SourceUrl,
            participants = e.Participants,
            results = e.Results,
            streams = e.Streams.Select(ToPublicStreamResponse),
            liveUpdates = includeLiveUpdates ? e.LiveUpdates.OrderBy(u => u.PublishedAt).ThenBy(u => u.Id) : null
        };

        private object ToPublicStreamResponse(OtherSportStream stream)
        {
            var exposeRawUrls = _configuration.GetValue("Streams:ExposeRawUrls", false);
            var playbackUrl = IsInternalProxyUrl(stream.M3U8Url) ? stream.M3U8Url : IsInternalProxyUrl(stream.StreamUrl) ? stream.StreamUrl : string.Empty;

            return new
            {
                stream.Id,
                stream.Source,
                playbackUrl,
                stream.StatusMessage,
                stream.UpdatedAt,
                streamUrl = exposeRawUrls ? stream.StreamUrl : string.Empty,
                m3u8Url = exposeRawUrls ? stream.M3U8Url : string.Empty
            };
        }

        private static bool IsInternalProxyUrl(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.StartsWith("/api/SportsData/proxy/hls", StringComparison.OrdinalIgnoreCase);
        }
    }
}
