using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace QemmaProject.Migrations
{
    /// <inheritdoc />
    public partial class IntialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AchievementDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TargetCount = table.Column<int>(type: "int", nullable: false),
                    RewardType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RewardCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AchievementDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QemmaCoinsBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsEliteSubscriber = table.Column<bool>(type: "bit", nullable: false),
                    EliteSubscriptionEndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CustomThemePalette = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActiveBadgeUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheerPhrases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Locale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheerPhrases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CosmeticItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UnlockType = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThemePalette = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    IsLimited = table.Column<bool>(type: "bit", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvailableUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PriceCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PriceMoney = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CosmeticItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DateSyncLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HasData = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DateSyncLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PushSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Endpoint = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    P256dh = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Auth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeviceName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApiTeamId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    YallaKoraId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MatchStart = table.Column<bool>(type: "bit", nullable: false),
                    LineupReleased = table.Column<bool>(type: "bit", nullable: false),
                    Goals = table.Column<bool>(type: "bit", nullable: false),
                    Summaries = table.Column<bool>(type: "bit", nullable: false),
                    FantasyPoints = table.Column<bool>(type: "bit", nullable: false),
                    OneHourReminder = table.Column<bool>(type: "bit", nullable: false),
                    PushEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserStreaks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentCount = table.Column<int>(type: "int", nullable: false),
                    BestCount = table.Column<int>(type: "int", nullable: false),
                    LastActivityDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserStreaks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserAchievements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AchievementDefinitionId = table.Column<int>(type: "int", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAchievements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAchievements_AchievementDefinitions_AchievementDefinitionId",
                        column: x => x.AchievementDefinitionId,
                        principalTable: "AchievementDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomTournamentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CommunityName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContactInfo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestedFeatures = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstimatedBudgetCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomTournamentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomTournamentRequests_AspNetUsers_OrganizerUserId",
                        column: x => x.OrganizerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ManualPaymentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequestedCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PhoneNumberUsed = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceiptImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AdminNotes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPaymentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ManualPaymentRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionLeagues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreationFeeCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EntryFeeCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxMembers = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    KnockoutCurrentRound = table.Column<int>(type: "int", nullable: false),
                    KnockoutWinnerBonusPoints = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KnockoutActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KnockoutCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThemePalette = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TrophyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PremiumAnalyticsUnlocked = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionLeagues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionLeagues_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RewardedVideoViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlacementId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalRewardId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CoinsAwarded = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardedVideoViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RewardedVideoViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCosmeticItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CosmeticItemId = table.Column<int>(type: "int", nullable: false),
                    IsEquipped = table.Column<bool>(type: "bit", nullable: false),
                    UnlockedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCosmeticItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCosmeticItems_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCosmeticItems_CosmeticItems_CosmeticItemId",
                        column: x => x.CosmeticItemId,
                        principalTable: "CosmeticItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    TeamId = table.Column<int>(type: "int", nullable: true),
                    TeamName = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShirtNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImageSource = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExternalPlayerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Players_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserFavoriteTeams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavoriteTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFavoriteTeams_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyContests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    ContestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    MaxMembers = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    KnockoutCurrentRound = table.Column<int>(type: "int", nullable: false),
                    KnockoutWinnerBonusPoints = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KnockoutActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KnockoutCompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsOpen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyContests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyContests_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FantasyContests_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Matches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeTeamId = table.Column<int>(type: "int", nullable: false),
                    AwayTeamId = table.Column<int>(type: "int", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoreHome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScoreAway = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Time = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TournamentId = table.Column<int>(type: "int", nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeFormation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AwayFormation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HomeCoach = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AwayCoach = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Matches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_AwayTeamId",
                        column: x => x.AwayTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Teams_HomeTeamId",
                        column: x => x.HomeTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Matches_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerScorers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerScorers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerScorers_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Standing",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    Played = table.Column<int>(type: "int", nullable: false),
                    Won = table.Column<int>(type: "int", nullable: false),
                    Drawn = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Standing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Standing_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Standing_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StandingEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    GroupName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    TeamName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Played = table.Column<int>(type: "int", nullable: false),
                    Won = table.Column<int>(type: "int", nullable: false),
                    Drawn = table.Column<int>(type: "int", nullable: false),
                    Lost = table.Column<int>(type: "int", nullable: false),
                    GoalsFor = table.Column<int>(type: "int", nullable: false),
                    GoalsAgainst = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingEntries_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TournamentBrackets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<int>(type: "int", nullable: false),
                    RoundName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamHomeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamAwayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Score = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WinnerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentBrackets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TournamentBrackets_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionLeagueMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PredictionLeagueId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KnockoutSeed = table.Column<int>(type: "int", nullable: false),
                    KnockoutRound = table.Column<int>(type: "int", nullable: false),
                    IsKnockoutEliminated = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionLeagueMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionLeagueMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PredictionLeagueMembers_PredictionLeagues_PredictionLeagueId",
                        column: x => x.PredictionLeagueId,
                        principalTable: "PredictionLeagues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FantasyContestId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<int>(type: "int", nullable: false),
                    KnockoutSeed = table.Column<int>(type: "int", nullable: false),
                    KnockoutRound = table.Column<int>(type: "int", nullable: false),
                    IsKnockoutEliminated = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyEntries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FantasyEntries_FantasyContests_FantasyContestId",
                        column: x => x.FantasyContestId,
                        principalTable: "FantasyContests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArchiveVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quality = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DownloadsCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveVideos_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LiveChatMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheerPhraseId = table.Column<int>(type: "int", nullable: true),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    PaidCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PinnedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveChatMessages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LiveChatMessages_CheerPhrases_CheerPhraseId",
                        column: x => x.CheerPhraseId,
                        principalTable: "CheerPhrases",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LiveChatMessages_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Minute = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssistPlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Detail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TeamSide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchEvents_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchFanPasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BadgeText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaidCoins = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchFanPasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchFanPasses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MatchFanPasses_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchLineups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    TeamSide = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Position = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSubstitute = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLineups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchLineups_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HomeValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AwayValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStatistics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchStatistics_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    StreamUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    home_team = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    away_team = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    M3U8Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AltM3U8Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchStreams_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HighlightsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSummaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchSummaries_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MatchVideos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmbedUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchVideos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchVideos_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "News",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TournamentId = table.Column<int>(type: "int", nullable: true),
                    MatchId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_News", x => x.Id);
                    table.ForeignKey(
                        name: "FK_News_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_News_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PlayerMatchStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    Goals = table.Column<int>(type: "int", nullable: false),
                    Assists = table.Column<int>(type: "int", nullable: false),
                    YellowCards = table.Column<int>(type: "int", nullable: false),
                    RedCards = table.Column<int>(type: "int", nullable: false),
                    MinutesPlayed = table.Column<int>(type: "int", nullable: false),
                    Shots = table.Column<int>(type: "int", nullable: false),
                    KeyPasses = table.Column<int>(type: "int", nullable: false),
                    Saves = table.Column<int>(type: "int", nullable: false),
                    PenaltiesScored = table.Column<int>(type: "int", nullable: false),
                    PenaltiesMissed = table.Column<int>(type: "int", nullable: false),
                    CleanSheet = table.Column<bool>(type: "bit", nullable: false),
                    Started = table.Column<bool>(type: "bit", nullable: false),
                    Substitute = table.Column<bool>(type: "bit", nullable: false),
                    FantasyPoints = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMatchStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerMatchStats_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerMatchStats_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    PredictionLeagueId = table.Column<int>(type: "int", nullable: true),
                    PredictedHomeScore = table.Column<int>(type: "int", nullable: false),
                    PredictedAwayScore = table.Column<int>(type: "int", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    PointsEarned = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Predictions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Predictions_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Predictions_PredictionLeagues_PredictionLeagueId",
                        column: x => x.PredictionLeagueId,
                        principalTable: "PredictionLeagues",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WatchPartyRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MatchId = table.Column<int>(type: "int", nullable: false),
                    HostUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchPartyRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchPartyRooms_AspNetUsers_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchPartyRooms_Matches_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Matches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FantasyEntryPicks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FantasyEntryId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FantasyEntryPicks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FantasyEntryPicks_FantasyEntries_FantasyEntryId",
                        column: x => x.FantasyEntryId,
                        principalTable: "FantasyEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FantasyEntryPicks_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchPartyMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WatchPartyRoomId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsHost = table.Column<bool>(type: "bit", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchPartyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchPartyMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchPartyMembers_WatchPartyRooms_WatchPartyRoomId",
                        column: x => x.WatchPartyRoomId,
                        principalTable: "WatchPartyRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WatchPartyMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WatchPartyRoomId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reaction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Prediction = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchPartyMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchPartyMessages_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WatchPartyMessages_WatchPartyRooms_WatchPartyRoomId",
                        column: x => x.WatchPartyRoomId,
                        principalTable: "WatchPartyRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "CheerPhrases",
                columns: new[] { "Id", "IsActive", "Locale", "SortOrder", "TeamName", "Text" },
                values: new object[,]
                {
                    { 1, true, "ar-EG", 1, null, "يلا يا أبطال!" },
                    { 2, true, "ar-EG", 2, null, "الريمونتادا جاية!" },
                    { 3, true, "ar-EG", 3, null, "دفاع حديد!" },
                    { 4, true, "ar-EG", 4, null, "هدف في الطريق!" }
                });

            migrationBuilder.InsertData(
                table: "CosmeticItems",
                columns: new[] { "Id", "AssetUrl", "AvailableFrom", "AvailableUntil", "Category", "CreatedAt", "Currency", "Description", "IsActive", "IsFeatured", "IsLimited", "MatchId", "Name", "PlayerName", "PriceCoins", "PriceMoney", "Slug", "TeamName", "ThemePalette", "Type", "UnlockType" },
                values: new object[,]
                {
                    { 1, null, null, null, null, new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6387), "EGP", null, true, false, false, null, "Classic Qemma", null, 0m, 0m, "classic-qemma", null, "Default", 0, 0 },
                    { 2, null, null, null, null, new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6396), "EGP", null, true, true, false, null, "Derby Night", null, 150m, 0m, "derby-night", null, "DerbyNight", 0, 1 },
                    { 3, "/assets/emojis/football.png", null, null, null, new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6402), "EGP", null, true, false, false, null, "كرة", null, 0m, 0m, "football-free", null, null, 1, 0 },
                    { 4, "/assets/badges/gold-supporter.png", null, null, null, new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6404), "EGP", null, true, true, false, null, "مشجع ذهبي", null, 250m, 0m, "gold-supporter", null, null, 2, 1 },
                    { 5, "/assets/badges/supporter.png", null, null, "Supporter", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6406), "EGP", "شارة داعم تظهر بجانب الاسم.", true, true, false, null, "Supporter", null, 99m, 0m, "supporter-badge", null, null, 2, 1 },
                    { 6, null, null, null, "TeamPack", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6409), "EGP", "حزمة شكلية لمشجعي الفرق الحمراء.", true, true, false, null, "Red Fans Pack", null, 120m, 0m, "team-pack-red", "Red Fans", "RedFans", 0, 1 },
                    { 7, null, null, null, "TeamPack", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6418), "EGP", "حزمة شكلية لمشجعي الطابع الملكي.", true, true, false, null, "Royal Fans Pack", null, 120m, 0m, "team-pack-royal", "Royal Fans", "RoyalFans", 0, 1 },
                    { 8, "/assets/frames/premium-profile.png", null, null, "ProfilePremium", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6420), "EGP", "إطار بروفايل مميز.", true, false, false, null, "Premium Profile Frame", null, 180m, 0m, "premium-profile-frame", null, null, 3, 1 },
                    { 9, "/assets/badges/match-night.png", null, null, "MatchNight", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6423), "EGP", "تذكار رقمي اختياري لماتش مميز.", true, false, false, null, "Match Night Pass", null, 25m, 0m, "match-night-pass", null, null, 5, 1 },
                    { 10, "/assets/badges/limited-final.png", null, null, "LimitedEvent", new DateTime(2026, 7, 9, 18, 2, 17, 993, DateTimeKind.Utc).AddTicks(6425), "EGP", "شارة محدودة للأحداث الكبرى.", true, false, true, null, "Limited Final Badge", null, 200m, 0m, "limited-final-badge", null, null, 2, 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AchievementDefinitions_Code",
                table: "AchievementDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveVideos_MatchId",
                table: "ArchiveVideos",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CosmeticItems_Slug",
                table: "CosmeticItems",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomTournamentRequests_OrganizerUserId",
                table: "CustomTournamentRequests",
                column: "OrganizerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DateSyncLogs_Date",
                table: "DateSyncLogs",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FantasyContests_Code",
                table: "FantasyContests",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyContests_OwnerUserId",
                table: "FantasyContests",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyContests_TournamentId",
                table: "FantasyContests",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntries_FantasyContestId_UserId",
                table: "FantasyEntries",
                columns: new[] { "FantasyContestId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntries_UserId",
                table: "FantasyEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntryPicks_FantasyEntryId_PlayerId",
                table: "FantasyEntryPicks",
                columns: new[] { "FantasyEntryId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FantasyEntryPicks_PlayerId",
                table: "FantasyEntryPicks",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatMessages_CheerPhraseId",
                table: "LiveChatMessages",
                column: "CheerPhraseId");

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatMessages_MatchId_CreatedAt",
                table: "LiveChatMessages",
                columns: new[] { "MatchId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LiveChatMessages_UserId",
                table: "LiveChatMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ManualPaymentRequests_UserId",
                table: "ManualPaymentRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_AwayTeamId",
                table: "Matches",
                column: "AwayTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_HomeTeamId",
                table: "Matches",
                column: "HomeTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Matches_TournamentId",
                table: "Matches",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchEvents_MatchId",
                table: "MatchEvents",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchFanPasses_MatchId_UserId",
                table: "MatchFanPasses",
                columns: new[] { "MatchId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchFanPasses_UserId",
                table: "MatchFanPasses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLineups_MatchId",
                table: "MatchLineups",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchStatistics_MatchId_Name",
                table: "MatchStatistics",
                columns: new[] { "MatchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchStreams_MatchId",
                table: "MatchStreams",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchSummaries_MatchId_Locale_Source",
                table: "MatchSummaries",
                columns: new[] { "MatchId", "Locale", "Source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchVideos_MatchId_ExternalId",
                table: "MatchVideos",
                columns: new[] { "MatchId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchVideos_MatchId_Type",
                table: "MatchVideos",
                columns: new[] { "MatchId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_News_MatchId",
                table: "News",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_News_TournamentId",
                table: "News",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStats_MatchId",
                table: "PlayerMatchStats",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMatchStats_PlayerId_MatchId",
                table: "PlayerMatchStats",
                columns: new[] { "PlayerId", "MatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Players_NormalizedName_TeamName",
                table: "Players",
                columns: new[] { "NormalizedName", "TeamName" },
                unique: true,
                filter: "[NormalizedName] IS NOT NULL AND [TeamName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Players_TeamId",
                table: "Players",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerScorers_TournamentId",
                table: "PlayerScorers",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLeagueMembers_PredictionLeagueId_UserId",
                table: "PredictionLeagueMembers",
                columns: new[] { "PredictionLeagueId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLeagueMembers_UserId",
                table: "PredictionLeagueMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLeagues_Code",
                table: "PredictionLeagues",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionLeagues_OwnerUserId",
                table: "PredictionLeagues",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_MatchId",
                table: "Predictions",
                column: "MatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_PredictionLeagueId",
                table: "Predictions",
                column: "PredictionLeagueId");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_UserId_Endpoint",
                table: "PushSubscriptions",
                columns: new[] { "UserId", "Endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardedVideoViews_Provider_ExternalRewardId",
                table: "RewardedVideoViews",
                columns: new[] { "Provider", "ExternalRewardId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RewardedVideoViews_UserId",
                table: "RewardedVideoViews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Standing_TeamId",
                table: "Standing",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Standing_TournamentId",
                table: "Standing",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingEntries_TournamentId",
                table: "StandingEntries",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentBrackets_TournamentId",
                table: "TournamentBrackets",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_UserId",
                table: "Transactions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_AchievementDefinitionId",
                table: "UserAchievements",
                column: "AchievementDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserAchievements_UserId_AchievementDefinitionId",
                table: "UserAchievements",
                columns: new[] { "UserId", "AchievementDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserCosmeticItems_CosmeticItemId",
                table: "UserCosmeticItems",
                column: "CosmeticItemId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCosmeticItems_UserId_CosmeticItemId",
                table: "UserCosmeticItems",
                columns: new[] { "UserId", "CosmeticItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteTeams_TeamId",
                table: "UserFavoriteTeams",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteTeams_UserId_TeamId",
                table: "UserFavoriteTeams",
                columns: new[] { "UserId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationPreferences_UserId",
                table: "UserNotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserStreaks_UserId_Type",
                table: "UserStreaks",
                columns: new[] { "UserId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyMembers_UserId",
                table: "WatchPartyMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyMembers_WatchPartyRoomId_UserId",
                table: "WatchPartyMembers",
                columns: new[] { "WatchPartyRoomId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyMessages_UserId",
                table: "WatchPartyMessages",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyMessages_WatchPartyRoomId",
                table: "WatchPartyMessages",
                column: "WatchPartyRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyRooms_Code",
                table: "WatchPartyRooms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyRooms_HostUserId",
                table: "WatchPartyRooms",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WatchPartyRooms_MatchId",
                table: "WatchPartyRooms",
                column: "MatchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveVideos");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "CustomTournamentRequests");

            migrationBuilder.DropTable(
                name: "DateSyncLogs");

            migrationBuilder.DropTable(
                name: "FantasyEntryPicks");

            migrationBuilder.DropTable(
                name: "LiveChatMessages");

            migrationBuilder.DropTable(
                name: "ManualPaymentRequests");

            migrationBuilder.DropTable(
                name: "MatchEvents");

            migrationBuilder.DropTable(
                name: "MatchFanPasses");

            migrationBuilder.DropTable(
                name: "MatchLineups");

            migrationBuilder.DropTable(
                name: "MatchStatistics");

            migrationBuilder.DropTable(
                name: "MatchStreams");

            migrationBuilder.DropTable(
                name: "MatchSummaries");

            migrationBuilder.DropTable(
                name: "MatchVideos");

            migrationBuilder.DropTable(
                name: "News");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PlayerMatchStats");

            migrationBuilder.DropTable(
                name: "PlayerScorers");

            migrationBuilder.DropTable(
                name: "PredictionLeagueMembers");

            migrationBuilder.DropTable(
                name: "Predictions");

            migrationBuilder.DropTable(
                name: "PushSubscriptions");

            migrationBuilder.DropTable(
                name: "RewardedVideoViews");

            migrationBuilder.DropTable(
                name: "Standing");

            migrationBuilder.DropTable(
                name: "StandingEntries");

            migrationBuilder.DropTable(
                name: "TournamentBrackets");

            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropTable(
                name: "UserAchievements");

            migrationBuilder.DropTable(
                name: "UserCosmeticItems");

            migrationBuilder.DropTable(
                name: "UserFavoriteTeams");

            migrationBuilder.DropTable(
                name: "UserNotificationPreferences");

            migrationBuilder.DropTable(
                name: "UserStreaks");

            migrationBuilder.DropTable(
                name: "WatchPartyMembers");

            migrationBuilder.DropTable(
                name: "WatchPartyMessages");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "FantasyEntries");

            migrationBuilder.DropTable(
                name: "CheerPhrases");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "PredictionLeagues");

            migrationBuilder.DropTable(
                name: "AchievementDefinitions");

            migrationBuilder.DropTable(
                name: "CosmeticItems");

            migrationBuilder.DropTable(
                name: "WatchPartyRooms");

            migrationBuilder.DropTable(
                name: "FantasyContests");

            migrationBuilder.DropTable(
                name: "Matches");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
