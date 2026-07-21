using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Engagement;
using QemmaProject.Models.Payment;
using QemmaProject.Models.Sports;
using QemmaProject.Services;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CoinWalletService _wallet;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(AppDbContext context, CoinWalletService wallet, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _wallet = wallet;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("scraping/status")]
        public async Task<IActionResult> GetScrapingStatus([FromQuery] DateTime? date = null)
        {
            var targetDate = (date ?? DateTime.UtcNow.AddHours(3)).Date;
            var matchDate = DateOnly.FromDateTime(targetDate);
            var matchesQuery = _context.Matches.Where(m => m.MatchDate == matchDate);
            var finishedMatchesQuery = matchesQuery.Where(m => m.Status.Contains("انته") || m.Status.Contains("Finished") || m.Status.Contains("FT"));

            var totalTeams = await _context.Teams.CountAsync();
            var teamsWithLogo = await _context.Teams.CountAsync(t => t.LogoUrl != null && t.LogoUrl != "");
            var totalTournaments = await _context.Tournaments.CountAsync();
            var tournamentsWithLogo = await _context.Tournaments.CountAsync(t => t.LogoUrl != null && t.LogoUrl != "");
            var matches = await matchesQuery.CountAsync();
            var finishedMatches = await finishedMatchesQuery.CountAsync();
            var matchesWithVideos = await finishedMatchesQuery.CountAsync(m => m.Videos.Any(v => v.IsAvailable));
            var matchesWithStreams = await matchesQuery.CountAsync(m => m.Streams.Any());

            return Ok(new
            {
                date = targetDate,
                generatedAt = DateTime.UtcNow,
                matches = new
                {
                    total = matches,
                    finished = finishedMatches,
                    withStreams = matchesWithStreams,
                    withVideos = matchesWithVideos,
                    videoCoveragePercent = Percent(matchesWithVideos, finishedMatches)
                },
                logos = new
                {
                    teams = new { total = totalTeams, withLogo = teamsWithLogo, missing = totalTeams - teamsWithLogo, coveragePercent = Percent(teamsWithLogo, totalTeams) },
                    tournaments = new { total = totalTournaments, withLogo = tournamentsWithLogo, missing = totalTournaments - tournamentsWithLogo, coveragePercent = Percent(tournamentsWithLogo, totalTournaments) }
                },
                latestSyncLogs = await _context.DateSyncLogs.OrderByDescending(l => l.Date).Take(10).ToListAsync(),
                latestMatches = await _context.Matches
                    .Include(m => m.HomeTeam)
                    .Include(m => m.AwayTeam)
                    .OrderByDescending(m => m.Id)
                    .Take(10)
                    .Select(m => new { m.Id, m.MatchId, homeTeam = m.HomeTeam.Name, awayTeam = m.AwayTeam.Name, m.Status, m.MatchDate, m.Time })
                    .ToListAsync()
            });
        }

        [HttpGet("scraping/video-coverage")]
        public async Task<IActionResult> GetVideoCoverage([FromQuery] DateTime? date = null)
        {
            var targetDate = (date ?? DateTime.UtcNow.AddHours(3)).Date;
            var matchDate = DateOnly.FromDateTime(targetDate);
            var finishedMatches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Videos)
                .Where(m => m.MatchDate == matchDate && (m.Status.Contains("انته") || m.Status.Contains("Finished") || m.Status.Contains("FT")))
                .ToListAsync();

            var withSummary = finishedMatches.Count(m => m.Videos.Any(v => v.IsAvailable && v.Type == "Summary"));
            var withGoals = finishedMatches.Count(m => m.Videos.Any(v => v.IsAvailable && (v.Type == "Goals" || v.Type == "SingleGoal" || v.Type == "Penalties")));
            var withAny = finishedMatches.Count(m => m.Videos.Any(v => v.IsAvailable));

            return Ok(new
            {
                date = targetDate,
                finishedMatches = finishedMatches.Count,
                matchesWithAnyVideo = withAny,
                matchesWithSummary = withSummary,
                matchesWithGoals = withGoals,
                coveragePercent = Percent(withAny, finishedMatches.Count),
                missing = finishedMatches
                    .Where(m => !m.Videos.Any(v => v.IsAvailable))
                    .Select(m => new { m.Id, m.MatchId, homeTeam = m.HomeTeam?.Name, awayTeam = m.AwayTeam?.Name, m.Status })
                    .ToList()
            });
        }

        [HttpGet("scraping/logo-coverage")]
        public async Task<IActionResult> GetLogoCoverage()
        {
            var teams = await _context.Teams.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.LogoUrl }).ToListAsync();
            var tournaments = await _context.Tournaments.OrderBy(t => t.Name).Select(t => new { t.Id, t.Name, t.LogoUrl }).ToListAsync();
            var teamsWithLogo = teams.Count(t => !string.IsNullOrWhiteSpace(t.LogoUrl));
            var tournamentsWithLogo = tournaments.Count(t => !string.IsNullOrWhiteSpace(t.LogoUrl));

            return Ok(new
            {
                teams = new
                {
                    total = teams.Count,
                    withLogo = teamsWithLogo,
                    missing = teams.Count - teamsWithLogo,
                    coveragePercent = Percent(teamsWithLogo, teams.Count),
                    missingItems = teams.Where(t => string.IsNullOrWhiteSpace(t.LogoUrl)).Take(200)
                },
                tournaments = new
                {
                    total = tournaments.Count,
                    withLogo = tournamentsWithLogo,
                    missing = tournaments.Count - tournamentsWithLogo,
                    coveragePercent = Percent(tournamentsWithLogo, tournaments.Count),
                    missingItems = tournaments.Where(t => string.IsNullOrWhiteSpace(t.LogoUrl)).Take(200)
                }
            });
        }

        [HttpGet("payments/stats")]
        public async Task<IActionResult> GetPaymentStats([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var query = ApplyDateRange(_context.ManualPaymentRequests.AsQueryable(), from, to);
            var requests = await query.ToListAsync();
            return Ok(new
            {
                total = requests.Count,
                pending = requests.Count(r => r.Status == PaymentStatus.Pending),
                approved = requests.Count(r => r.Status == PaymentStatus.Approved),
                rejected = requests.Count(r => r.Status == PaymentStatus.Rejected),
                requestedCoins = requests.Sum(r => r.RequestedCoins),
                approvedCoins = requests.Where(r => r.Status == PaymentStatus.Approved).Sum(r => r.RequestedCoins),
                amountPaid = requests.Sum(r => r.AmountPaid),
                approvedAmountPaid = requests.Where(r => r.Status == PaymentStatus.Approved).Sum(r => r.AmountPaid)
            });
        }

        [HttpGet("payments/manual")]
        public async Task<IActionResult> GetManualPayments([FromQuery] PaymentStatus? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var query = _context.ManualPaymentRequests.Include(r => r.User).AsQueryable();
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new
                {
                    r.Id,
                    r.UserId,
                    userName = r.User.UserName,
                    r.AmountPaid,
                    r.RequestedCoins,
                    r.PaymentMethod,
                    r.PhoneNumberUsed,
                    r.ReceiptImageUrl,
                    r.Status,
                    r.CreatedAt,
                    r.ProcessedAt,
                    r.AdminNotes
                })
                .ToListAsync();
            return Ok(new { page, pageSize, total, items });
        }

        [HttpPost("payments/manual/{requestId:int}/approve")]
        public async Task<IActionResult> ApproveManualPayment(int requestId, [FromBody] AdminPaymentDecisionRequest request)
        {
            var paymentRequest = await _context.ManualPaymentRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId);
            if (paymentRequest == null) return NotFound(new { message = "Payment request not found." });
            if (paymentRequest.Status != PaymentStatus.Pending) return BadRequest(new { message = "Payment request was already processed." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            paymentRequest.Status = PaymentStatus.Approved;
            paymentRequest.ProcessedAt = DateTime.UtcNow;
            paymentRequest.AdminNotes = request.AdminNotes ?? string.Empty;
            await _wallet.CreditAsync(paymentRequest.User, paymentRequest.RequestedCoins, CoinTransactionTypes.ManualDeposit,
                $"Admin approved manual payment request #{paymentRequest.Id} via {paymentRequest.PaymentMethod}");
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            return Ok(new { message = "Payment approved and coins credited.", paymentRequest.UserId, paymentRequest.User.QemmaCoinsBalance });
        }

        [HttpPost("payments/manual/{requestId:int}/reject")]
        public async Task<IActionResult> RejectManualPayment(int requestId, [FromBody] AdminPaymentDecisionRequest request)
        {
            var paymentRequest = await _context.ManualPaymentRequests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (paymentRequest == null) return NotFound(new { message = "Payment request not found." });
            if (paymentRequest.Status != PaymentStatus.Pending) return BadRequest(new { message = "Payment request was already processed." });
            paymentRequest.Status = PaymentStatus.Rejected;
            paymentRequest.ProcessedAt = DateTime.UtcNow;
            paymentRequest.AdminNotes = request.AdminNotes ?? string.Empty;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Payment rejected." });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 200);
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => (u.UserName != null && u.UserName.Contains(term)) || (u.Email != null && u.Email.Contains(term)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
            }
            var total = await query.CountAsync();
            var users = await query.OrderBy(u => u.UserName).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var items = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new { user.Id, user.UserName, user.Email, user.PhoneNumber, roles, user.QemmaCoinsBalance, user.IsEliteSubscriber, user.EliteSubscriptionEndDate, user.CustomThemePalette, user.ActiveBadgeUrl });
            }
            return Ok(new { page, pageSize, total, items });
        }

        [HttpPost("users/{userId}/coins/adjust")]
        public async Task<IActionResult> AdjustUserCoins(string userId, [FromBody] AdjustUserCoinsRequest request)
        {
            if (request.Amount == 0) return BadRequest(new { message = "Amount cannot be zero." });
            var user = await _wallet.GetUserAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });
            try
            {
                if (request.Amount > 0)
                {
                    await _wallet.CreditAsync(user, request.Amount, "AdminAdjustment", request.Notes ?? "Admin coin adjustment");
                }
                else
                {
                    await _wallet.DebitAsync(user, Math.Abs(request.Amount), "AdminAdjustment", request.Notes ?? "Admin coin adjustment");
                }
                await _context.SaveChangesAsync();
                return Ok(new { user.Id, user.QemmaCoinsBalance });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("users/{userId}/roles")]
        public async Task<IActionResult> UpdateUserRoles(string userId, [FromBody] UpdateUserRolesRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });
            var requestedRoles = (request.Roles ?? new List<string>())
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var role in requestedRoles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded) return BadRequest(new { message = "Failed removing current roles.", errors = removeResult.Errors.Select(e => e.Description) });
            var addResult = await _userManager.AddToRolesAsync(user, requestedRoles);
            if (!addResult.Succeeded) return BadRequest(new { message = "Failed assigning roles.", errors = addResult.Errors.Select(e => e.Description) });

            return Ok(new { user.Id, roles = await _userManager.GetRolesAsync(user) });
        }

        [HttpGet("cosmetics")]
        public async Task<IActionResult> GetCosmetics([FromQuery] bool includeInactive = true)
        {
            var query = _context.CosmeticItems.AsQueryable();
            if (!includeInactive) query = query.Where(c => c.IsActive);
            return Ok(await query.OrderByDescending(c => c.IsFeatured).ThenBy(c => c.Name).ToListAsync());
        }

        [HttpPost("cosmetics")]
        public async Task<IActionResult> CreateCosmetic([FromBody] UpsertCosmeticRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Name is required." });
            var slug = string.IsNullOrWhiteSpace(request.Slug) ? MakeSlug(request.Name) : request.Slug.Trim();
            if (await _context.CosmeticItems.AnyAsync(c => c.Slug == slug)) return Conflict(new { message = "Slug already exists." });
            var cosmetic = new CosmeticItem { Slug = slug };
            ApplyCosmeticRequest(cosmetic, request);
            _context.CosmeticItems.Add(cosmetic);
            await _context.SaveChangesAsync();
            return Ok(cosmetic);
        }

        [HttpPut("cosmetics/{cosmeticId:int}")]
        public async Task<IActionResult> UpdateCosmetic(int cosmeticId, [FromBody] UpsertCosmeticRequest request)
        {
            var cosmetic = await _context.CosmeticItems.FirstOrDefaultAsync(c => c.Id == cosmeticId);
            if (cosmetic == null) return NotFound(new { message = "Cosmetic item not found." });
            if (!string.IsNullOrWhiteSpace(request.Slug) && request.Slug != cosmetic.Slug && await _context.CosmeticItems.AnyAsync(c => c.Slug == request.Slug && c.Id != cosmeticId))
                return Conflict(new { message = "Slug already exists." });
            ApplyCosmeticRequest(cosmetic, request);
            await _context.SaveChangesAsync();
            return Ok(cosmetic);
        }

        [HttpGet("custom-tournaments/requests")]
        public async Task<IActionResult> GetCustomTournamentRequests([FromQuery] string? status = null)
        {
            var query = _context.CustomTournamentRequests.Include(r => r.OrganizerUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status.Trim());
            return Ok(await query.OrderByDescending(r => r.CreatedAt).Take(200)
                .Select(r => new { r.Id, r.Name, r.CommunityName, r.ContactInfo, r.RequestedFeatures, r.EstimatedBudgetCoins, r.Status, r.CreatedAt, r.OrganizerUserId, organizerName = r.OrganizerUser.UserName })
                .ToListAsync());
        }

        [HttpPost("custom-tournaments/requests/{requestId:int}/status")]
        public async Task<IActionResult> UpdateCustomTournamentRequestStatus(int requestId, [FromBody] UpdateCustomTournamentStatusRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Status)) return BadRequest(new { message = "Status is required." });
            var item = await _context.CustomTournamentRequests.FirstOrDefaultAsync(r => r.Id == requestId);
            if (item == null) return NotFound(new { message = "Custom tournament request not found." });
            item.Status = request.Status.Trim();
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        private static IQueryable<ManualPaymentRequest> ApplyDateRange(IQueryable<ManualPaymentRequest> query, DateTime? from, DateTime? to)
        {
            if (from.HasValue) query = query.Where(r => r.CreatedAt >= from.Value);
            if (to.HasValue) query = query.Where(r => r.CreatedAt <= to.Value);
            return query;
        }

        private static decimal Percent(int value, int total) => total <= 0 ? 0 : Math.Round((decimal)value * 100m / total, 2);

        private static void ApplyCosmeticRequest(CosmeticItem cosmetic, UpsertCosmeticRequest request)
        {
            cosmetic.Type = request.Type;
            cosmetic.UnlockType = request.UnlockType;
            cosmetic.Name = request.Name.Trim();
            if (!string.IsNullOrWhiteSpace(request.Slug)) cosmetic.Slug = request.Slug.Trim();
            cosmetic.TeamName = request.TeamName?.Trim();
            cosmetic.PlayerName = request.PlayerName?.Trim();
            cosmetic.Category = request.Category?.Trim();
            cosmetic.Description = request.Description?.Trim();
            cosmetic.AssetUrl = request.AssetUrl?.Trim();
            cosmetic.ThemePalette = request.ThemePalette?.Trim();
            cosmetic.MatchId = request.MatchId;
            cosmetic.IsLimited = request.IsLimited;
            cosmetic.AvailableFrom = request.AvailableFrom;
            cosmetic.AvailableUntil = request.AvailableUntil;
            cosmetic.PriceCoins = Math.Max(0, request.PriceCoins);
            cosmetic.PriceMoney = Math.Max(0, request.PriceMoney);
            cosmetic.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EGP" : request.Currency.Trim();
            cosmetic.IsActive = request.IsActive;
            cosmetic.IsFeatured = request.IsFeatured;
        }

        private static string MakeSlug(string value)
        {
            var raw = value.Trim().ToLowerInvariant();
            var chars = raw.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
