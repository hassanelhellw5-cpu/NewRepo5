using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Payment;
using QemmaProject.Services;
using System.Security.Cryptography;
using System.Text;
using QemmaProject.Models.Requests;

namespace QemmaProject.Controllers
{
    [Route("api/coins")]
    [ApiController]
    [Authorize]
    public class CoinsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CoinWalletService _wallet;
        private readonly IConfiguration _configuration;

        public CoinsController(AppDbContext context, CoinWalletService wallet, IConfiguration configuration)
        {
            _context = context;
            _wallet = wallet;
            _configuration = configuration;
        }

        [HttpGet("wallet/{userId}")]
        public async Task<IActionResult> GetWallet(string userId)
        {
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();
            var user = await _wallet.GetUserAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var recentTransactions = await _context.Transactions
                .Where(t => t.UserId == user.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(25)
                .Select(t => new { t.Id, t.Amount, t.TransactionType, t.CreatedAt, t.Notes })
                .ToListAsync();

            return Ok(new { user.Id, user.UserName, user.QemmaCoinsBalance, recentTransactions });
        }

        [HttpGet("transactions/{userId}")]
        public async Task<IActionResult> GetTransactions(string userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        {
            if (!this.IsSelfOrAdmin(userId)) return this.ForbiddenUser();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists) return NotFound(new { message = "User not found." });

            var query = _context.Transactions.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt);
            var total = await query.CountAsync();
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(t => new { t.Id, t.Amount, t.TransactionType, t.CreatedAt, t.Notes })
                .ToListAsync();

            return Ok(new { page, pageSize, total, items });
        }

        [HttpPost("payments/manual")]
        public async Task<IActionResult> CreateManualPaymentRequest([FromBody] CreateManualPaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (!this.IsSelfOrAdmin(request.UserId)) return this.ForbiddenUser();
            if (request.AmountPaid <= 0) return BadRequest(new { message = "AmountPaid must be greater than zero." });
            if (request.RequestedCoins <= 0) return BadRequest(new { message = "RequestedCoins must be greater than zero." });
            if (string.IsNullOrWhiteSpace(request.PaymentMethod)) return BadRequest(new { message = "PaymentMethod is required." });

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var paymentRequest = new ManualPaymentRequest
            {
                UserId = user.Id,
                AmountPaid = request.AmountPaid,
                RequestedCoins = request.RequestedCoins,
                PaymentMethod = request.PaymentMethod.Trim(),
                PhoneNumberUsed = request.PhoneNumberUsed?.Trim() ?? string.Empty,
                ReceiptImageUrl = request.ReceiptImageUrl?.Trim() ?? string.Empty,
                Status = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.ManualPaymentRequests.Add(paymentRequest);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Manual payment request submitted.", paymentRequest.Id, paymentRequest.Status });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("payments/manual")]
        public async Task<IActionResult> GetManualPaymentRequests([FromQuery] PaymentStatus? status = null)
        {
            var query = _context.ManualPaymentRequests.Include(r => r.User).AsQueryable();
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);

            var items = await query.OrderByDescending(r => r.CreatedAt)
                .Take(200)
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

            return Ok(items);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("payments/manual/{requestId:int}/approve")]
        public async Task<IActionResult> ApproveManualPayment(int requestId, [FromBody] ProcessManualPaymentRequest request)
        {
            var paymentRequest = await _context.ManualPaymentRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == requestId);
            if (paymentRequest == null) return NotFound(new { message = "Payment request not found." });
            if (paymentRequest.Status != PaymentStatus.Pending) return BadRequest(new { message = "Payment request was already processed." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            paymentRequest.Status = PaymentStatus.Approved;
            paymentRequest.ProcessedAt = DateTime.UtcNow;
            paymentRequest.AdminNotes = request.AdminNotes ?? string.Empty;
            await _wallet.CreditAsync(paymentRequest.User, paymentRequest.RequestedCoins, CoinTransactionTypes.ManualDeposit,
                $"Approved manual payment request #{paymentRequest.Id} via {paymentRequest.PaymentMethod}");

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new { message = "Payment approved and coins credited.", paymentRequest.UserId, paymentRequest.User.QemmaCoinsBalance });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("payments/manual/{requestId:int}/reject")]
        public async Task<IActionResult> RejectManualPayment(int requestId, [FromBody] ProcessManualPaymentRequest request)
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

        [AllowAnonymous]
        [HttpPost("rewards/video/complete")]
        public async Task<IActionResult> CompleteRewardedVideo([FromBody] CompleteRewardedVideoRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId)) return BadRequest(new { message = "UserId is required." });
            if (string.IsNullOrWhiteSpace(request.Provider)) return BadRequest(new { message = "Provider is required." });
            if (string.IsNullOrWhiteSpace(request.ExternalRewardId)) return BadRequest(new { message = "ExternalRewardId is required." });

            if (!IsValidRewardSignature(request)) return Unauthorized(new { message = "Invalid rewarded video signature." });

            var alreadyRewarded = await _context.RewardedVideoViews.AnyAsync(v =>
                v.Provider == request.Provider && v.ExternalRewardId == request.ExternalRewardId);
            if (alreadyRewarded) return Conflict(new { message = "Reward was already credited." });

            var user = await _wallet.GetUserAsync(request.UserId);
            if (user == null) return NotFound(new { message = "User not found." });

            var maxReward = _wallet.GetMaxRewardedVideoCoins();
            var defaultReward = _wallet.GetDefaultRewardedVideoCoins();
            var coins = request.CoinsAwarded <= 0 ? defaultReward : Math.Min(request.CoinsAwarded, maxReward);
            if (coins <= 0) return BadRequest(new { message = "Rewarded video coins are not configured." });

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            _context.RewardedVideoViews.Add(new RewardedVideoView
            {
                UserId = user.Id,
                Provider = request.Provider.Trim(),
                PlacementId = request.PlacementId?.Trim() ?? string.Empty,
                ExternalRewardId = request.ExternalRewardId.Trim(),
                CoinsAwarded = coins,
                CreatedAt = DateTime.UtcNow
            });
            await _wallet.CreditAsync(user, coins, CoinTransactionTypes.RewardedVideo,
                $"Rewarded video from {request.Provider}, placement {request.PlacementId}, reward {request.ExternalRewardId}");

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return Ok(new { message = "Reward credited.", coinsAwarded = coins, user.QemmaCoinsBalance });
        }

        private bool IsValidRewardSignature(CompleteRewardedVideoRequest request)
        {
            var secret = _configuration["Rewards:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret)) return true;
            if (string.IsNullOrWhiteSpace(request.Signature)) return false;

            var payload = $"{request.UserId}|{request.Provider}|{request.ExternalRewardId}|{request.CoinsAwarded}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
            var providedBytes = Encoding.UTF8.GetBytes(request.Signature);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            return providedBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
        }
    }
}
