using Microsoft.EntityFrameworkCore;
using QemmaProject.Data;
using QemmaProject.Models.Payment;

namespace QemmaProject.Services
{
    public static class CoinTransactionTypes
    {
        public const string ManualDeposit = "ManualDeposit";
        public const string RewardedVideo = "RewardedVideo";
        public const string CreatePredictionLeague = "CreatePredictionLeague";
        public const string JoinPredictionLeague = "JoinPredictionLeague";
        public const string BuyCosmetic = "BuyCosmetic";
        public const string BuyMatchFanPass = "BuyMatchFanPass";
        public const string BuySupporter = "BuySupporter";
        public const string LeagueCustomization = "LeagueCustomization";
        public const string PinLiveComment = "PinLiveComment";
        public const string PremiumReactionBurst = "PremiumReactionBurst";
    }

    public class CoinWalletService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public CoinWalletService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public decimal GetMaxRewardedVideoCoins()
            => Math.Max(0, _configuration.GetValue<decimal?>("Rewards:MaxVideoCoins") ?? 10m);

        public decimal GetDefaultRewardedVideoCoins()
            => Math.Max(0, _configuration.GetValue<decimal?>("Rewards:DefaultVideoCoins") ?? GetMaxRewardedVideoCoins());

        public async Task<ApplicationUser?> GetUserAsync(string userId)
            => await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        public Task<Transaction> CreditAsync(ApplicationUser user, decimal coins, string transactionType, string notes)
        {
            if (coins <= 0) throw new ArgumentOutOfRangeException(nameof(coins), "Coins must be greater than zero.");

            user.QemmaCoinsBalance += coins;
            var transaction = new Transaction
            {
                UserId = user.Id,
                Amount = coins,
                TransactionType = transactionType,
                CreatedAt = DateTime.UtcNow,
                Notes = notes
            };
            _context.Transactions.Add(transaction);
            return Task.FromResult(transaction);
        }

        public Task<Transaction> DebitAsync(ApplicationUser user, decimal coins, string transactionType, string notes)
        {
            if (coins <= 0) throw new ArgumentOutOfRangeException(nameof(coins), "Coins must be greater than zero.");
            if (user.QemmaCoinsBalance < coins) throw new InvalidOperationException("Insufficient Qemma Coins balance.");

            user.QemmaCoinsBalance -= coins;
            var transaction = new Transaction
            {
                UserId = user.Id,
                Amount = -coins,
                TransactionType = transactionType,
                CreatedAt = DateTime.UtcNow,
                Notes = notes
            };
            _context.Transactions.Add(transaction);
            return Task.FromResult(transaction);
        }
    }
}
