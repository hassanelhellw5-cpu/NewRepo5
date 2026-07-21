using QemmaProject.Models.Payment;

namespace QemmaProject.Models.Sports
{
    public enum PredictionLeagueRole
    {
        Owner = 0,
        Admin = 1,
        Member = 2
    }

    public enum PredictionLeagueStatus
    {
        Active = 0,
        Closed = 1,
        Completed = 2
    }

    public enum PredictionLeagueFormat
    {
        Leaderboard = 0,
        Knockout = 1
    }

    public class PredictionLeague
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string OwnerUserId { get; set; } = string.Empty;
        public virtual ApplicationUser Owner { get; set; }
        public bool IsPublic { get; set; }
        public decimal CreationFeeCoins { get; set; }
        public decimal EntryFeeCoins { get; set; }
        public int MaxMembers { get; set; } = 20;
        public PredictionLeagueFormat Format { get; set; } = PredictionLeagueFormat.Leaderboard;
        public int KnockoutCurrentRound { get; set; }
        public decimal KnockoutWinnerBonusPoints { get; set; } = 100m;
        public DateTime? KnockoutActivatedAt { get; set; }
        public DateTime? KnockoutCompletedAt { get; set; }
        public string? ThemePalette { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? TrophyName { get; set; }
        public bool PremiumAnalyticsUnlocked { get; set; }
        public PredictionLeagueStatus Status { get; set; } = PredictionLeagueStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public virtual ICollection<PredictionLeagueMember> Members { get; set; } = new List<PredictionLeagueMember>();
    }

    public class PredictionLeagueMember
    {
        public int Id { get; set; }
        public int PredictionLeagueId { get; set; }
        public virtual PredictionLeague PredictionLeague { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public PredictionLeagueRole Role { get; set; } = PredictionLeagueRole.Member;
        public decimal TotalPoints { get; set; }
        public int KnockoutSeed { get; set; }
        public int KnockoutRound { get; set; }
        public bool IsKnockoutEliminated { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
