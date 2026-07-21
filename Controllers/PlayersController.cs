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
            var stats = await _context.PlayerMatchStats.Where(s => s.PlayerId == playerId).ToListAsync();
            return Ok(new { player.Id, player.Name, team = player.Team?.Name ?? player.TeamName, player.ImageUrl, rating = stats.Count == 0 ? 6 : Math.Round(stats.Average(s => (double)s.Rating), 1), goals = stats.Sum(s => s.Goals), assists = stats.Sum(s => s.Assists), fantasyPoints = stats.Sum(s => s.FantasyPoints), shareImage = $"/api/players/{player.Id}/card/share-image" });
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
    }
}
