using QemmaProject.Models.Engagement;
using QemmaProject.Models.Sports;

namespace QemmaProject.Models.Requests
{
    // Match summary requests
    public record GenerateSummaryRequest(string Source = "Heuristic", string Locale = "ar-EG", string? Summary = null);
    // Fantasy requests
    public record CreateContestRequest(int TournamentId, DateTime ContestDate, string? Name, string OwnerUserId = "", bool IsPublic = false, int MaxMembers = 20, PredictionLeagueFormat Format = PredictionLeagueFormat.Leaderboard);
    public record JoinFantasyContestRequest(string UserId, string Code);
    public record PickPlayersRequest(string UserId, List<int> PlayerIds);
    // Cosmetic and player requests
    public record LimitedCosmeticRequest(string Name, string Slug, CosmeticItemType Type, string? Description, string? AssetUrl, string? ThemePalette, int? MatchId, DateTime? AvailableFrom, DateTime? AvailableUntil, decimal PriceCoins);
    public record UpdatePlayerImageRequest(string? ImageUrl, string? ImageSource, string? ExternalPlayerId);
    // Watch party requests
    public record CreateWatchPartyRequest(int MatchId, string HostUserId, string? Title);
    public record JoinWatchPartyRequest(string UserId);
    public record WatchPartyMessageRequest(string UserId, string? Message, string? Reaction, string? Prediction);
    public record WatchPartySyncRequest(string UserId, double CurrentTime, bool IsPlaying, double PlaybackRate = 1, string? Source = null);
    // Notification requests
    public record FavoriteTeamRequest(string UserId, int TeamId);
    public record PushSubscriptionRequest(string UserId, string Endpoint, string? P256dh, string? Auth, string? Provider, string? DeviceName);
    public record PreferenceRequest(string UserId, bool MatchStart, bool LineupReleased, bool Goals, bool Summaries, bool FantasyPoints, bool OneHourReminder, bool PushEnabled);

    // Fan engagement requests
    public class PurchaseCosmeticRequest { public string UserId { get; set; } = string.Empty; public bool EquipNow { get; set; } = true; }
    public class SubscribeSupporterRequest { public string UserId { get; set; } = string.Empty; public int Months { get; set; } = 1; public decimal PriceCoins { get; set; } }
    public class BuyMatchFanPassRequest { public string UserId { get; set; } = string.Empty; public string? Title { get; set; } public string? BadgeText { get; set; } public decimal PriceCoins { get; set; } }
    public class CreateCustomTournamentRequest { public string OrganizerUserId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public string? CommunityName { get; set; } public string? ContactInfo { get; set; } public string? RequestedFeatures { get; set; } public decimal EstimatedBudgetCoins { get; set; } }
    public class EquipCosmeticRequest { public string UserId { get; set; } = string.Empty; }
    public class SendChatMessageRequest { public string UserId { get; set; } = string.Empty; public string Message { get; set; } = string.Empty; public int? CheerPhraseId { get; set; } public bool Pin { get; set; } public decimal PinCoins { get; set; } public int PinMinutes { get; set; } }
    public class MatchReactionRequest { public string UserId { get; set; } = string.Empty; public string Reaction { get; set; } = string.Empty; public string? Message { get; set; } public int? MatchEventId { get; set; } }

    // Admin requests
    public class AdminPaymentDecisionRequest { public string? AdminNotes { get; set; } }
    public class AdjustUserCoinsRequest { public decimal Amount { get; set; } public string? Notes { get; set; } }
    public class UpdateUserRolesRequest { public List<string> Roles { get; set; } = new List<string>(); }
    public class UpsertCosmeticRequest { public CosmeticItemType Type { get; set; } public CosmeticUnlockType UnlockType { get; set; } = CosmeticUnlockType.Free; public string Name { get; set; } = string.Empty; public string? Slug { get; set; } public string? TeamName { get; set; } public string? PlayerName { get; set; } public string? Category { get; set; } public string? Description { get; set; } public string? AssetUrl { get; set; } public string? ThemePalette { get; set; } public int? MatchId { get; set; } public bool IsLimited { get; set; } public DateTime? AvailableFrom { get; set; } public DateTime? AvailableUntil { get; set; } public decimal PriceCoins { get; set; } public decimal PriceMoney { get; set; } public string Currency { get; set; } = "EGP"; public bool IsActive { get; set; } = true; public bool IsFeatured { get; set; } }
    public class UpdateCustomTournamentStatusRequest { public string Status { get; set; } = string.Empty; }
    // Auth requests
    public class RegisterRequest { public string Email { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; public string? UserName { get; set; } public string? PhoneNumber { get; set; } }
    public class LoginRequest { public string EmailOrUserName { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; }
    // Prediction league requests
    public class CreatePredictionLeagueRequest { public string OwnerUserId { get; set; } = string.Empty; public string Name { get; set; } = string.Empty; public bool IsPublic { get; set; } public PredictionLeagueFormat Format { get; set; } = PredictionLeagueFormat.Leaderboard; public decimal CreationFeeCoins { get; set; } public decimal EntryFeeCoins { get; set; } public int MaxMembers { get; set; } = 20; public DateTime? StartsAt { get; set; } public DateTime? EndsAt { get; set; } }
    public class JoinPredictionLeagueRequest { public string UserId { get; set; } = string.Empty; public string Code { get; set; } = string.Empty; }
    public class ConvertToKnockoutRequest { public decimal WinnerBonusPoints { get; set; } = 100m; }
    public class LeaguePredictionRequest { public string UserId { get; set; } = string.Empty; public int MatchId { get; set; } public int PredictedHomeScore { get; set; } public int PredictedAwayScore { get; set; } }
    public class UpdateLeagueCustomizationRequest { public string? UserId { get; set; } public string? ThemePalette { get; set; } public string? CoverImageUrl { get; set; } public string? TrophyName { get; set; } public bool UnlockPremiumAnalytics { get; set; } public decimal PriceCoins { get; set; } }
    // Coin and payment requests
    public class CreateManualPaymentRequest { public string UserId { get; set; } = string.Empty; public decimal AmountPaid { get; set; } public decimal RequestedCoins { get; set; } public string PaymentMethod { get; set; } = string.Empty; public string? PhoneNumberUsed { get; set; } public string? ReceiptImageUrl { get; set; } }
    public class ProcessManualPaymentRequest { public string? AdminNotes { get; set; } }
    public class CompleteRewardedVideoRequest { public string UserId { get; set; } = string.Empty; public string Provider { get; set; } = string.Empty; public string? PlacementId { get; set; } public string ExternalRewardId { get; set; } = string.Empty; public decimal CoinsAwarded { get; set; } public string? Signature { get; set; } }
}
