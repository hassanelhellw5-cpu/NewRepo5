-- Adds free private prediction leagues, knockout mode, and rewarded-video coin credits.
-- Run after the existing base migrations if dotnet-ef is unavailable on the host.

IF COL_LENGTH('Predictions', 'PredictionLeagueId') IS NULL
BEGIN
    ALTER TABLE Predictions ADD PredictionLeagueId int NULL;
END;

IF OBJECT_ID('PredictionLeagues', 'U') IS NULL
BEGIN
    CREATE TABLE PredictionLeagues (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PredictionLeagues PRIMARY KEY,
        Name nvarchar(max) NOT NULL,
        Code nvarchar(450) NOT NULL,
        OwnerUserId nvarchar(450) NOT NULL,
        IsPublic bit NOT NULL,
        CreationFeeCoins decimal(18,2) NOT NULL,
        EntryFeeCoins decimal(18,2) NOT NULL,
        MaxMembers int NOT NULL,
        Format int NOT NULL CONSTRAINT DF_PredictionLeagues_Format DEFAULT 0,
        KnockoutCurrentRound int NOT NULL CONSTRAINT DF_PredictionLeagues_KnockoutCurrentRound DEFAULT 0,
        KnockoutWinnerBonusPoints decimal(18,2) NOT NULL CONSTRAINT DF_PredictionLeagues_KnockoutWinnerBonusPoints DEFAULT 100,
        KnockoutActivatedAt datetime2 NULL,
        KnockoutCompletedAt datetime2 NULL,
        Status int NOT NULL,
        CreatedAt datetime2 NOT NULL,
        StartsAt datetime2 NULL,
        EndsAt datetime2 NULL,
        CONSTRAINT FK_PredictionLeagues_AspNetUsers_OwnerUserId FOREIGN KEY (OwnerUserId) REFERENCES AspNetUsers(Id)
    );
    CREATE UNIQUE INDEX IX_PredictionLeagues_Code ON PredictionLeagues(Code);
    CREATE INDEX IX_PredictionLeagues_OwnerUserId ON PredictionLeagues(OwnerUserId);
END;

IF OBJECT_ID('PredictionLeagueMembers', 'U') IS NULL
BEGIN
    CREATE TABLE PredictionLeagueMembers (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_PredictionLeagueMembers PRIMARY KEY,
        PredictionLeagueId int NOT NULL,
        UserId nvarchar(450) NOT NULL,
        Role int NOT NULL,
        TotalPoints decimal(18,2) NOT NULL,
        KnockoutSeed int NOT NULL CONSTRAINT DF_PredictionLeagueMembers_KnockoutSeed DEFAULT 0,
        KnockoutRound int NOT NULL CONSTRAINT DF_PredictionLeagueMembers_KnockoutRound DEFAULT 0,
        IsKnockoutEliminated bit NOT NULL CONSTRAINT DF_PredictionLeagueMembers_IsKnockoutEliminated DEFAULT 0,
        JoinedAt datetime2 NOT NULL,
        CONSTRAINT FK_PredictionLeagueMembers_PredictionLeagues_PredictionLeagueId FOREIGN KEY (PredictionLeagueId) REFERENCES PredictionLeagues(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PredictionLeagueMembers_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_PredictionLeagueMembers_PredictionLeagueId_UserId ON PredictionLeagueMembers(PredictionLeagueId, UserId);
    CREATE INDEX IX_PredictionLeagueMembers_UserId ON PredictionLeagueMembers(UserId);
END;

IF COL_LENGTH('PredictionLeagues', 'Format') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD Format int NOT NULL CONSTRAINT DF_PredictionLeagues_Format DEFAULT 0;
END;

IF COL_LENGTH('PredictionLeagues', 'KnockoutCurrentRound') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD KnockoutCurrentRound int NOT NULL CONSTRAINT DF_PredictionLeagues_KnockoutCurrentRound DEFAULT 0;
END;

IF COL_LENGTH('PredictionLeagues', 'KnockoutWinnerBonusPoints') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD KnockoutWinnerBonusPoints decimal(18,2) NOT NULL CONSTRAINT DF_PredictionLeagues_KnockoutWinnerBonusPoints DEFAULT 100;
END;

IF COL_LENGTH('PredictionLeagues', 'KnockoutActivatedAt') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD KnockoutActivatedAt datetime2 NULL;
END;

IF COL_LENGTH('PredictionLeagues', 'KnockoutCompletedAt') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD KnockoutCompletedAt datetime2 NULL;
END;

IF COL_LENGTH('PredictionLeagueMembers', 'KnockoutSeed') IS NULL
BEGIN
    ALTER TABLE PredictionLeagueMembers ADD KnockoutSeed int NOT NULL CONSTRAINT DF_PredictionLeagueMembers_KnockoutSeed DEFAULT 0;
END;

IF COL_LENGTH('PredictionLeagueMembers', 'KnockoutRound') IS NULL
BEGIN
    ALTER TABLE PredictionLeagueMembers ADD KnockoutRound int NOT NULL CONSTRAINT DF_PredictionLeagueMembers_KnockoutRound DEFAULT 0;
END;

IF COL_LENGTH('PredictionLeagueMembers', 'IsKnockoutEliminated') IS NULL
BEGIN
    ALTER TABLE PredictionLeagueMembers ADD IsKnockoutEliminated bit NOT NULL CONSTRAINT DF_PredictionLeagueMembers_IsKnockoutEliminated DEFAULT 0;
END;

UPDATE PredictionLeagues SET CreationFeeCoins = 0, EntryFeeCoins = 0;

IF OBJECT_ID('RewardedVideoViews', 'U') IS NULL
BEGIN
    CREATE TABLE RewardedVideoViews (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_RewardedVideoViews PRIMARY KEY,
        UserId nvarchar(450) NOT NULL,
        Provider nvarchar(450) NOT NULL,
        PlacementId nvarchar(max) NOT NULL,
        ExternalRewardId nvarchar(450) NOT NULL,
        CoinsAwarded decimal(18,2) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        CONSTRAINT FK_RewardedVideoViews_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_RewardedVideoViews_Provider_ExternalRewardId ON RewardedVideoViews(Provider, ExternalRewardId);
    CREATE INDEX IX_RewardedVideoViews_UserId ON RewardedVideoViews(UserId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Predictions_PredictionLeagueId' AND object_id = OBJECT_ID('Predictions'))
BEGIN
    CREATE INDEX IX_Predictions_PredictionLeagueId ON Predictions(PredictionLeagueId);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Predictions_PredictionLeagues_PredictionLeagueId')
BEGIN
    ALTER TABLE Predictions ADD CONSTRAINT FK_Predictions_PredictionLeagues_PredictionLeagueId
        FOREIGN KEY (PredictionLeagueId) REFERENCES PredictionLeagues(Id);
END;
