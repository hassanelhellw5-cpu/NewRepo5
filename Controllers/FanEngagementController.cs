using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Hubs;
using QemmaProject.Models.Engagement;
using QemmaProject.Services;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [Route("api/fan-engagement")]
    [ApiController]
    [Authorize]
    public class FanEngagementController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CoinWalletService _wallet;
        private readonly IHubContext<MatchHub> _hubContext;

        public FanEngagementController(AppDbContext context, CoinWalletService wallet, IHubContext<MatchHub> hubContext)
        {
            _context = context;
            _wallet = wallet;
            _hubContext = hubContext;
        }

        [AllowAnonymous]
        [HttpGet("cosmetics")]
        public async Task<IActionResult> GetCosmetics([FromQuery] CosmeticItemType? type = null, [FromQuery] string? category = null, [FromQuery] string? teamName = null, [FromQuery] int? matchId = null, [FromQuery] bool limitedOnly = false)
        {
            var query = _context.CosmeticItems.Where(c => c.IsActive);
            if (type.HasValue) query = query.Where(c => c.Type == type.Value);
            if (!string.IsNullOrWhiteSpace(category)) query = query.Where(c => c.Category == category.Trim());
            if (!string.IsNullOrWhiteSpace(teamName)) query = query.Where(c => c.TeamName == teamName.Trim());
            if (matchId.HasValue) query = query.Where(c => c.MatchId == matchId.Value || c.MatchId == null);
            if (limitedOnly) query = query.Where(c => c.IsLimited);
            var now = DateTime.UtcNow;
            query = query.Where(c => (c.AvailableFrom == null || c.AvailableFrom <= now) && (c.AvailableUntil == null || c.AvailableUntil >= now));

            var items = await query.OrderByDescending(c => c.IsFeatured).ThenBy(c => c.PriceCoins).ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Type,
                    c.UnlockType,
                    c.Name,
                    c.Slug,
                    c.Category,
                    c.Description,
                    c.TeamName,
                    c.PlayerName,
                    c.MatchId,
                    c.AssetUrl,
                    c.ThemePalette,
                    c.IsLimited,
                    c.AvailableFrom,
                    c.AvailableUntil,
                    c.PriceCoins,
                    c.PriceMoney,
                    c.Currency,
                    c.IsFeatured
                })
                .ToListAsync();

            return Ok(items);
        }

        [AllowAnonymous]
        [HttpGet("events/limited-store")]
        public async Task<IActionResult> GetLimitedStore()
        {
            var now = DateTime.UtcNow;
            var items = await _context.CosmeticItems
                .Where(c => c.IsActive && c.IsLimited && (c.AvailableFrom == null || c.AvailableFrom <= now) && (c.AvailableUntil == null || c.AvailableUntil >= now))
                .OrderBy(c => c.AvailableUntil ?? DateTime.MaxValue)
                .Select(c => new { c.Id, c.Type, c.Name, c.Slug, c.Category, c.Description, c.AssetUrl, c.ThemePalette, c.MatchId, c.AvailableFrom, c.AvailableUntil, c.PriceCoins, c.PriceMoney, c.Currency })
                .ToListAsync();
            return Ok(items);
        }

        [HttpPost("supporter/subscribe")]
        public async Task<IActionResult> SubscribeSupporter([FromBody] SubscribeSupporterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var months = Math.Clamp(request.Months <= 0 ? 1 : request.Months, 1, 12);
            var priceCoins = request.PriceCoins <= 0 ? 99m * months : request.PriceCoins;

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            await _wallet.DebitAsync(user, priceCoins, CoinTransactionTypes.BuySupporter,
                $"Supporter subscription for {months} month(s)");

            var currentEnd = user.EliteSubscriptionEndDate > DateTime.UtcNow ? user.EliteSubscriptionEndDate.Value : DateTime.UtcNow;
            user.IsEliteSubscriber = true;
            user.EliteSubscriptionEndDate = currentEnd.AddMonths(months);

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new
            {
                message = "Supporter subscription activated.",
                user.Id,
                user.IsEliteSubscriber,
                user.EliteSubscriptionEndDate,
                user.QemmaCoinsBalance
            });
        }

        [AllowAnonymous]
        [HttpGet("profile/{userId}/premium")]
        public async Task<IActionResult> GetPremiumProfile(string userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var predictions = await _context.Predictions
                .Where(p => p.UserId == userId)
                .Select(p => new { p.IsProcessed, p.PointsEarned })
                .ToListAsync();

            var leagueRows = await _context.PredictionLeagueMembers
                .Where(m => m.UserId == userId)
                .Select(m => new { m.PredictionLeagueId, m.TotalPoints, m.KnockoutRound, m.IsKnockoutEliminated })
                .ToListAsync();

            var ownedCosmetics = await _context.UserCosmeticItems
                .Where(o => o.UserId == userId)
                .Select(o => new { o.CosmeticItem.Name, o.CosmeticItem.Type, o.CosmeticItem.Slug, o.IsEquipped })
                .ToListAsync();

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.IsEliteSubscriber,
                user.EliteSubscriptionEndDate,
                user.CustomThemePalette,
                user.ActiveBadgeUrl,
                predictionStats = new
                {
                    total = predictions.Count,
                    processed = predictions.Count(p => p.IsProcessed),
                    totalPoints = predictions.Sum(p => p.PointsEarned)
                },
                leagues = new
                {
                    total = leagueRows.Count,
                    bestPoints = leagueRows.Count == 0 ? 0 : leagueRows.Max(l => l.TotalPoints),
                    knockoutRuns = leagueRows.Count(l => l.KnockoutRound > 0)
                },
                ownedCosmetics
            });
        }


        [AllowAnonymous]
        [HttpGet("matches/{matchId:int}/experience")]
        public async Task<IActionResult> GetMatchEngagementExperience(int matchId, [FromQuery] string? userId = null)
        {
            if (!string.IsNullOrWhiteSpace(userId) && !this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();

            var match = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) return NotFound(new { message = "Match not found." });

            var now = DateTime.UtcNow;
            var storeItems = await _context.CosmeticItems
                .Where(c => c.IsActive && (c.MatchId == null || c.MatchId == matchId) &&
                    (c.AvailableFrom == null || c.AvailableFrom <= now) &&
                    (c.AvailableUntil == null || c.AvailableUntil >= now) &&
                    (c.Category == "MatchDay" || c.Category == "LimitedEvent" || c.Category == "Supporter" || c.Type == CosmeticItemType.MatchPass || c.IsLimited))
                .OrderByDescending(c => c.IsFeatured)
                .ThenBy(c => c.PriceCoins)
                .ThenBy(c => c.Name)
                .Select(c => new
                {
                    c.Id,
                    c.Type,
                    c.UnlockType,
                    c.Name,
                    c.Slug,
                    c.Category,
                    c.Description,
                    c.AssetUrl,
                    c.ThemePalette,
                    c.MatchId,
                    c.IsLimited,
                    c.AvailableUntil,
                    c.PriceCoins,
                    c.PriceMoney,
                    c.Currency
                })
                .ToListAsync();

            var fanPass = string.IsNullOrWhiteSpace(userId)
                ? null
                : await _context.MatchFanPasses
                    .Where(p => p.MatchId == matchId && p.UserId == userId)
                    .Select(p => new { p.Id, p.Title, p.BadgeText, p.PaidCoins, p.PurchasedAt })
                    .FirstOrDefaultAsync();

            var supporterRows = await BuildMatchSupporterLeaderboardAsync(matchId, 10);

            return Ok(new
            {
                match = new
                {
                    match.Id,
                    match.MatchId,
                    homeTeam = match.HomeTeam?.Name,
                    awayTeam = match.AwayTeam?.Name,
                    match.MatchDate,
                    match.Time,
                    match.Status
                },
                user = string.IsNullOrWhiteSpace(userId) ? null : new { userId, fanPass },
                store = new
                {
                    matchPassDefaultPriceCoins = 25,
                    pinnedCheerMinCoins = 10,
                    premiumBurstMinCoins = 5,
                    items = storeItems
                },
                supporterLeaderboard = supporterRows,
                signalR = new
                {
                    hub = "/matchHub",
                    room = MatchHub.MatchRoom(matchId),
                    joinMethod = "JoinMatchRoom",
                    leaveMethod = "LeaveMatchRoom",
                    events = new[] { "ReceiveMatchChatMessage", "ReceiveMatchReaction", "ReceiveMatchUpdate", "ReceiveMatchScoreUpdate", "ReceiveMatchDetailsUpdate" }
                }
            });
        }

        [AllowAnonymous]
        [HttpGet("matches/{matchId:int}/supporter-leaderboard")]
        public async Task<IActionResult> GetMatchSupporterLeaderboard(int matchId, [FromQuery] int take = 10)
        {
            var matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId);
            if (!matchExists) return NotFound(new { message = "Match not found." });

            return Ok(new
            {
                matchId,
                leaderboard = await BuildMatchSupporterLeaderboardAsync(matchId, Math.Clamp(take, 1, 50))
            });
        }

        [HttpPost("matches/{matchId:int}/fan-pass")]
        public async Task<IActionResult> BuyMatchFanPass(int matchId, [FromBody] BuyMatchFanPassRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();

            var match = await _context.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) return NotFound(new { message = "Match not found." });

            var existing = await _context.MatchFanPasses.FirstOrDefaultAsync(p => p.MatchId == matchId && p.UserId == request.UserId);
            if (existing != null) return Ok(new { message = "Fan pass already owned.", existing.Id, existing.BadgeText });

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var priceCoins = request.PriceCoins <= 0 ? 25m : request.PriceCoins;
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            await _wallet.DebitAsync(user, priceCoins, CoinTransactionTypes.BuyMatchFanPass,
                $"Match fan pass for match #{matchId}");

            var pass = new MatchFanPass
            {
                MatchId = matchId,
                UserId = user.Id,
                Title = string.IsNullOrWhiteSpace(request.Title) ? $"{match.HomeTeam?.Name} vs {match.AwayTeam?.Name}" : request.Title.Trim(),
                BadgeText = string.IsNullOrWhiteSpace(request.BadgeText) ? "حضرت الماتش" : request.BadgeText.Trim(),
                PaidCoins = priceCoins
            };
            _context.MatchFanPasses.Add(pass);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new { message = "Match fan pass unlocked.", pass.Id, pass.Title, pass.BadgeText, user.QemmaCoinsBalance });
        }

        [HttpPost("custom-tournaments/requests")]
        public async Task<IActionResult> CreateCustomTournamentRequest([FromBody] CreateCustomTournamentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OrganizerUserId)) return BadRequest(new { message = "OrganizerUserId is required." });
            if (!this.IsSelfOrAdmin(request.OrganizerUserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Name is required." });

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.OrganizerUserId);
            if (!userExists) return NotFound(new { message = "Organizer user not found." });

            var item = new CustomTournamentRequest
            {
                OrganizerUserId = request.OrganizerUserId,
                Name = request.Name.Trim(),
                CommunityName = request.CommunityName?.Trim() ?? string.Empty,
                ContactInfo = request.ContactInfo?.Trim() ?? string.Empty,
                RequestedFeatures = request.RequestedFeatures?.Trim() ?? string.Empty,
                EstimatedBudgetCoins = Math.Max(0, request.EstimatedBudgetCoins)
            };
            _context.CustomTournamentRequests.Add(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Custom tournament request submitted.", item.Id, item.Status });
        }

        [HttpPost("cosmetics/{cosmeticId:int}/purchase")]
        public async Task<IActionResult> PurchaseCosmetic(int cosmeticId, [FromBody] PurchaseCosmeticRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var cosmetic = await _context.CosmeticItems.FirstOrDefaultAsync(c => c.Id == cosmeticId && c.IsActive);
            if (cosmetic == null) return NotFound(new { message = "Cosmetic item not found." });

            var alreadyOwned = await _context.UserCosmeticItems.AnyAsync(u => u.UserId == user.Id && u.CosmeticItemId == cosmetic.Id);
            if (alreadyOwned) return Ok(new { message = "Cosmetic already owned.", user.QemmaCoinsBalance });
            if (cosmetic.UnlockType == CosmeticUnlockType.Money)
            {
                return BadRequest(new { message = "Money purchases must be approved through the payment flow before unlocking this cosmetic." });
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            if (cosmetic.UnlockType == CosmeticUnlockType.Coins && cosmetic.PriceCoins > 0)
            {
                await _wallet.DebitAsync(user, cosmetic.PriceCoins, CoinTransactionTypes.BuyCosmetic,
                    $"Purchased cosmetic #{cosmetic.Id} ({cosmetic.Name})");
            }
            _context.UserCosmeticItems.Add(new UserCosmeticItem
            {
                UserId = user.Id,
                CosmeticItemId = cosmetic.Id,
                IsEquipped = request.EquipNow
            });

            if (request.EquipNow)
            {
                await UnequipSameTypeAsync(user.Id, cosmetic.Type);
                ApplyCosmeticToUser(user, cosmetic);
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new { message = "Cosmetic unlocked.", user.QemmaCoinsBalance });
        }

        [HttpPost("cosmetics/{cosmeticId:int}/equip")]
        public async Task<IActionResult> EquipCosmetic(int cosmeticId, [FromBody] EquipCosmeticRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();

            var ownership = await _context.UserCosmeticItems
                .Include(o => o.CosmeticItem)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.UserId == request.UserId && o.CosmeticItemId == cosmeticId);
            if (ownership == null) return NotFound(new { message = "User does not own this cosmetic." });

            await UnequipSameTypeAsync(request.UserId, ownership.CosmeticItem.Type);
            ownership.IsEquipped = true;
            ApplyCosmeticToUser(ownership.User, ownership.CosmeticItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cosmetic equipped." });
        }

        [AllowAnonymous]
        [HttpGet("cheers")]
        public async Task<IActionResult> GetCheerPhrases([FromQuery] string? teamName = null)
        {
            var query = _context.CheerPhrases.Where(c => c.IsActive);
            if (!string.IsNullOrWhiteSpace(teamName)) query = query.Where(c => c.TeamName == null || c.TeamName == teamName);

            return Ok(await query.OrderBy(c => c.SortOrder).ThenBy(c => c.Text).ToListAsync());
        }


        [HttpPost("matches/{matchId:int}/reactions")]
        public async Task<IActionResult> SendMatchReaction(int matchId, [FromBody] MatchReactionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Reaction)) return BadRequest(new { message = "Reaction is required." });

            var matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId);
            if (!matchExists) return NotFound(new { message = "Match not found." });
            var user = request.PremiumBurst
                ? await _wallet.GetUserAsync(request.UserId)
                : await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var burstCoins = 0m;
            if (request.PremiumBurst)
            {
                burstCoins = Math.Max(5m, request.BurstCoins);
                await _wallet.DebitAsync(user, burstCoins, CoinTransactionTypes.PremiumReactionBurst,
                    $"Premium reaction burst for match #{matchId}");
                await _context.SaveChangesAsync();
            }

            var payload = new
            {
                matchId,
                request.UserId,
                userName = user.UserName,
                reaction = request.Reaction.Trim(),
                message = request.Message?.Trim(),
                request.MatchEventId,
                isPremiumBurst = request.PremiumBurst,
                burstCoins,
                animation = request.PremiumBurst ? ResolveReactionAnimation(request.Reaction) : null,
                createdAt = DateTime.UtcNow,
                user.ActiveBadgeUrl,
                user.CustomThemePalette
            };
            await _hubContext.Clients.Group(MatchHub.MatchRoom(matchId)).SendAsync("ReceiveMatchReaction", payload);
            return Ok(payload);
        }

        [HttpPost("matches/{matchId:int}/chat")]
        public async Task<IActionResult> SendMatchChatMessage(int matchId, [FromBody] SendChatMessageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Message)) return BadRequest(new { message = "Message is required." });
            if (request.Message.Length > 240) return BadRequest(new { message = "Message cannot exceed 240 characters." });

            var matchExists = await _context.Matches.AnyAsync(m => m.Id == matchId);
            if (!matchExists) return NotFound(new { message = "Match not found." });

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var shouldPin = request.Pin || request.PinMinutes > 0;
            var pinMinutes = shouldPin ? Math.Clamp(request.PinMinutes <= 0 ? 5 : request.PinMinutes, 1, 30) : 0;
            var message = new LiveChatMessage
            {
                MatchId = matchId,
                UserId = user.Id,
                Message = request.Message.Trim(),
                CheerPhraseId = request.CheerPhraseId,
                IsPinned = shouldPin,
                PaidCoins = request.PinCoins,
                PinnedUntil = shouldPin ? DateTime.UtcNow.AddMinutes(pinMinutes) : null
            };

            if (message.IsPinned)
            {
                var minPinCoins = 10m;
                var coinsToCharge = Math.Max(minPinCoins, request.PinCoins);
                await _wallet.DebitAsync(user, coinsToCharge, CoinTransactionTypes.PinLiveComment,
                    $"Pinned live comment for match #{matchId}");
                message.PaidCoins = coinsToCharge;
            }

            _context.LiveChatMessages.Add(message);
            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            var payload = new
            {
                message.Id,
                message.MatchId,
                message.UserId,
                userName = user.UserName,
                message.Message,
                message.CheerPhraseId,
                message.IsPinned,
                message.PaidCoins,
                message.PinnedUntil,
                message.CreatedAt,
                user.ActiveBadgeUrl,
                user.CustomThemePalette
            };
            await _hubContext.Clients.Group(MatchHub.MatchRoom(matchId)).SendAsync("ReceiveMatchChatMessage", payload);
            return Ok(payload);
        }

        [AllowAnonymous]
        [HttpGet("matches/{matchId:int}/chat")]
        public async Task<IActionResult> GetMatchChatMessages(int matchId, [FromQuery] int take = 100)
        {
            take = Math.Clamp(take, 1, 200);
            var messages = await _context.LiveChatMessages
                .Where(m => m.MatchId == matchId)
                .OrderByDescending(m => m.IsPinned && m.PinnedUntil > DateTime.UtcNow)
                .ThenByDescending(m => m.CreatedAt)
                .Take(take)
                .Select(m => new
                {
                    m.Id,
                    m.MatchId,
                    m.UserId,
                    userName = m.User.UserName,
                    m.Message,
                    m.CheerPhraseId,
                    m.IsPinned,
                    m.PaidCoins,
                    m.PinnedUntil,
                    m.CreatedAt,
                    m.User.ActiveBadgeUrl,
                    m.User.CustomThemePalette
                })
                .ToListAsync();

            return Ok(messages.OrderBy(m => m.CreatedAt));
        }

        [AllowAnonymous]
        [HttpGet("matches/{matchId:int}/live-readiness")]
        public async Task<IActionResult> GetLiveReadiness(int matchId)
        {
            var match = await _context.Matches
                .Include(m => m.Streams)
                .FirstOrDefaultAsync(m => m.Id == matchId);
            if (match == null) return NotFound(new { message = "Match not found." });

            var hasStream = match.Streams.Any(s => !string.IsNullOrWhiteSpace(s.M3U8Url) || !string.IsNullOrWhiteSpace(s.StreamUrl));
            var hasChat = true;
            return Ok(new
            {
                match.Id,
                match.MatchId,
                match.Status,
                hasStream,
                hasChat,
                matchRoom = MatchHub.MatchRoom(matchId),
                ready = hasStream && hasChat
            });
        }


        private async Task<List<object>> BuildMatchSupporterLeaderboardAsync(int matchId, int take)
        {
            var chatSpend = await _context.LiveChatMessages
                .Where(m => m.MatchId == matchId && m.PaidCoins > 0)
                .GroupBy(m => m.UserId)
                .Select(g => new { UserId = g.Key, PinnedCoins = g.Sum(m => m.PaidCoins), PinnedMessages = g.Count() })
                .ToListAsync();

            var passSpend = await _context.MatchFanPasses
                .Where(p => p.MatchId == matchId && p.PaidCoins > 0)
                .GroupBy(p => p.UserId)
                .Select(g => new { UserId = g.Key, FanPassCoins = g.Sum(p => p.PaidCoins), FanPasses = g.Count() })
                .ToListAsync();

            var userIds = chatSpend.Select(s => s.UserId).Concat(passSpend.Select(s => s.UserId)).Distinct().ToList();
            if (userIds.Count == 0) return new List<object>();

            var users = await _context.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.UserName, u.ActiveBadgeUrl, u.CustomThemePalette })
                .ToDictionaryAsync(u => u.Id);

            return userIds
                .Select(userId =>
                {
                    var chat = chatSpend.FirstOrDefault(s => s.UserId == userId);
                    var pass = passSpend.FirstOrDefault(s => s.UserId == userId);
                    users.TryGetValue(userId, out var user);
                    var totalCoins = (chat?.PinnedCoins ?? 0) + (pass?.FanPassCoins ?? 0);
                    return new
                    {
                        userId,
                        userName = user?.UserName,
                        totalCoins,
                        pinnedCoins = chat?.PinnedCoins ?? 0,
                        fanPassCoins = pass?.FanPassCoins ?? 0,
                        pinnedMessages = chat?.PinnedMessages ?? 0,
                        fanPasses = pass?.FanPasses ?? 0,
                        activeBadgeUrl = user?.ActiveBadgeUrl,
                        customThemePalette = user?.CustomThemePalette
                    };
                })
                .OrderByDescending(r => r.totalCoins)
                .ThenBy(r => r.userName)
                .Take(take)
                .Cast<object>()
                .ToList();
        }

        private static string ResolveReactionAnimation(string reaction)
        {
            var normalized = reaction.Trim().ToLowerInvariant();
            if (normalized.Contains("goal") || normalized.Contains("هدف") || normalized.Contains("⚽")) return "goal-fire";
            if (normalized.Contains("win") || normalized.Contains("فوز") || normalized.Contains("🏆")) return "trophy-confetti";
            if (normalized.Contains("heart") || normalized.Contains("حب") || normalized.Contains("❤️")) return "heart-burst";
            return "stadium-burst";
        }

        private async Task UnequipSameTypeAsync(string userId, CosmeticItemType type)
        {
            var equipped = await _context.UserCosmeticItems
                .Include(o => o.CosmeticItem)
                .Where(o => o.UserId == userId && o.IsEquipped && o.CosmeticItem.Type == type)
                .ToListAsync();
            foreach (var item in equipped) item.IsEquipped = false;
        }

        private static void ApplyCosmeticToUser(Models.Payment.ApplicationUser user, CosmeticItem cosmetic)
        {
            if (cosmetic.Type == CosmeticItemType.Theme && !string.IsNullOrWhiteSpace(cosmetic.ThemePalette))
            {
                user.CustomThemePalette = cosmetic.ThemePalette;
            }
            else if ((cosmetic.Type == CosmeticItemType.Badge || cosmetic.Type == CosmeticItemType.Emoji) && !string.IsNullOrWhiteSpace(cosmetic.AssetUrl))
            {
                user.ActiveBadgeUrl = cosmetic.AssetUrl;
            }
        }

    }
}
