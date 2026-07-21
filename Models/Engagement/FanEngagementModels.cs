using QemmaProject.Models.Payment;
using QemmaProject.Models.Sports;

namespace QemmaProject.Models.Engagement
{
    public enum CosmeticItemType
    {
        Theme = 0,
        Emoji = 1,
        Badge = 2,
        Frame = 3,
        Sticker = 4,
        MatchPass = 5
    }

    public enum CosmeticUnlockType
    {
        Free = 0,
        Coins = 1,
        Money = 2
    }

    public class CosmeticItem
    {
        public int Id { get; set; }
        public CosmeticItemType Type { get; set; }
        public CosmeticUnlockType UnlockType { get; set; } = CosmeticUnlockType.Free;
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? PlayerName { get; set; }
        public string? Category { get; set; }
        public string? Description { get; set; }
        public string? AssetUrl { get; set; }
        public string? ThemePalette { get; set; }
        public int? MatchId { get; set; }
        public bool IsLimited { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableUntil { get; set; }
        public decimal PriceCoins { get; set; }
        public decimal PriceMoney { get; set; }
        public string Currency { get; set; } = "EGP";
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserCosmeticItem
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public int CosmeticItemId { get; set; }
        public virtual CosmeticItem CosmeticItem { get; set; }
        public bool IsEquipped { get; set; }
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }

    public class MatchFanPass
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public decimal PaidCoins { get; set; }
        public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    }

    public class CustomTournamentRequest
    {
        public int Id { get; set; }
        public string OrganizerUserId { get; set; } = string.Empty;
        public virtual ApplicationUser OrganizerUser { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CommunityName { get; set; } = string.Empty;
        public string ContactInfo { get; set; } = string.Empty;
        public string RequestedFeatures { get; set; } = string.Empty;
        public decimal EstimatedBudgetCoins { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CheerPhrase
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? TeamName { get; set; }
        public string? Locale { get; set; } = "ar-EG";
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class LiveChatMessage
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? CheerPhraseId { get; set; }
        public virtual CheerPhrase? CheerPhrase { get; set; }
        public bool IsPinned { get; set; }
        public decimal PaidCoins { get; set; }
        public DateTime? PinnedUntil { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }


}
