using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QemmaProject.Models.Engagement;
using QemmaProject.Models.Payment;
using QemmaProject.Models.Sports; // ضفنا الـ Namespace الجديد

namespace QemmaProject.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // جداول الدفع
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<ManualPaymentRequest> ManualPaymentRequests { get; set; }
        public DbSet<RewardedVideoView> RewardedVideoViews { get; set; }
        public DbSet<MatchLineup> MatchLineups { get; set; }

        // جداول الرياضة
        public DbSet<Team> Teams { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<Prediction> Predictions { get; set; }
        public DbSet<PredictionLeague> PredictionLeagues { get; set; }
        public DbSet<PredictionLeagueMember> PredictionLeagueMembers { get; set; }
        public DbSet<ArchiveVideo> ArchiveVideos { get; set; }
        public DbSet<DateSyncLog> DateSyncLogs { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<MatchEvent> MatchEvents { get; set; }
        public DbSet<MatchStatistic> MatchStatistics { get; set; }
        public DbSet<MatchVideo> MatchVideos { get; set; }
        public DbSet<StandingEntry> StandingEntries { get; set; }
        public DbSet<PlayerScorer> PlayerScorers { get; set; }
        public DbSet<TournamentBracket> TournamentBrackets { get; set; }
        public DbSet<MatchStream> MatchStreams { get; set; }
        public DbSet<News> News { get; set; }
        public DbSet<CosmeticItem> CosmeticItems { get; set; }
        public DbSet<UserCosmeticItem> UserCosmeticItems { get; set; }
        public DbSet<MatchFanPass> MatchFanPasses { get; set; }
        public DbSet<CustomTournamentRequest> CustomTournamentRequests { get; set; }
        public DbSet<CheerPhrase> CheerPhrases { get; set; }
        public DbSet<LiveChatMessage> LiveChatMessages { get; set; }

        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerMatchStat> PlayerMatchStats { get; set; }
        public DbSet<FantasyContest> FantasyContests { get; set; }
        public DbSet<FantasyEntry> FantasyEntries { get; set; }
        public DbSet<FantasyEntryPick> FantasyEntryPicks { get; set; }
        public DbSet<WatchPartyRoom> WatchPartyRooms { get; set; }
        public DbSet<WatchPartyMember> WatchPartyMembers { get; set; }
        public DbSet<WatchPartyMessage> WatchPartyMessages { get; set; }
        public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<UserFavoriteTeam> UserFavoriteTeams { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<MatchSummary> MatchSummaries { get; set; }
        public DbSet<OtherSportEvent> OtherSportEvents { get; set; }
        public DbSet<OtherSportParticipant> OtherSportParticipants { get; set; }
        public DbSet<OtherSportResult> OtherSportResults { get; set; }
        public DbSet<OtherSportLiveUpdate> OtherSportLiveUpdates { get; set; }
        public DbSet<OtherSportStream> OtherSportStreams { get; set; }
        public DbSet<OtherSportProfile> OtherSportProfiles { get; set; }
        public DbSet<OtherSportTeamProfile> OtherSportTeamProfiles { get; set; }
        public DbSet<UserStreak> UserStreaks { get; set; }
        public DbSet<AchievementDefinition> AchievementDefinitions { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<DateSyncLog>()
        .HasIndex(d => d.Date)
        .IsUnique();

            builder.Entity<Player>()
                .HasIndex(p => new { p.NormalizedName, p.TeamName })
                .IsUnique()
                .HasFilter("[NormalizedName] IS NOT NULL AND [TeamName] IS NOT NULL");

            builder.Entity<PlayerMatchStat>()
                .HasIndex(s => new { s.PlayerId, s.MatchId })
                .IsUnique();

            builder.Entity<FantasyContest>()
                .HasIndex(c => c.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            builder.Entity<FantasyContest>()
                .HasOne(c => c.Owner)
                .WithMany()
                .HasForeignKey(c => c.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<FantasyEntry>()
                .HasIndex(e => new { e.FantasyContestId, e.UserId })
                .IsUnique();

            builder.Entity<FantasyEntryPick>()
                .HasIndex(p => new { p.FantasyEntryId, p.PlayerId })
                .IsUnique();

            builder.Entity<WatchPartyRoom>()
                .HasIndex(r => r.Code)
                .IsUnique();

            builder.Entity<WatchPartyMember>()
                .HasIndex(m => new { m.WatchPartyRoomId, m.UserId })
                .IsUnique();

            builder.Entity<UserNotificationPreference>()
                .HasIndex(p => p.UserId)
                .IsUnique();

            builder.Entity<UserFavoriteTeam>()
                .HasIndex(f => new { f.UserId, f.TeamId })
                .IsUnique();

            builder.Entity<PushSubscription>()
                .HasIndex(p => new { p.UserId, p.Endpoint })
                .IsUnique();

            builder.Entity<MatchSummary>()
                .HasIndex(s => new { s.MatchId, s.Locale, s.Source })
                .IsUnique();

            builder.Entity<OtherSportEvent>()
                .HasIndex(e => new { e.SportKey, e.ExternalId })
                .IsUnique();

            builder.Entity<OtherSportLiveUpdate>()
                .HasIndex(u => new { u.OtherSportEventId, u.ExternalId })
                .IsUnique()
                .HasFilter("[ExternalId] IS NOT NULL AND [ExternalId] <> ''");

            builder.Entity<OtherSportProfile>()
                .HasIndex(p => new { p.SportKey, p.ExternalId })
                .IsUnique();

            builder.Entity<OtherSportProfile>()
                .HasIndex(p => new { p.SportKey, p.Name });

            builder.Entity<OtherSportTeamProfile>()
                .HasIndex(t => new { t.SportKey, t.ExternalId })
                .IsUnique();

            builder.Entity<OtherSportTeamProfile>()
                .HasIndex(t => new { t.SportKey, t.Name });

            builder.Entity<UserStreak>()
                .HasIndex(s => new { s.UserId, s.Type })
                .IsUnique();

            builder.Entity<AchievementDefinition>()
                .HasIndex(a => a.Code)
                .IsUnique();

            builder.Entity<UserAchievement>()
                .HasIndex(a => new { a.UserId, a.AchievementDefinitionId })
                .IsUnique();


            // 1. سلوك الحذف لطلبات الدفع
            builder.Entity<ManualPaymentRequest>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<RewardedVideoView>()
                .HasIndex(r => new { r.Provider, r.ExternalRewardId })
                .IsUnique();

            builder.Entity<CosmeticItem>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            builder.Entity<UserCosmeticItem>()
                .HasIndex(u => new { u.UserId, u.CosmeticItemId })
                .IsUnique();

            builder.Entity<MatchFanPass>()
                .HasIndex(p => new { p.MatchId, p.UserId })
                .IsUnique();

            builder.Entity<CustomTournamentRequest>()
                .HasOne(r => r.OrganizerUser)
                .WithMany()
                .HasForeignKey(r => r.OrganizerUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<LiveChatMessage>()
                .HasIndex(m => new { m.MatchId, m.CreatedAt });

            builder.Entity<MatchStatistic>()
                .HasIndex(s => new { s.MatchId, s.Name })
                .IsUnique();

            builder.Entity<MatchVideo>()
                .HasIndex(v => new { v.MatchId, v.ExternalId })
                .IsUnique();

            builder.Entity<MatchVideo>()
                .HasIndex(v => new { v.MatchId, v.Type });


            builder.Entity<CheerPhrase>().HasData(
                new CheerPhrase { Id = 1, Text = "يلا يا أبطال!", SortOrder = 1 },
                new CheerPhrase { Id = 2, Text = "الريمونتادا جاية!", SortOrder = 2 },
                new CheerPhrase { Id = 3, Text = "دفاع حديد!", SortOrder = 3 },
                new CheerPhrase { Id = 4, Text = "هدف في الطريق!", SortOrder = 4 }
            );

            builder.Entity<CosmeticItem>().HasData(
                new CosmeticItem { Id = 1, Type = CosmeticItemType.Theme, UnlockType = CosmeticUnlockType.Free, Name = "Classic Qemma", Slug = "classic-qemma", ThemePalette = "Default", IsActive = true },
                new CosmeticItem { Id = 2, Type = CosmeticItemType.Theme, UnlockType = CosmeticUnlockType.Coins, Name = "Derby Night", Slug = "derby-night", ThemePalette = "DerbyNight", PriceCoins = 150m, IsActive = true, IsFeatured = true },
                new CosmeticItem { Id = 3, Type = CosmeticItemType.Emoji, UnlockType = CosmeticUnlockType.Free, Name = "كرة", Slug = "football-free", AssetUrl = "/assets/emojis/football.png", IsActive = true },
                new CosmeticItem { Id = 4, Type = CosmeticItemType.Badge, UnlockType = CosmeticUnlockType.Coins, Name = "مشجع ذهبي", Slug = "gold-supporter", AssetUrl = "/assets/badges/gold-supporter.png", PriceCoins = 250m, IsActive = true, IsFeatured = true },
                new CosmeticItem { Id = 5, Type = CosmeticItemType.Badge, UnlockType = CosmeticUnlockType.Coins, Name = "Supporter", Slug = "supporter-badge", Category = "Supporter", Description = "شارة داعم تظهر بجانب الاسم.", AssetUrl = "/assets/badges/supporter.png", PriceCoins = 99m, IsActive = true, IsFeatured = true },
                new CosmeticItem { Id = 6, Type = CosmeticItemType.Theme, UnlockType = CosmeticUnlockType.Coins, Name = "Red Fans Pack", Slug = "team-pack-red", Category = "TeamPack", Description = "حزمة شكلية لمشجعي الفرق الحمراء.", TeamName = "Red Fans", ThemePalette = "RedFans", PriceCoins = 120m, IsActive = true, IsFeatured = true },
                new CosmeticItem { Id = 7, Type = CosmeticItemType.Theme, UnlockType = CosmeticUnlockType.Coins, Name = "Royal Fans Pack", Slug = "team-pack-royal", Category = "TeamPack", Description = "حزمة شكلية لمشجعي الطابع الملكي.", TeamName = "Royal Fans", ThemePalette = "RoyalFans", PriceCoins = 120m, IsActive = true, IsFeatured = true },
                new CosmeticItem { Id = 8, Type = CosmeticItemType.Frame, UnlockType = CosmeticUnlockType.Coins, Name = "Premium Profile Frame", Slug = "premium-profile-frame", Category = "ProfilePremium", Description = "إطار بروفايل مميز.", AssetUrl = "/assets/frames/premium-profile.png", PriceCoins = 180m, IsActive = true },
                new CosmeticItem { Id = 9, Type = CosmeticItemType.MatchPass, UnlockType = CosmeticUnlockType.Coins, Name = "Match Night Pass", Slug = "match-night-pass", Category = "MatchNight", Description = "تذكار رقمي اختياري لماتش مميز.", AssetUrl = "/assets/badges/match-night.png", PriceCoins = 25m, IsActive = true },
                new CosmeticItem { Id = 10, Type = CosmeticItemType.Badge, UnlockType = CosmeticUnlockType.Coins, Name = "Limited Final Badge", Slug = "limited-final-badge", Category = "LimitedEvent", Description = "شارة محدودة للأحداث الكبرى.", AssetUrl = "/assets/badges/limited-final.png", PriceCoins = 200m, IsLimited = true, IsActive = true }
            );
            builder.Entity<PredictionLeague>()
                .HasIndex(l => l.Code)
                .IsUnique();

            builder.Entity<PredictionLeagueMember>()
                .HasIndex(m => new { m.PredictionLeagueId, m.UserId })
                .IsUnique();

            builder.Entity<PredictionLeague>()
                .HasOne(l => l.Owner)
                .WithMany()
                .HasForeignKey(l => l.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Match>()
                .Property(m => m.MatchDate)
                .HasColumnType("date");

            builder.Entity<News>()
                .Property(n => n.Category)
                .HasMaxLength(120);

            builder.Entity<News>()
                .Property(n => n.SportKey)
                .HasMaxLength(50);

            builder.Entity<News>()
                .Property(n => n.Source)
                .HasMaxLength(80);

            builder.Entity<News>()
                .Property(n => n.Tags)
                .HasMaxLength(500);

            // 2. منع تعارض الحذف بين الفريق الأساسي والمباراة
            builder.Entity<Match>()
                .HasOne(m => m.HomeTeam)
                .WithMany(t => t.HomeMatches)
                .HasForeignKey(m => m.HomeTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // 3. منع تعارض الحذف بين الفريق الضيف والمباراة
            builder.Entity<Match>()
                .HasOne(m => m.AwayTeam)
                .WithMany(t => t.AwayMatches)
                .HasForeignKey(m => m.AwayTeamId)
                .OnDelete(DeleteBehavior.Restrict);

            // === تعديلات حل مشكلة الـ Multiple Cascade Paths مع WatchParty ===

            // WatchPartyRoom.HostUserId -> AspNetUsers (Restrict عشان منعارضش مسار الحذف التاني)
            builder.Entity<WatchPartyRoom>()
                .HasOne(r => r.HostUser)
                .WithMany()
                .HasForeignKey(r => r.HostUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // WatchPartyMember.UserId -> AspNetUsers (Restrict)
            // (العلاقة مع WatchPartyRoom نفسها فاضلة Cascade زي الديفولت، فلما تتحذف الـ Room هيتحذفوا معاها الـ Members تلقائيًا)
            builder.Entity<WatchPartyMember>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // WatchPartyMessage.UserId -> AspNetUsers (Restrict) لنفس السبب
            builder.Entity<WatchPartyMessage>()
                .HasOne(m => m.User)
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}