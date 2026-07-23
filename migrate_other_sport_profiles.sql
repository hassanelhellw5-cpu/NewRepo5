IF OBJECT_ID(N'[OtherSportProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportProfiles] (
        [Id] int NOT NULL IDENTITY,
        [SportKey] nvarchar(64) NOT NULL DEFAULT N'',
        [ExternalId] nvarchar(128) NOT NULL DEFAULT N'',
        [Name] nvarchar(256) NOT NULL DEFAULT N'',
        [TeamName] nvarchar(256) NOT NULL DEFAULT N'',
        [Country] nvarchar(128) NOT NULL DEFAULT N'',
        [Role] nvarchar(64) NOT NULL DEFAULT N'',
        [EventsCount] int NOT NULL DEFAULT 0,
        [WinsOrFirstPlaces] int NOT NULL DEFAULT 0,
        [Points] decimal(18,2) NOT NULL DEFAULT 0,
        [ImageUrl] nvarchar(1024) NOT NULL DEFAULT N'',
        [Source] nvarchar(128) NOT NULL DEFAULT N'',
        [SourceUrl] nvarchar(1024) NOT NULL DEFAULT N'',
        [MetadataJson] nvarchar(max) NOT NULL DEFAULT N'{}',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_OtherSportProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OtherSportProfiles_SportKey_ExternalId' AND object_id = OBJECT_ID(N'[OtherSportProfiles]'))
    CREATE UNIQUE INDEX [IX_OtherSportProfiles_SportKey_ExternalId] ON [OtherSportProfiles] ([SportKey], [ExternalId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OtherSportProfiles_SportKey_Name' AND object_id = OBJECT_ID(N'[OtherSportProfiles]'))
    CREATE INDEX [IX_OtherSportProfiles_SportKey_Name] ON [OtherSportProfiles] ([SportKey], [Name]);

IF OBJECT_ID(N'[OtherSportTeamProfiles]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportTeamProfiles] (
        [Id] int NOT NULL IDENTITY,
        [SportKey] nvarchar(64) NOT NULL DEFAULT N'',
        [ExternalId] nvarchar(128) NOT NULL DEFAULT N'',
        [Name] nvarchar(256) NOT NULL DEFAULT N'',
        [Country] nvarchar(128) NOT NULL DEFAULT N'',
        [EventsCount] int NOT NULL DEFAULT 0,
        [WinsOrFirstPlaces] int NOT NULL DEFAULT 0,
        [Points] decimal(18,2) NOT NULL DEFAULT 0,
        [LogoUrl] nvarchar(1024) NOT NULL DEFAULT N'',
        [Source] nvarchar(128) NOT NULL DEFAULT N'',
        [SourceUrl] nvarchar(1024) NOT NULL DEFAULT N'',
        [MetadataJson] nvarchar(max) NOT NULL DEFAULT N'{}',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_OtherSportTeamProfiles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OtherSportTeamProfiles_SportKey_ExternalId' AND object_id = OBJECT_ID(N'[OtherSportTeamProfiles]'))
    CREATE UNIQUE INDEX [IX_OtherSportTeamProfiles_SportKey_ExternalId] ON [OtherSportTeamProfiles] ([SportKey], [ExternalId]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OtherSportTeamProfiles_SportKey_Name' AND object_id = OBJECT_ID(N'[OtherSportTeamProfiles]'))
    CREATE INDEX [IX_OtherSportTeamProfiles_SportKey_Name] ON [OtherSportTeamProfiles] ([SportKey], [Name]);
