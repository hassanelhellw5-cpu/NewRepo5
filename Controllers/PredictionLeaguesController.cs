using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Sports;
using QemmaProject.Services;
using System.Security.Cryptography;
using QemmaProject.Models.Requests;
using QemmaProject.Models;

namespace QemmaProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PredictionLeaguesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CoinWalletService _wallet;

        public PredictionLeaguesController(AppDbContext context, CoinWalletService wallet)
        {
            _context = context;
            _wallet = wallet;
        }

        [HttpPost]
        public async Task<IActionResult> CreateLeague([FromBody] CreatePredictionLeagueRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OwnerUserId)) return BadRequest(new { message = "OwnerUserId is required." });
            if (!this.IsSelfOrAdmin(request.OwnerUserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "League name is required." });

            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.OwnerUserId);
            if (owner == null) return NotFound(new { message = "Owner user not found." });

            // Prediction leagues are intentionally free. The paid/private league
            // friction made private competitions less fun, so ignore any legacy
            // client-sent fees and keep creation/join costs at zero.
            const decimal freeLeagueFee = 0m;

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            var league = new PredictionLeague
            {
                Name = request.Name.Trim(),
                Code = await GenerateUniqueCodeAsync(),
                OwnerUserId = owner.Id,
                IsPublic = request.IsPublic,
                CreationFeeCoins = freeLeagueFee,
                EntryFeeCoins = freeLeagueFee,
                MaxMembers = Math.Clamp(request.MaxMembers <= 0 ? 20 : request.MaxMembers, 2, 500),
                Format = request.Format,
                KnockoutCurrentRound = request.Format == PredictionLeagueFormat.Knockout ? 1 : 0,
                KnockoutActivatedAt = request.Format == PredictionLeagueFormat.Knockout ? DateTime.UtcNow : null,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt
            };

            _context.PredictionLeagues.Add(league);
            await _context.SaveChangesAsync();

            _context.PredictionLeagueMembers.Add(new PredictionLeagueMember
            {
                PredictionLeagueId = league.Id,
                UserId = owner.Id,
                Role = PredictionLeagueRole.Owner,
                KnockoutSeed = league.Format == PredictionLeagueFormat.Knockout ? 1 : 0,
                KnockoutRound = league.Format == PredictionLeagueFormat.Knockout ? 1 : 0
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new
            {
                league.Id,
                league.Name,
                league.Code,
                league.IsPublic,
                league.CreationFeeCoins,
                league.EntryFeeCoins,
                league.MaxMembers,
                league.Format,
                league.KnockoutCurrentRound,
                league.KnockoutActivatedAt,
                owner.QemmaCoinsBalance
            });
        }

        [HttpPost("join")]
        public async Task<IActionResult> JoinLeague([FromBody] JoinPredictionLeagueRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (string.IsNullOrWhiteSpace(request.Code)) return BadRequest(new { message = "League code is required." });

            var league = await _context.PredictionLeagues
                .Include(l => l.Members)
                .FirstOrDefaultAsync(l => l.Code == request.Code.Trim().ToUpper());
            if (league == null || league.Status != PredictionLeagueStatus.Active)
            {
                return NotFound(new { message = "Prediction league not found or closed." });
            }

            if (league.Members.Any(m => m.UserId == request.UserId))
            {
                return Ok(new { message = "User already joined this league.", league.Id, league.Name });
            }

            if (league.Members.Count >= league.MaxMembers)
            {
                return BadRequest(new { message = "Prediction league is full." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            _context.PredictionLeagueMembers.Add(new PredictionLeagueMember
            {
                PredictionLeagueId = league.Id,
                UserId = user.Id,
                Role = PredictionLeagueRole.Member,
                KnockoutSeed = league.Format == PredictionLeagueFormat.Knockout ? league.Members.Count + 1 : 0,
                KnockoutRound = league.Format == PredictionLeagueFormat.Knockout ? Math.Max(1, league.KnockoutCurrentRound) : 0
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new { message = "Joined prediction league successfully.", league.Id, league.Name, user.QemmaCoinsBalance });
        }



        [HttpGet("my")]
        public async Task<IActionResult> MyLeagues([FromQuery] string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();

            var leagues = await _context.PredictionLeagueMembers
                .Include(m => m.PredictionLeague)
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.JoinedAt)
                .Select(m => new
                {
                    memberId = m.Id,
                    m.UserId,
                    m.Role,
                    m.TotalPoints,
                    m.KnockoutSeed,
                    m.KnockoutRound,
                    m.IsKnockoutEliminated,
                    m.JoinedAt,
                    league = new
                    {
                        m.PredictionLeague.Id,
                        m.PredictionLeague.Name,
                        m.PredictionLeague.Code,
                        m.PredictionLeague.IsPublic,
                        m.PredictionLeague.Status,
                        m.PredictionLeague.Format,
                        m.PredictionLeague.KnockoutCurrentRound,
                        m.PredictionLeague.StartsAt,
                        m.PredictionLeague.EndsAt,
                        m.PredictionLeague.CreatedAt
                    }
                })
                .ToListAsync();

            return Ok(leagues);
        }

        [HttpPost("{leagueId:int}/predict")]
        public async Task<IActionResult> Predict(int leagueId, [FromBody] LeaguePredictionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (request.PredictedHomeScore < 0 || request.PredictedAwayScore < 0) return BadRequest(new { message = "Predicted scores must be zero or greater." });

            var league = await _context.PredictionLeagues.Include(l => l.Members).FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null || league.Status != PredictionLeagueStatus.Active) return NotFound(new { message = "Prediction league not found or closed." });
            if (!league.Members.Any(m => m.UserId == request.UserId)) return BadRequest(new { message = "User must join the league before predicting." });

            var match = await _context.Matches.Include(m => m.HomeTeam).Include(m => m.AwayTeam).FirstOrDefaultAsync(m => m.Id == request.MatchId);
            if (match == null) return NotFound(new { message = "Match not found." });
            if (IsMatchLocked(match)) return BadRequest(new { message = "Predictions are locked for this match." });

            var prediction = await _context.Predictions.FirstOrDefaultAsync(p => p.PredictionLeagueId == leagueId && p.MatchId == request.MatchId && p.UserId == request.UserId);
            if (prediction == null)
            {
                prediction = new Prediction { PredictionLeagueId = leagueId, MatchId = request.MatchId, UserId = request.UserId };
                _context.Predictions.Add(prediction);
            }

            prediction.PredictedHomeScore = request.PredictedHomeScore;
            prediction.PredictedAwayScore = request.PredictedAwayScore;
            prediction.IsProcessed = false;
            prediction.PointsEarned = 0;
            prediction.CreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Prediction saved.",
                prediction.Id,
                prediction.PredictionLeagueId,
                prediction.MatchId,
                homeTeam = match.HomeTeam.Name,
                awayTeam = match.AwayTeam.Name,
                prediction.PredictedHomeScore,
                prediction.PredictedAwayScore,
                prediction.CreatedAt
            });
        }

        [HttpGet("{leagueId:int}/matches")]
        public async Task<IActionResult> GetPredictionMatches(int leagueId, [FromQuery] string? userId = null, [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            var league = await _context.PredictionLeagues.FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });
            if (!string.IsNullOrWhiteSpace(userId) && !this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();

            var fromDate = DateOnly.FromDateTime((from ?? DateTime.UtcNow.AddDays(-1)).Date);
            var toDate = DateOnly.FromDateTime((to ?? DateTime.UtcNow.AddDays(14)).Date);
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

            var predictions = string.IsNullOrWhiteSpace(userId)
                ? new List<Prediction>()
                : await _context.Predictions.Where(p => p.PredictionLeagueId == leagueId && p.UserId == userId).ToListAsync();

            var matches = await _context.Matches
                .Include(m => m.HomeTeam)
                .Include(m => m.AwayTeam)
                .Include(m => m.Tournament)
                .Where(m => m.MatchDate >= fromDate && m.MatchDate <= toDate)
                .OrderBy(m => m.MatchDate)
                .ThenBy(m => m.Time)
                .Take(300)
                .ToListAsync();

            return Ok(new
            {
                league.Id,
                league.Name,
                from = fromDate,
                to = toDate,
                matches = matches.Select(m =>
                {
                    var prediction = predictions.FirstOrDefault(p => p.MatchId == m.Id);
                    return new
                    {
                        m.Id,
                        m.MatchId,
                        tournament = m.Tournament == null ? null : m.Tournament.Name,
                        homeTeam = m.HomeTeam.Name,
                        awayTeam = m.AwayTeam.Name,
                        m.MatchDate,
                        m.Time,
                        m.Status,
                        m.ScoreHome,
                        m.ScoreAway,
                        lockedForPrediction = IsMatchLocked(m),
                        prediction = prediction == null ? null : new { prediction.Id, prediction.PredictedHomeScore, prediction.PredictedAwayScore, prediction.IsProcessed, prediction.PointsEarned }
                    };
                })
            });
        }

        [AllowAnonymous]
        [HttpGet("{leagueId:int}/leaderboard")]
        public async Task<IActionResult> GetLeaderboard(int leagueId)
        {
            var league = await _context.PredictionLeagues.FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });

            var rows = await _context.PredictionLeagueMembers
                .Where(m => m.PredictionLeagueId == leagueId)
                .Select(m => new
                {
                    m.UserId,
                    userName = m.User.UserName,
                    m.Role,
                    m.TotalPoints,
                    m.KnockoutSeed,
                    m.KnockoutRound,
                    m.IsKnockoutEliminated,
                    m.JoinedAt
                })
                .OrderBy(m => m.IsKnockoutEliminated)
                .ThenByDescending(m => m.KnockoutRound)
                .ThenByDescending(m => m.TotalPoints)
                .ThenBy(m => m.JoinedAt)
                .ToListAsync();

            return Ok(new { league.Id, league.Name, league.Code, league.Format, league.KnockoutCurrentRound, league.KnockoutWinnerBonusPoints, leaderboard = rows });
        }

        [HttpPost("{leagueId:int}/customization")]
        public async Task<IActionResult> UpdateLeagueCustomization(int leagueId, [FromBody] UpdateLeagueCustomizationRequest request)
        {
            var league = await _context.PredictionLeagues.FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            if (!string.IsNullOrWhiteSpace(request.UserId) && !this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (!string.IsNullOrWhiteSpace(request.UserId) && request.PriceCoins > 0)
            {
                var user = await _wallet.GetUserAsync(request.UserId);
                if (user == null) return NotFound(new { message = "User not found." });
                await _wallet.DebitAsync(user, request.PriceCoins, CoinTransactionTypes.LeagueCustomization,
                    $"League customization for league #{league.Id} ({league.Name})");
            }

            league.ThemePalette = string.IsNullOrWhiteSpace(request.ThemePalette) ? league.ThemePalette : request.ThemePalette.Trim();
            league.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? league.CoverImageUrl : request.CoverImageUrl.Trim();
            league.TrophyName = string.IsNullOrWhiteSpace(request.TrophyName) ? league.TrophyName : request.TrophyName.Trim();
            if (request.UnlockPremiumAnalytics)
            {
                league.PremiumAnalyticsUnlocked = true;
            }

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            return Ok(new
            {
                message = "League customization updated.",
                league.Id,
                league.ThemePalette,
                league.CoverImageUrl,
                league.TrophyName,
                league.PremiumAnalyticsUnlocked
            });
        }

        [AllowAnonymous]
        [HttpGet("{leagueId:int}/analytics")]
        public async Task<IActionResult> GetLeagueAnalytics(int leagueId)
        {
            var league = await _context.PredictionLeagues.FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });

            var members = await _context.PredictionLeagueMembers
                .Where(m => m.PredictionLeagueId == leagueId)
                .Select(m => new { m.UserId, userName = m.User.UserName, m.TotalPoints, m.JoinedAt, m.KnockoutRound, m.IsKnockoutEliminated })
                .ToListAsync();

            var predictions = await _context.Predictions
                .Where(p => p.PredictionLeagueId == leagueId)
                .Select(p => new { p.UserId, p.IsProcessed, p.PointsEarned, p.PredictedHomeScore, p.PredictedAwayScore })
                .ToListAsync();

            var topMembers = members
                .OrderByDescending(m => m.TotalPoints)
                .ThenBy(m => m.JoinedAt)
                .Take(10)
                .ToList();

            var predictionRows = predictions
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    userId = g.Key,
                    totalPredictions = g.Count(),
                    processed = g.Count(p => p.IsProcessed),
                    totalPredictionPoints = g.Sum(p => p.PointsEarned),
                    exactScoreAttempts = g.Count(p => p.PredictedHomeScore >= 0 && p.PredictedAwayScore >= 0)
                })
                .OrderByDescending(r => r.totalPredictionPoints)
                .ToList();
            object predictionRowsResult = league.PremiumAnalyticsUnlocked ? predictionRows : new List<object>();

            return Ok(new
            {
                league.Id,
                league.Name,
                league.PremiumAnalyticsUnlocked,
                totals = new
                {
                    members = members.Count,
                    predictions = predictions.Count,
                    processedPredictions = predictions.Count(p => p.IsProcessed),
                    activeKnockoutMembers = members.Count(m => !m.IsKnockoutEliminated && m.KnockoutRound > 0)
                },
                topMembers,
                predictionRows = predictionRowsResult,
                upsell = league.PremiumAnalyticsUnlocked ? null : "Unlock premium analytics to view per-user prediction breakdowns."
            });
        }

        [HttpPost("{leagueId:int}/convert-to-knockout")]
        public async Task<IActionResult> ConvertToKnockout(int leagueId, [FromBody] ConvertToKnockoutRequest request)
        {
            request ??= new ConvertToKnockoutRequest();
            var league = await _context.PredictionLeagues
                .Include(l => l.Members)
                .FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });
            if (!IsLeagueOwnerOrAdmin(league)) return this.ForbiddenUser();
            if (league.Members.Count < 2) return BadRequest(new { message = "Knockout needs at least 2 members." });

            var seededMembers = league.Members
                .OrderByDescending(m => m.TotalPoints)
                .ThenBy(m => m.JoinedAt)
                .ToList();

            for (var i = 0; i < seededMembers.Count; i++)
            {
                seededMembers[i].KnockoutSeed = i + 1;
                seededMembers[i].KnockoutRound = 1;
                seededMembers[i].IsKnockoutEliminated = false;
            }

            league.Format = PredictionLeagueFormat.Knockout;
            league.KnockoutCurrentRound = 1;
            league.KnockoutWinnerBonusPoints = request.WinnerBonusPoints <= 0 ? 100m : request.WinnerBonusPoints;
            league.KnockoutActivatedAt = DateTime.UtcNow;
            league.KnockoutCompletedAt = null;

            await _context.SaveChangesAsync();
            return Ok(BuildKnockoutResponse(league, seededMembers));
        }

        [HttpPost("{leagueId:int}/advance-knockout-round")]
        public async Task<IActionResult> AdvanceKnockoutRound(int leagueId)
        {
            var league = await _context.PredictionLeagues
                .Include(l => l.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });
            if (!IsLeagueOwnerOrAdmin(league)) return this.ForbiddenUser();
            if (league.Format != PredictionLeagueFormat.Knockout)
            {
                return BadRequest(new { message = "Prediction league is not in knockout format." });
            }

            var activeMembers = league.Members
                .Where(m => !m.IsKnockoutEliminated && m.KnockoutRound == league.KnockoutCurrentRound)
                .OrderByDescending(m => m.TotalPoints)
                .ThenBy(m => m.KnockoutSeed)
                .ToList();

            if (activeMembers.Count <= 1)
            {
                var champion = activeMembers.FirstOrDefault() ?? league.Members
                    .Where(m => !m.IsKnockoutEliminated)
                    .OrderByDescending(m => m.TotalPoints)
                    .ThenBy(m => m.KnockoutSeed)
                    .FirstOrDefault();
                if (champion == null) return BadRequest(new { message = "No active knockout member found." });

                champion.TotalPoints += league.KnockoutWinnerBonusPoints;
                league.Status = PredictionLeagueStatus.Completed;
                league.KnockoutCompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Knockout completed.", champion = ToKnockoutMember(champion), league.KnockoutWinnerBonusPoints });
            }

            var winners = new List<PredictionLeagueMember>();
            var eliminated = new List<PredictionLeagueMember>();
            for (var i = 0; i < activeMembers.Count; i += 2)
            {
                if (i == activeMembers.Count - 1)
                {
                    winners.Add(activeMembers[i]);
                    continue;
                }

                var first = activeMembers[i];
                var second = activeMembers[i + 1];
                var winner = first.TotalPoints > second.TotalPoints ? first :
                    second.TotalPoints > first.TotalPoints ? second :
                    first.KnockoutSeed <= second.KnockoutSeed ? first : second;
                var loser = winner.Id == first.Id ? second : first;

                winners.Add(winner);
                loser.IsKnockoutEliminated = true;
                eliminated.Add(loser);
            }

            league.KnockoutCurrentRound += 1;
            foreach (var winner in winners)
            {
                winner.KnockoutRound = league.KnockoutCurrentRound;
            }

            await _context.SaveChangesAsync();
            return Ok(new
            {
                message = "Knockout round advanced.",
                league.Id,
                league.KnockoutCurrentRound,
                winners = winners.Select(ToKnockoutMember),
                eliminated = eliminated.Select(ToKnockoutMember),
                knockout = BuildKnockoutResponse(league, league.Members)
            });
        }

        [AllowAnonymous]
        [HttpGet("{leagueId:int}/knockout")]
        public async Task<IActionResult> GetKnockout(int leagueId)
        {
            var league = await _context.PredictionLeagues
                .Include(l => l.Members)
                .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(l => l.Id == leagueId);
            if (league == null) return NotFound(new { message = "Prediction league not found." });
            return Ok(BuildKnockoutResponse(league, league.Members));
        }

        private static bool IsMatchLocked(Match match)
        {
            var status = match.Status ?? string.Empty;
            return status.Contains("انته") || status.Contains("Finished", StringComparison.OrdinalIgnoreCase) || status.Contains("FT", StringComparison.OrdinalIgnoreCase) || status.Contains("Live", StringComparison.OrdinalIgnoreCase) || status.Contains("مباشر");
        }

        private bool IsLeagueOwnerOrAdmin(PredictionLeague league) => User.IsInRole("Admin") || this.IsSelfOrAdmin(league.OwnerUserId);

        private static object BuildKnockoutResponse(PredictionLeague league, IEnumerable<PredictionLeagueMember> members)
        {
            var bracket = members
                .GroupBy(m => Math.Max(1, m.KnockoutRound))
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    round = g.Key,
                    members = g
                        .OrderBy(m => m.IsKnockoutEliminated)
                        .ThenBy(m => m.KnockoutSeed == 0 ? int.MaxValue : m.KnockoutSeed)
                        .Select(ToKnockoutMember)
                        .ToList()
                })
                .ToList();

            var champion = league.Status == PredictionLeagueStatus.Completed
                ? members
                    .Where(m => !m.IsKnockoutEliminated)
                    .OrderByDescending(m => m.TotalPoints)
                    .ThenBy(m => m.KnockoutSeed == 0 ? int.MaxValue : m.KnockoutSeed)
                    .Select(ToKnockoutMember)
                    .FirstOrDefault()
                : null;

            return new
            {
                league.Id,
                league.Name,
                league.Code,
                league.Status,
                league.Format,
                league.KnockoutCurrentRound,
                league.KnockoutWinnerBonusPoints,
                league.KnockoutActivatedAt,
                league.KnockoutCompletedAt,
                champion,
                bracket
            };
        }

        private static object ToKnockoutMember(PredictionLeagueMember member)
        {
            return new
            {
                member.Id,
                member.UserId,
                userName = member.User?.UserName,
                member.Role,
                member.TotalPoints,
                member.KnockoutSeed,
                member.KnockoutRound,
                member.IsKnockoutEliminated,
                member.JoinedAt
            };
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var bytes = RandomNumberGenerator.GetBytes(6);
                var code = new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
                if (!await _context.PredictionLeagues.AnyAsync(l => l.Code == code)) return code;
            }

            return Guid.NewGuid().ToString("N")[..8].ToUpper();
        }

    }
}
