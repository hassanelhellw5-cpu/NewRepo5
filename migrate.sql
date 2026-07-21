IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [QemmaCoinsBalance] decimal(18,2) NOT NULL,
        [IsEliteSubscriber] bit NOT NULL,
        [EliteSubscriptionEndDate] datetime2 NULL,
        [CustomThemePalette] nvarchar(max) NOT NULL,
        [ActiveBadgeUrl] nvarchar(max) NOT NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [DateSyncLogs] (
        [Id] int NOT NULL IDENTITY,
        [Date] datetime2 NOT NULL,
        [LastSyncedAt] datetime2 NOT NULL,
        [HasData] bit NOT NULL,
        CONSTRAINT [PK_DateSyncLogs] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Teams] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(max) NOT NULL,
        [LogoUrl] nvarchar(max) NOT NULL,
        [ApiTeamId] int NULL,
        CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Tournaments] (
        [Id] int NOT NULL IDENTITY,
        [YallaKoraId] nvarchar(max) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [LogoUrl] nvarchar(max) NULL,
        [Type] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Tournaments] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetRoleClaims] (
        [Id] int NOT NULL IDENTITY,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserClaims] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [ManualPaymentRequests] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [RequestedCoins] decimal(18,2) NOT NULL,
        [PaymentMethod] nvarchar(max) NOT NULL,
        [PhoneNumberUsed] nvarchar(max) NOT NULL,
        [ReceiptImageUrl] nvarchar(max) NOT NULL,
        [Status] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        [AdminNotes] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_ManualPaymentRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ManualPaymentRequests_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Transactions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [TransactionType] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Notes] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Transactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Transactions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Matches] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] nvarchar(max) NOT NULL,
        [HomeTeamId] int NOT NULL,
        [AwayTeamId] int NOT NULL,
        [MatchDate] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ScoreHome] nvarchar(max) NOT NULL,
        [ScoreAway] nvarchar(max) NOT NULL,
        [Time] nvarchar(max) NOT NULL,
        [TournamentId] int NULL,
        CONSTRAINT [PK_Matches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Matches_Teams_AwayTeamId] FOREIGN KEY ([AwayTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Teams_HomeTeamId] FOREIGN KEY ([HomeTeamId]) REFERENCES [Teams] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Matches_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [PlayerScorers] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [PlayerName] nvarchar(max) NOT NULL,
        [TeamName] nvarchar(max) NOT NULL,
        [Goals] int NOT NULL,
        [Assists] int NOT NULL,
        [Rank] int NOT NULL,
        CONSTRAINT [PK_PlayerScorers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlayerScorers_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Standing] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [TeamId] int NOT NULL,
        [Rank] int NOT NULL,
        [Points] int NOT NULL,
        [Played] int NOT NULL,
        [Won] int NOT NULL,
        [Drawn] int NOT NULL,
        [Lost] int NOT NULL,
        CONSTRAINT [PK_Standing] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Standing_Teams_TeamId] FOREIGN KEY ([TeamId]) REFERENCES [Teams] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Standing_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [StandingEntries] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [GroupName] nvarchar(max) NOT NULL,
        [Rank] int NOT NULL,
        [TeamName] nvarchar(max) NOT NULL,
        [Played] int NOT NULL,
        [Won] int NOT NULL,
        [Drawn] int NOT NULL,
        [Lost] int NOT NULL,
        [GoalsFor] int NOT NULL,
        [GoalsAgainst] int NOT NULL,
        [Points] int NOT NULL,
        CONSTRAINT [PK_StandingEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StandingEntries_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [TournamentBrackets] (
        [Id] int NOT NULL IDENTITY,
        [TournamentId] int NOT NULL,
        [RoundName] nvarchar(max) NOT NULL,
        [TeamHomeName] nvarchar(max) NOT NULL,
        [TeamAwayName] nvarchar(max) NOT NULL,
        [Score] nvarchar(max) NOT NULL,
        [WinnerName] nvarchar(max) NOT NULL,
        [MatchOrder] int NOT NULL,
        CONSTRAINT [PK_TournamentBrackets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TournamentBrackets_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [ArchiveVideos] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(max) NOT NULL,
        [VideoUrl] nvarchar(max) NOT NULL,
        [Quality] nvarchar(max) NOT NULL,
        [MatchId] int NULL,
        [UploadedAt] datetime2 NOT NULL,
        [DownloadsCount] int NOT NULL,
        CONSTRAINT [PK_ArchiveVideos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArchiveVideos_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [MatchEvents] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [Minute] nvarchar(max) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [PlayerName] nvarchar(max) NOT NULL,
        [AssistPlayerName] nvarchar(max) NOT NULL,
        [Detail] nvarchar(max) NOT NULL,
        [TeamSide] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MatchEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchEvents_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [MatchStreams] (
        [Id] int NOT NULL IDENTITY,
        [MatchId] int NOT NULL,
        [StreamUrl] nvarchar(max) NOT NULL,
        [M3U8Url] nvarchar(max) NOT NULL,
        [AltM3U8Url] nvarchar(max) NOT NULL,
        [Source] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_MatchStreams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchStreams_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [News] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ImageUrl] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        [Url] nvarchar(max) NOT NULL,
        [TournamentId] int NULL,
        [MatchId] int NULL,
        CONSTRAINT [PK_News] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_News_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]),
        CONSTRAINT [FK_News_Tournaments_TournamentId] FOREIGN KEY ([TournamentId]) REFERENCES [Tournaments] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE TABLE [Predictions] (
        [Id] int NOT NULL IDENTITY,
        [UserId] nvarchar(450) NOT NULL,
        [MatchId] int NOT NULL,
        [PredictedHomeScore] int NOT NULL,
        [PredictedAwayScore] int NOT NULL,
        [IsProcessed] bit NOT NULL,
        [PointsEarned] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Predictions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Predictions_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Predictions_Matches_MatchId] FOREIGN KEY ([MatchId]) REFERENCES [Matches] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_ArchiveVideos_MatchId] ON [ArchiveVideos] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DateSyncLogs_Date] ON [DateSyncLogs] ([Date]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_ManualPaymentRequests_UserId] ON [ManualPaymentRequests] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_AwayTeamId] ON [Matches] ([AwayTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_HomeTeamId] ON [Matches] ([HomeTeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Matches_TournamentId] ON [Matches] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchEvents_MatchId] ON [MatchEvents] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_MatchStreams_MatchId] ON [MatchStreams] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_News_MatchId] ON [News] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_News_TournamentId] ON [News] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_PlayerScorers_TournamentId] ON [PlayerScorers] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Predictions_MatchId] ON [Predictions] ([MatchId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Predictions_UserId] ON [Predictions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Standing_TeamId] ON [Standing] ([TeamId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Standing_TournamentId] ON [Standing] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_StandingEntries_TournamentId] ON [StandingEntries] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_TournamentBrackets_TournamentId] ON [TournamentBrackets] ([TournamentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    CREATE INDEX [IX_Transactions_UserId] ON [Transactions] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260704172702_IntialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260704172702_IntialCreate', N'8.0.28');
END;
GO

COMMIT;
GO

