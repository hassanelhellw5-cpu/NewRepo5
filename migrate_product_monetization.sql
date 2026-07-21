-- Adds non-intrusive monetization features: supporter cosmetics, team packs,
-- profile premium cosmetics, match fan passes, league customization,
-- premium league analytics, limited event cosmetics, and custom tournament requests.

IF COL_LENGTH('CosmeticItems', 'Category') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD Category nvarchar(max) NULL;
END;

IF COL_LENGTH('CosmeticItems', 'Description') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD Description nvarchar(max) NULL;
END;

IF COL_LENGTH('CosmeticItems', 'MatchId') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD MatchId int NULL;
END;

IF COL_LENGTH('CosmeticItems', 'IsLimited') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD IsLimited bit NOT NULL CONSTRAINT DF_CosmeticItems_IsLimited DEFAULT 0;
END;

IF COL_LENGTH('CosmeticItems', 'AvailableFrom') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD AvailableFrom datetime2 NULL;
END;

IF COL_LENGTH('CosmeticItems', 'AvailableUntil') IS NULL
BEGIN
    ALTER TABLE CosmeticItems ADD AvailableUntil datetime2 NULL;
END;

IF COL_LENGTH('PredictionLeagues', 'ThemePalette') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD ThemePalette nvarchar(max) NULL;
END;

IF COL_LENGTH('PredictionLeagues', 'CoverImageUrl') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD CoverImageUrl nvarchar(max) NULL;
END;

IF COL_LENGTH('PredictionLeagues', 'TrophyName') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD TrophyName nvarchar(max) NULL;
END;

IF COL_LENGTH('PredictionLeagues', 'PremiumAnalyticsUnlocked') IS NULL
BEGIN
    ALTER TABLE PredictionLeagues ADD PremiumAnalyticsUnlocked bit NOT NULL CONSTRAINT DF_PredictionLeagues_PremiumAnalyticsUnlocked DEFAULT 0;
END;

IF OBJECT_ID('MatchFanPasses', 'U') IS NULL
BEGIN
    CREATE TABLE MatchFanPasses (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MatchFanPasses PRIMARY KEY,
        MatchId int NOT NULL,
        UserId nvarchar(450) NOT NULL,
        Title nvarchar(max) NOT NULL,
        BadgeText nvarchar(max) NOT NULL,
        PaidCoins decimal(18,2) NOT NULL,
        PurchasedAt datetime2 NOT NULL,
        CONSTRAINT FK_MatchFanPasses_Matches_MatchId FOREIGN KEY (MatchId) REFERENCES Matches(Id) ON DELETE CASCADE,
        CONSTRAINT FK_MatchFanPasses_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_MatchFanPasses_MatchId_UserId ON MatchFanPasses(MatchId, UserId);
    CREATE INDEX IX_MatchFanPasses_UserId ON MatchFanPasses(UserId);
END;

IF OBJECT_ID('CustomTournamentRequests', 'U') IS NULL
BEGIN
    CREATE TABLE CustomTournamentRequests (
        Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomTournamentRequests PRIMARY KEY,
        OrganizerUserId nvarchar(450) NOT NULL,
        Name nvarchar(max) NOT NULL,
        CommunityName nvarchar(max) NOT NULL,
        ContactInfo nvarchar(max) NOT NULL,
        RequestedFeatures nvarchar(max) NOT NULL,
        EstimatedBudgetCoins decimal(18,2) NOT NULL,
        Status nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL,
        CONSTRAINT FK_CustomTournamentRequests_AspNetUsers_OrganizerUserId FOREIGN KEY (OrganizerUserId) REFERENCES AspNetUsers(Id) ON DELETE CASCADE
    );
    CREATE INDEX IX_CustomTournamentRequests_OrganizerUserId ON CustomTournamentRequests(OrganizerUserId);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'supporter-badge')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, AssetUrl, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (2, 1, N'Supporter', 'supporter-badge', 'Supporter', N'شارة داعم تظهر بجانب الاسم.', '/assets/badges/supporter.png', 99, 0, 'EGP', 1, 1, SYSUTCDATETIME(), 0);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'team-pack-red')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, TeamName, ThemePalette, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (0, 1, N'Red Fans Pack', 'team-pack-red', 'TeamPack', N'حزمة شكلية لمشجعي الفرق الحمراء.', N'Red Fans', 'RedFans', 120, 0, 'EGP', 1, 1, SYSUTCDATETIME(), 0);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'team-pack-royal')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, TeamName, ThemePalette, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (0, 1, N'Royal Fans Pack', 'team-pack-royal', 'TeamPack', N'حزمة شكلية لمشجعي الطابع الملكي.', N'Royal Fans', 'RoyalFans', 120, 0, 'EGP', 1, 1, SYSUTCDATETIME(), 0);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'premium-profile-frame')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, AssetUrl, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (3, 1, N'Premium Profile Frame', 'premium-profile-frame', 'ProfilePremium', N'إطار بروفايل مميز.', '/assets/frames/premium-profile.png', 180, 0, 'EGP', 1, 0, SYSUTCDATETIME(), 0);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'match-night-pass')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, AssetUrl, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (5, 1, N'Match Night Pass', 'match-night-pass', 'MatchNight', N'تذكار رقمي اختياري لماتش مميز.', '/assets/badges/match-night.png', 25, 0, 'EGP', 1, 0, SYSUTCDATETIME(), 0);
END;

IF NOT EXISTS (SELECT 1 FROM CosmeticItems WHERE Slug = 'limited-final-badge')
BEGIN
    INSERT INTO CosmeticItems (Type, UnlockType, Name, Slug, Category, Description, AssetUrl, PriceCoins, PriceMoney, Currency, IsActive, IsFeatured, CreatedAt, IsLimited)
    VALUES (2, 1, N'Limited Final Badge', 'limited-final-badge', 'LimitedEvent', N'شارة محدودة للأحداث الكبرى.', '/assets/badges/limited-final.png', 200, 0, 'EGP', 1, 0, SYSUTCDATETIME(), 1);
END;
