using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Hubs;
using QemmaProject.Models.Sports;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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


        [HttpGet("sports")]
        public async Task<IActionResult> GetSports()
        {
            var configuredSports = new[]
            {
                new { sportKey = "football", name = "Football" },
                new { sportKey = "basketball", name = "Basketball" },
                new { sportKey = "tennis", name = "Tennis" },
                new { sportKey = "formula1", name = "Formula 1" },
                new { sportKey = "handball", name = "Handball" },
                new { sportKey = "volleyball", name = "Volleyball" }
            };

            var eventCounts = await _context.OtherSportEvents
                .AsNoTracking()
                .GroupBy(e => e.SportKey)
                .Select(g => new { sportKey = g.Key, eventsCount = g.Count(), nextEventDate = g.Min(e => (DateTime?)e.EventDate) })
                .ToListAsync();

            var countMap = eventCounts.ToDictionary(e => e.sportKey, StringComparer.OrdinalIgnoreCase);
            var fromDbOnly = eventCounts
                .Where(e => !configuredSports.Any(s => s.sportKey.Equals(e.sportKey, StringComparison.OrdinalIgnoreCase)))
                .Select(e => new { sportKey = e.sportKey, name = ToDisplaySportName(e.sportKey) });

            var sports = configuredSports.Concat(fromDbOnly)
                .OrderBy(s => s.name)
                .Select(s =>
                {
                    countMap.TryGetValue(s.sportKey, out var stats);
                    return new
                    {
                        s.sportKey,
                        s.name,
                        eventsCount = stats?.eventsCount ?? 0,
                        nextEventDate = stats?.nextEventDate
                    };
                });

            return Ok(sports);
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



        [HttpGet("events/live")]
        public async Task<IActionResult> GetLiveEvents([FromQuery] string? sportKey = null)
        {
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var query = _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.Status.Contains("Live") || e.Status.Contains("مباشر") || e.Status.Contains("جارية"));

            if (!string.IsNullOrWhiteSpace(normalizedSportKey))
            {
                query = query.Where(e => e.SportKey == normalizedSportKey);
            }

            var events = await query.OrderBy(e => e.EventDate).ToListAsync();
            return Ok(events.Select(e => ToEventResponse(e)));
        }

        [HttpGet("results")]
        public async Task<IActionResult> GetResults([FromQuery] string? sportKey = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var fromDate = (from ?? DateTime.UtcNow.Date.AddDays(-7)).Date;
            var toDate = (to ?? DateTime.UtcNow.Date).Date.AddDays(1);
            var query = _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.EventDate >= fromDate && e.EventDate < toDate)
                .Where(e => e.Status.Contains("Completed") || e.Status.Contains("انتهت") || e.Results.Any());

            if (!string.IsNullOrWhiteSpace(normalizedSportKey))
            {
                query = query.Where(e => e.SportKey == normalizedSportKey);
            }

            var events = await query.OrderByDescending(e => e.EventDate).Take(200).ToListAsync();
            return Ok(events.Select(e => ToEventResponse(e)));
        }

        [HttpGet("formula1/dashboard")]
        public async Task<IActionResult> GetFormula1Dashboard([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var fromDate = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
            var toDate = (to ?? DateTime.UtcNow.Date.AddDays(365)).Date.AddDays(1);
            var events = await _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.SportKey == "formula1" && e.EventDate >= fromDate && e.EventDate < toDate)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            var latestMetadata = events.LastOrDefault(e => !string.IsNullOrWhiteSpace(e.MetadataJson) && e.MetadataJson != "{}")?.MetadataJson ?? "{}";
            return Ok(new
            {
                sportKey = "formula1",
                events = events.Select(e => ToEventResponse(e)),
                live = events.Where(IsLiveStatus).Select(e => ToEventResponse(e)),
                recentResults = events.Where(e => e.Results.Any() || IsCompletedStatus(e)).OrderByDescending(e => e.EventDate).Take(10).Select(e => ToEventResponse(e)),
                standingsMetadataJson = latestMetadata
            });
        }



        [HttpGet("formula1/news")]
        public async Task<IActionResult> GetFormula1News()
        {
            var metadata = await GetLatestFormula1MetadataJson();
            return Ok(ReadMetadataArray(metadata, "officialNews"));
        }

        [HttpGet("formula1/drivers")]
        public async Task<IActionResult> GetFormula1Drivers()
        {
            var metadata = await GetLatestFormula1MetadataJson();
            return Ok(ReadMetadataArray(metadata, "officialDrivers"));
        }

        [HttpGet("formula1/teams")]
        public async Task<IActionResult> GetFormula1Teams()
        {
            var metadata = await GetLatestFormula1MetadataJson();
            return Ok(ReadMetadataArray(metadata, "officialTeams"));
        }

        [HttpGet("participants")]
        public async Task<IActionResult> GetParticipants([FromQuery] string? sportKey = null, [FromQuery] string? q = null)
        {
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var search = q?.Trim();
            var query = _context.OtherSportParticipants.AsNoTracking().Include(p => p.OtherSportEvent).AsQueryable();
            if (!string.IsNullOrWhiteSpace(normalizedSportKey)) query = query.Where(p => p.OtherSportEvent != null && p.OtherSportEvent.SportKey == normalizedSportKey);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search) || p.TeamName.Contains(search));

            var participants = await query
                .GroupBy(p => new { p.Name, p.TeamName, p.Country, p.Role })
                .Select(g => new { g.Key.Name, g.Key.TeamName, g.Key.Country, g.Key.Role, eventsCount = g.Count(), latestMetadataJson = g.Max(p => p.MetadataJson) })
                .OrderBy(p => p.Name)
                .Take(200)
                .ToListAsync();
            return Ok(participants);
        }

        [HttpGet("participants/{name}")]
        public async Task<IActionResult> GetParticipantProfile(string name, [FromQuery] string? sportKey = null)
        {
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var decodedName = Uri.UnescapeDataString(name).Trim();
            var eventsQuery = _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.Participants.Any(p => p.Name == decodedName || p.TeamName == decodedName));
            if (!string.IsNullOrWhiteSpace(normalizedSportKey)) eventsQuery = eventsQuery.Where(e => e.SportKey == normalizedSportKey);
            var events = await eventsQuery.OrderByDescending(e => e.EventDate).Take(50).ToListAsync();
            if (!events.Any()) return NotFound(new { message = "Participant profile not found." });
            var participantRows = events.SelectMany(e => e.Participants).Where(p => p.Name == decodedName || p.TeamName == decodedName).ToList();
            return Ok(new { name = decodedName, profile = participantRows.FirstOrDefault(), events = events.Select(e => ToEventResponse(e)), results = events.SelectMany(e => e.Results.Where(r => r.ParticipantName == decodedName)) });
        }



        [HttpGet("profiles")]
        public async Task<IActionResult> GetOtherSportProfiles([FromQuery] string? sportKey = null, [FromQuery] string? q = null, [FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 200);
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var search = q?.Trim();
            var query = _context.OtherSportProfiles.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(normalizedSportKey)) query = query.Where(p => p.SportKey == normalizedSportKey);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(p => p.Name.Contains(search) || p.TeamName.Contains(search));
            return Ok(await query.OrderBy(p => p.SportKey).ThenBy(p => p.Name).Take(take).ToListAsync());
        }

        [HttpGet("profiles/{profileId:int}")]
        public async Task<IActionResult> GetOtherSportProfile(int profileId)
        {
            var profile = await _context.OtherSportProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == profileId);
            if (profile == null) return NotFound(new { message = "Other sport profile not found." });
            var events = await _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.SportKey == profile.SportKey && e.Participants.Any(p => p.Name == profile.Name || p.TeamName == profile.TeamName))
                .OrderByDescending(e => e.EventDate)
                .Take(50)
                .ToListAsync();
            return Ok(new { profile, events = events.Select(e => ToEventResponse(e)), results = events.SelectMany(e => e.Results.Where(r => r.ParticipantName == profile.Name)) });
        }

        [HttpGet("team-profiles")]
        public async Task<IActionResult> GetOtherSportTeamProfiles([FromQuery] string? sportKey = null, [FromQuery] string? q = null, [FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 200);
            var normalizedSportKey = sportKey?.Trim().ToLowerInvariant();
            var search = q?.Trim();
            var query = _context.OtherSportTeamProfiles.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(normalizedSportKey)) query = query.Where(t => t.SportKey == normalizedSportKey);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(t => t.Name.Contains(search));
            return Ok(await query.OrderBy(t => t.SportKey).ThenBy(t => t.Name).Take(take).ToListAsync());
        }

        [HttpGet("team-profiles/{teamProfileId:int}")]
        public async Task<IActionResult> GetOtherSportTeamProfile(int teamProfileId)
        {
            var team = await _context.OtherSportTeamProfiles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamProfileId);
            if (team == null) return NotFound(new { message = "Other sport team profile not found." });
            var events = await _context.OtherSportEvents
                .AsNoTracking()
                .Include(e => e.Participants)
                .Include(e => e.Results)
                .Include(e => e.Streams)
                .Where(e => e.SportKey == team.SportKey && e.Participants.Any(p => p.TeamName == team.Name || p.Name == team.Name))
                .OrderByDescending(e => e.EventDate)
                .Take(50)
                .ToListAsync();
            return Ok(new { team, events = events.Select(e => ToEventResponse(e)), results = events.SelectMany(e => e.Results.Where(r => r.ParticipantName == team.Name)) });
        }

        [HttpGet("events/{eventId:int}/live-updates")]
        public async Task<IActionResult> GetLiveUpdates(int eventId)
        {
            var exists = await _context.OtherSportEvents.AnyAsync(e => e.Id == eventId);
            if (!exists) return NotFound(new { message = "Other sport event not found." });
            return Ok(await _context.OtherSportLiveUpdates.AsNoTracking().Where(u => u.OtherSportEventId == eventId).OrderBy(u => u.PublishedAt).ThenBy(u => u.Id).ToListAsync());
        }

        [HttpGet("events/{eventId:int}/scoreboard")]
        public async Task<IActionResult> GetScoreboard(int eventId)
        {
            var item = await _context.OtherSportEvents.AsNoTracking().Include(e => e.Participants).Include(e => e.Results).Include(e => e.LiveUpdates).FirstOrDefaultAsync(e => e.Id == eventId);
            if (item == null) return NotFound(new { message = "Other sport event not found." });
            return Ok(new { item.Id, item.SportKey, item.Title, item.Status, item.Time, item.EventDate, participants = item.Participants, results = item.Results, lastUpdate = item.LiveUpdates.OrderByDescending(u => u.PublishedAt).FirstOrDefault(), liveUpdates = item.LiveUpdates.OrderBy(u => u.PublishedAt).ThenBy(u => u.Id) });
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
                var existing = await _context.OtherSportEvents.Include(e => e.Participants).Include(e => e.Results).Include(e => e.Streams).Include(e => e.LiveUpdates).FirstOrDefaultAsync(e => e.SportKey == incoming.SportKey && e.ExternalId == incoming.ExternalId);
                if (existing == null)
                {
                    incoming.UpdatedAt = DateTime.UtcNow;
                    _context.OtherSportEvents.Add(incoming);
                }
                else
                {
                    existing.Title = incoming.Title; existing.CompetitionName = incoming.CompetitionName; existing.EventDate = incoming.EventDate; existing.Status = incoming.Status; existing.Time = incoming.Time; existing.Venue = incoming.Venue; existing.Country = incoming.Country; existing.Source = incoming.Source; existing.SourceUrl = incoming.SourceUrl; existing.MetadataJson = incoming.MetadataJson; existing.UpdatedAt = DateTime.UtcNow;
                    ReplaceChildren(existing, incoming);
                }
            }
            await UpsertOtherSportProfilesAsync(events);
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
            foreach (var update in incoming.LiveUpdates.Where(u => string.IsNullOrWhiteSpace(u.ExternalId) || !existing.LiveUpdates.Any(x => x.ExternalId == u.ExternalId)))
            {
                update.OtherSportEvent = existing;
                existing.LiveUpdates.Add(update);
            }
            existing.Participants = incoming.Participants;
            existing.Results = incoming.Results;
            existing.Streams = incoming.Streams;
        }



        private async Task UpsertOtherSportProfilesAsync(IEnumerable<OtherSportEvent> events)
        {
            var now = DateTime.UtcNow;
            var participantRows = events
                .SelectMany(e => e.Participants.Select(p => new { Event = e, Participant = p }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Participant.Name))
                .ToList();

            foreach (var row in participantRows)
            {
                var sportKey = row.Event.SportKey;
                var name = row.Participant.Name.Trim();
                var externalId = BuildStableExternalId(sportKey, name, row.Participant.Role);
                var profile = await _context.OtherSportProfiles.FirstOrDefaultAsync(p => p.SportKey == sportKey && p.ExternalId == externalId);
                if (profile == null)
                {
                    profile = new OtherSportProfile { SportKey = sportKey, ExternalId = externalId, CreatedAt = now };
                    _context.OtherSportProfiles.Add(profile);
                }

                profile.Name = name;
                profile.TeamName = row.Participant.TeamName ?? string.Empty;
                profile.Country = row.Participant.Country ?? string.Empty;
                profile.Role = row.Participant.Role ?? string.Empty;
                profile.Source = row.Event.Source;
                profile.SourceUrl = row.Event.SourceUrl;
                profile.MetadataJson = row.Participant.MetadataJson ?? "{}";
                profile.UpdatedAt = now;
            }

            foreach (var group in participantRows.GroupBy(x => new { x.Event.SportKey, Name = string.IsNullOrWhiteSpace(x.Participant.TeamName) ? x.Participant.Name.Trim() : x.Participant.TeamName.Trim() }))
            {
                if (string.IsNullOrWhiteSpace(group.Key.Name)) continue;
                var externalId = BuildStableExternalId(group.Key.SportKey, group.Key.Name, "team");
                var team = await _context.OtherSportTeamProfiles.FirstOrDefaultAsync(t => t.SportKey == group.Key.SportKey && t.ExternalId == externalId);
                if (team == null)
                {
                    team = new OtherSportTeamProfile { SportKey = group.Key.SportKey, ExternalId = externalId, CreatedAt = now };
                    _context.OtherSportTeamProfiles.Add(team);
                }

                team.Name = group.Key.Name;
                team.Country = group.Select(x => x.Participant.Country).FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? string.Empty;
                team.EventsCount = group.Select(x => x.Event.ExternalId).Distinct().Count();
                team.Source = group.Select(x => x.Event.Source).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                team.SourceUrl = group.Select(x => x.Event.SourceUrl).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
                team.UpdatedAt = now;
            }

            await UpsertFormula1OfficialProfilesFromMetadataAsync(events, now);
        }

        private async Task UpsertFormula1OfficialProfilesFromMetadataAsync(IEnumerable<OtherSportEvent> events, DateTime now)
        {
            var metadataJson = events.Where(e => e.SportKey == "formula1").Select(e => e.MetadataJson).LastOrDefault(m => !string.IsNullOrWhiteSpace(m) && m != "{}");
            if (string.IsNullOrWhiteSpace(metadataJson)) return;

            var drivers = ReadMetadataArrayElements(metadataJson, "officialDrivers");
            foreach (var item in drivers)
            {
                var title = item.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) continue;
                var url = item.TryGetProperty("url", out var urlProperty) ? urlProperty.GetString() ?? string.Empty : string.Empty;
                var externalId = BuildStableExternalId("formula1", title, "driver");
                var profile = await _context.OtherSportProfiles.FirstOrDefaultAsync(p => p.SportKey == "formula1" && p.ExternalId == externalId);
                if (profile == null)
                {
                    profile = new OtherSportProfile { SportKey = "formula1", ExternalId = externalId, CreatedAt = now };
                    _context.OtherSportProfiles.Add(profile);
                }
                profile.Name = title;
                profile.Role = "driver";
                profile.Source = "Formula1.com";
                profile.SourceUrl = url;
                profile.MetadataJson = item.GetRawText();
                profile.UpdatedAt = now;
            }

            var teams = ReadMetadataArrayElements(metadataJson, "officialTeams");
            foreach (var item in teams)
            {
                var title = item.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) continue;
                var url = item.TryGetProperty("url", out var urlProperty) ? urlProperty.GetString() ?? string.Empty : string.Empty;
                var externalId = BuildStableExternalId("formula1", title, "team");
                var team = await _context.OtherSportTeamProfiles.FirstOrDefaultAsync(t => t.SportKey == "formula1" && t.ExternalId == externalId);
                if (team == null)
                {
                    team = new OtherSportTeamProfile { SportKey = "formula1", ExternalId = externalId, CreatedAt = now };
                    _context.OtherSportTeamProfiles.Add(team);
                }
                team.Name = title;
                team.Source = "Formula1.com";
                team.SourceUrl = url;
                team.MetadataJson = item.GetRawText();
                team.UpdatedAt = now;
            }
        }

        private static IReadOnlyList<JsonElement> ReadMetadataArrayElements(string metadataJson, string propertyName)
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<JsonElement>();
            }

            return property.EnumerateArray().Select(element => element.Clone()).ToList();
        }

        private static string BuildStableExternalId(string sportKey, string name, string role)
        {
            var raw = $"{sportKey}:{role}:{name}".ToLowerInvariant();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant()[..24];
        }

        private async Task<string> GetLatestFormula1MetadataJson()
        {
            return await _context.OtherSportEvents
                .AsNoTracking()
                .Where(e => e.SportKey == "formula1" && !string.IsNullOrWhiteSpace(e.MetadataJson) && e.MetadataJson != "{}")
                .OrderByDescending(e => e.UpdatedAt)
                .Select(e => e.MetadataJson)
                .FirstOrDefaultAsync() ?? "{}";
        }

        private static object ReadMetadataArray(string metadataJson, string propertyName)
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<object>(property.GetRawText()) ?? Array.Empty<object>();
            }

            return Array.Empty<object>();
        }

        private static bool IsLiveStatus(OtherSportEvent e) => e.Status.Contains("Live", StringComparison.OrdinalIgnoreCase) || e.Status.Contains("مباشر") || e.Status.Contains("جارية");

        private static bool IsCompletedStatus(OtherSportEvent e) => e.Status.Contains("Completed", StringComparison.OrdinalIgnoreCase) || e.Status.Contains("انتهت");

        private static string ToDisplaySportName(string sportKey) => string.IsNullOrWhiteSpace(sportKey) ? "Other" : string.Join(" ", sportKey.Replace("-", " ").Replace("_", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

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
            e.MetadataJson,
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
