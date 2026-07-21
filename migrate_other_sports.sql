IF OBJECT_ID(N'[OtherSportEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportEvents] (
        [Id] int NOT NULL IDENTITY,
        [SportKey] nvarchar(450) NOT NULL,
        [ExternalId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [CompetitionName] nvarchar(max) NOT NULL,
        [EventDate] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Time] nvarchar(max) NOT NULL,
        [Venue] nvarchar(max) NOT NULL,
        [Country] nvarchar(max) NOT NULL,
        [Source] nvarchar(max) NOT NULL,
        [SourceUrl] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OtherSportEvents] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_OtherSportEvents_SportKey_ExternalId] ON [OtherSportEvents] ([SportKey], [ExternalId]);
END;

IF OBJECT_ID(N'[OtherSportParticipants]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportParticipants] (
        [Id] int NOT NULL IDENTITY,
        [OtherSportEventId] int NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [TeamName] nvarchar(max) NOT NULL,
        [Country] nvarchar(max) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [SeedOrNumber] int NULL,
        [MetadataJson] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_OtherSportParticipants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OtherSportParticipants_OtherSportEvents_OtherSportEventId] FOREIGN KEY ([OtherSportEventId]) REFERENCES [OtherSportEvents] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_OtherSportParticipants_OtherSportEventId] ON [OtherSportParticipants] ([OtherSportEventId]);
END;

IF OBJECT_ID(N'[OtherSportResults]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportResults] (
        [Id] int NOT NULL IDENTITY,
        [OtherSportEventId] int NOT NULL,
        [ParticipantName] nvarchar(max) NOT NULL,
        [Rank] int NULL,
        [Score] nvarchar(max) NOT NULL,
        [ResultText] nvarchar(max) NOT NULL,
        [MetadataJson] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OtherSportResults] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OtherSportResults_OtherSportEvents_OtherSportEventId] FOREIGN KEY ([OtherSportEventId]) REFERENCES [OtherSportEvents] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_OtherSportResults_OtherSportEventId] ON [OtherSportResults] ([OtherSportEventId]);
END;

IF OBJECT_ID(N'[OtherSportLiveUpdates]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportLiveUpdates] (
        [Id] int NOT NULL IDENTITY,
        [OtherSportEventId] int NOT NULL,
        [ExternalId] nvarchar(450) NOT NULL,
        [MinuteOrLap] nvarchar(max) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Detail] nvarchar(max) NOT NULL,
        [PublishedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OtherSportLiveUpdates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OtherSportLiveUpdates_OtherSportEvents_OtherSportEventId] FOREIGN KEY ([OtherSportEventId]) REFERENCES [OtherSportEvents] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_OtherSportLiveUpdates_OtherSportEventId] ON [OtherSportLiveUpdates] ([OtherSportEventId]);
    CREATE UNIQUE INDEX [IX_OtherSportLiveUpdates_OtherSportEventId_ExternalId] ON [OtherSportLiveUpdates] ([OtherSportEventId], [ExternalId]) WHERE [ExternalId] IS NOT NULL AND [ExternalId] <> '';
END;

IF OBJECT_ID(N'[OtherSportStreams]', N'U') IS NULL
BEGIN
    CREATE TABLE [OtherSportStreams] (
        [Id] int NOT NULL IDENTITY,
        [OtherSportEventId] int NOT NULL,
        [Source] nvarchar(max) NOT NULL,
        [StreamUrl] nvarchar(max) NOT NULL,
        [M3U8Url] nvarchar(max) NOT NULL,
        [StatusMessage] nvarchar(max) NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_OtherSportStreams] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OtherSportStreams_OtherSportEvents_OtherSportEventId] FOREIGN KEY ([OtherSportEventId]) REFERENCES [OtherSportEvents] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_OtherSportStreams_OtherSportEventId] ON [OtherSportStreams] ([OtherSportEventId]);
END;
