using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using QemmaProject.Data;
using QemmaProject.Models.Sports;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/matches/{matchId:int}/summaries")]
    public class MatchSummariesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MatchSummariesController(AppDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> Get(int matchId, [FromQuery] string locale = "ar-EG")
        {
            var summary = await _context.MatchSummaries
                .Where(s => s.MatchId == matchId && s.Locale == locale)
                .OrderByDescending(s => s.GeneratedAt)
                .FirstOrDefaultAsync();
            return summary == null ? NotFound(new { message = "Summary not found." }) : Ok(summary);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("generate")]
        public async Task<IActionResult> GenerateHeuristic(int matchId, [FromBody] GenerateSummaryRequest request)
        {
            var match = await _context.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) return NotFound(new { message = "Match not found." });

            var events = await _context.MatchEvents.Where(e => e.MatchId == matchId).OrderBy(e => e.Minute).ToListAsync();
            var goals = events.Where(e => e.Type.Contains("Goal", StringComparison.OrdinalIgnoreCase)).ToList();
            var cards = events.Count(e => e.Type.Contains("Card", StringComparison.OrdinalIgnoreCase));
            var highlights = events.Take(8).Select(e => new { e.Minute, e.Type, e.PlayerName, e.TeamSide, e.Detail }).ToList();
            var text = string.IsNullOrWhiteSpace(request.Summary)
                ? $"{match.HomeTeam.Name} ضد {match.AwayTeam.Name}: النتيجة {match.ScoreHome}-{match.ScoreAway}. شهدت المباراة {goals.Count} أهداف و{cards} كروت. أبرز اللقطات: {string.Join("، ", highlights.Select(h => $"د{h.Minute} {h.Type} {h.PlayerName}"))}."
                : request.Summary.Trim();

            var summary = await _context.MatchSummaries.FirstOrDefaultAsync(s => s.MatchId == matchId && s.Locale == request.Locale && s.Source == request.Source);
            if (summary == null)
            {
                summary = new MatchSummary { MatchId = matchId, Locale = request.Locale, Source = request.Source };
                _context.MatchSummaries.Add(summary);
            }

            summary.Summary = text;
            summary.HighlightsJson = JsonSerializer.Serialize(highlights);
            summary.GeneratedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(summary);
        }
    }
}
