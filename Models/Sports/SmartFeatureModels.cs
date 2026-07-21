using QemmaProject.Models.Payment;

namespace QemmaProject.Models.Sports
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NormalizedName { get; set; }
        public int? TeamId { get; set; }
        public virtual Team? Team { get; set; }
        public string? TeamName { get; set; }
        public string? Position { get; set; }
        public string? ShirtNumber { get; set; }
        public string? ImageUrl { get; set; }
        public string? ImageSource { get; set; }
        public string? ExternalPlayerId { get; set; }
        public string? Nationality { get; set; }
        public DateTime? BirthDate { get; set; }
        public int? Age { get; set; }
        public string? Height { get; set; }
        public string? PreferredFoot { get; set; }
        public string? FormerTeamsJson { get; set; }
        public string? ProfileMetadataJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class PlayerMatchStat
    {
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public virtual Player Player { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int MinutesPlayed { get; set; }
        public int Shots { get; set; }
        public int KeyPasses { get; set; }
        public int Saves { get; set; }
        public int PenaltiesScored { get; set; }
        public int PenaltiesMissed { get; set; }
        public bool CleanSheet { get; set; }
        public bool Started { get; set; }
        public bool Substitute { get; set; }
        public int FantasyPoints { get; set; }
        public decimal Rating { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class FantasyContest
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public virtual Tournament Tournament { get; set; }
        public DateTime ContestDate { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string? OwnerUserId { get; set; }
        public virtual ApplicationUser? Owner { get; set; }
        public bool IsPublic { get; set; }
        public int MaxMembers { get; set; } = 20;
        public PredictionLeagueFormat Format { get; set; } = PredictionLeagueFormat.Leaderboard;
        public int KnockoutCurrentRound { get; set; }
        public decimal KnockoutWinnerBonusPoints { get; set; } = 100m;
        public DateTime? KnockoutActivatedAt { get; set; }
        public DateTime? KnockoutCompletedAt { get; set; }
        public bool IsOpen { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<FantasyEntry> Entries { get; set; } = new List<FantasyEntry>();
    }

    public class FantasyEntry
    {
        public int Id { get; set; }
        public int FantasyContestId { get; set; }
        public virtual FantasyContest FantasyContest { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public PredictionLeagueRole Role { get; set; } = PredictionLeagueRole.Member;
        public int TotalPoints { get; set; }
        public int KnockoutSeed { get; set; }
        public int KnockoutRound { get; set; }
        public bool IsKnockoutEliminated { get; set; }
        public int FreeTransfersBanked { get; set; } = 1;
        public int TransfersMadeThisRound { get; set; }
        public int TransferPenaltyPoints { get; set; }
        public int LastTransferRound { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<FantasyEntryPick> Picks { get; set; } = new List<FantasyEntryPick>();
    }

    public class FantasyEntryPick
    {
        public int Id { get; set; }
        public int FantasyEntryId { get; set; }
        public virtual FantasyEntry FantasyEntry { get; set; }
        public int PlayerId { get; set; }
        public virtual Player Player { get; set; }
        public int Points { get; set; }
    }

    public class WatchPartyRoom
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string HostUserId { get; set; } = string.Empty;
        public virtual ApplicationUser HostUser { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<WatchPartyMember> Members { get; set; } = new List<WatchPartyMember>();
    }

    public class WatchPartyMember
    {
        public int Id { get; set; }
        public int WatchPartyRoomId { get; set; }
        public virtual WatchPartyRoom WatchPartyRoom { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public bool IsHost { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }

    public class WatchPartyMessage
    {
        public int Id { get; set; }
        public int WatchPartyRoomId { get; set; }
        public virtual WatchPartyRoom WatchPartyRoom { get; set; }
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Reaction { get; set; }
        public string? Prediction { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserNotificationPreference
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public bool MatchStart { get; set; } = true;
        public bool LineupReleased { get; set; } = true;
        public bool Goals { get; set; } = true;
        public bool Summaries { get; set; } = true;
        public bool FantasyPoints { get; set; } = true;
        public bool OneHourReminder { get; set; } = true;
        public bool PushEnabled { get; set; } = true;
    }

    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int? MatchId { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }


    public class PushSubscription
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
        public string Provider { get; set; } = "WebPush";
        public string? DeviceName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class MatchSummary
    {
        public int Id { get; set; }
        public int MatchId { get; set; }
        public virtual Match Match { get; set; }
        public string Source { get; set; } = "Manual";
        public string Locale { get; set; } = "ar-EG";
        public string Summary { get; set; } = string.Empty;
        public string? HighlightsJson { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class UserFavoriteTeam
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int TeamId { get; set; }
        public virtual Team Team { get; set; }
    }

    public class UserStreak
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = "DailyLogin";
        public int CurrentCount { get; set; }
        public int BestCount { get; set; }
        public DateTime? LastActivityDate { get; set; }
    }

    public class AchievementDefinition
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int TargetCount { get; set; }
        public string RewardType { get; set; } = "Badge";
        public decimal RewardCoins { get; set; }
    }

    public class UserAchievement
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int AchievementDefinitionId { get; set; }
        public virtual AchievementDefinition AchievementDefinition { get; set; }
        public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
    }
}
