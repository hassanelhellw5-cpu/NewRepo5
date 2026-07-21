using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QemmaProject.Data;
using QemmaProject.Models.Engagement;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [ApiController]
    [Route("api/admin/cosmetics")]
    [Authorize(Roles = "Admin")]
    public class AdminCosmeticsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public AdminCosmeticsController(AppDbContext context) => _context = context;
        [HttpPost("limited-events")]
        public async Task<IActionResult> CreateLimited([FromBody] LimitedCosmeticRequest request)
        { var item = new CosmeticItem { Name = request.Name, Slug = request.Slug, Type = request.Type, Category = "LimitedEvent", Description = request.Description, AssetUrl = request.AssetUrl, ThemePalette = request.ThemePalette, MatchId = request.MatchId, IsLimited = true, AvailableFrom = request.AvailableFrom, AvailableUntil = request.AvailableUntil, PriceCoins = request.PriceCoins, IsActive = true, IsFeatured = true, UnlockType = request.PriceCoins > 0 ? CosmeticUnlockType.Coins : CosmeticUnlockType.Free }; _context.CosmeticItems.Add(item); await _context.SaveChangesAsync(); return Ok(item); }
    }
}
