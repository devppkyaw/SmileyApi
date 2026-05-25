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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [ApiKeys] (
        [Id] int NOT NULL IDENTITY,
        [KeyHash] nvarchar(64) NOT NULL,
        [OwnerEmail] nvarchar(256) NOT NULL,
        [Tier] nvarchar(32) NOT NULL,
        [RequestsToday] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [LastResetAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [Businesses] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] nvarchar(32) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [CompanyName] nvarchar(256) NOT NULL,
        [Tier] nvarchar(16) NOT NULL,
        [IsEmailVerified] bit NOT NULL,
        [MagicLinkToken] nvarchar(64) NULL,
        [MagicLinkTokenExpiry] datetime2 NULL,
        [StripeCustomerId] nvarchar(64) NULL,
        [StripeSubscriptionId] nvarchar(64) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [VerifiedAt] datetime2 NULL,
        [TermsAcceptedAt] datetime2 NOT NULL,
        [MarketingConsentGiven] bit NOT NULL,
        [MarketingConsentAt] datetime2 NULL,
        CONSTRAINT [PK_Businesses] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [Establishments] (
        [Id] int NOT NULL IDENTITY,
        [Navnelbnr] int NOT NULL,
        [CvrNumber] nvarchar(20) NULL,
        [Name] nvarchar(512) NOT NULL,
        [Address] nvarchar(512) NULL,
        [PostalCode] nvarchar(10) NULL,
        [City] nvarchar(256) NULL,
        [IndustryCode] nvarchar(32) NULL,
        [IndustryName] nvarchar(256) NULL,
        [GeoLat] float NULL,
        [GeoLng] float NULL,
        [ReportUrl] nvarchar(1024) NULL,
        [LatestScore] int NULL,
        [FirstSeenAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Establishments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [AccessRequests] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(256) NOT NULL,
        [Email] nvarchar(256) NOT NULL,
        [Company] nvarchar(256) NULL,
        [UseCase] nvarchar(2000) NOT NULL,
        [SubmittedAt] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [ApiKeyId] int NULL,
        CONSTRAINT [PK_AccessRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccessRequests_ApiKeys_ApiKeyId] FOREIGN KEY ([ApiKeyId]) REFERENCES [ApiKeys] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [BusinessLocations] (
        [Id] int NOT NULL IDENTITY,
        [BusinessId] int NOT NULL,
        [Navnelbnr] int NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BusinessLocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BusinessLocations_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [Inspections] (
        [Id] int NOT NULL IDENTITY,
        [EstablishmentId] int NOT NULL,
        [SmileyScore] int NOT NULL,
        [InspectedOn] date NOT NULL,
        [RecordedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Inspections] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Inspections_Establishments_EstablishmentId] FOREIGN KEY ([EstablishmentId]) REFERENCES [Establishments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE TABLE [WebhookSubscriptions] (
        [Id] int NOT NULL IDENTITY,
        [ApiKeyId] int NOT NULL,
        [EstablishmentId] int NOT NULL,
        [CallbackUrl] nvarchar(1024) NOT NULL,
        [SecretKey] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WebhookSubscriptions_ApiKeys_ApiKeyId] FOREIGN KEY ([ApiKeyId]) REFERENCES [ApiKeys] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WebhookSubscriptions_Establishments_EstablishmentId] FOREIGN KEY ([EstablishmentId]) REFERENCES [Establishments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccessRequests_ApiKeyId] ON [AccessRequests] ([ApiKeyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Businesses_BusinessId] ON [Businesses] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Businesses_Email] ON [Businesses] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BusinessLocations_BusinessId_Navnelbnr] ON [BusinessLocations] ([BusinessId], [Navnelbnr]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessLocations_Navnelbnr] ON [BusinessLocations] ([Navnelbnr]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Establishments_Navnelbnr] ON [Establishments] ([Navnelbnr]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Inspections_EstablishmentId_InspectedOn] ON [Inspections] ([EstablishmentId], [InspectedOn]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WebhookSubscriptions_ApiKeyId] ON [WebhookSubscriptions] ([ApiKeyId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_WebhookSubscriptions_EstablishmentId] ON [WebhookSubscriptions] ([EstablishmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520205632_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520205632_InitialCreate', N'9.0.16');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] DROP CONSTRAINT [FK_WebhookSubscriptions_ApiKeys_ApiKeyId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    DECLARE @var sysname;
    SELECT @var = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WebhookSubscriptions]') AND [c].[name] = N'ApiKeyId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [WebhookSubscriptions] DROP CONSTRAINT [' + @var + '];');
    ALTER TABLE [WebhookSubscriptions] ALTER COLUMN [ApiKeyId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] ADD [BusinessId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    CREATE INDEX [IX_WebhookSubscriptions_BusinessId] ON [WebhookSubscriptions] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] ADD CONSTRAINT [FK_WebhookSubscriptions_ApiKeys_ApiKeyId] FOREIGN KEY ([ApiKeyId]) REFERENCES [ApiKeys] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    ALTER TABLE [WebhookSubscriptions] ADD CONSTRAINT [FK_WebhookSubscriptions_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521175041_AddBusinessWebhookSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521175041_AddBusinessWebhookSupport', N'9.0.16');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523191739_AddEstablishmentDisplayFields'
)
BEGIN
    ALTER TABLE [Establishments] ADD [LatestScoreDate] date NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523191739_AddEstablishmentDisplayFields'
)
BEGIN
    ALTER TABLE [Establishments] ADD [PNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523191739_AddEstablishmentDisplayFields'
)
BEGIN
    ALTER TABLE [Establishments] ADD [Pixibranche] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523191739_AddEstablishmentDisplayFields'
)
BEGIN
    ALTER TABLE [Establishments] ADD [VirksomhedsType] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523191739_AddEstablishmentDisplayFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523191739_AddEstablishmentDisplayFields', N'9.0.16');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523205639_IndexEstablishmentDisplayFields'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Establishments]') AND [c].[name] = N'VirksomhedsType');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Establishments] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Establishments] ALTER COLUMN [VirksomhedsType] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523205639_IndexEstablishmentDisplayFields'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Establishments]') AND [c].[name] = N'Pixibranche');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Establishments] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Establishments] ALTER COLUMN [Pixibranche] nvarchar(256) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523205639_IndexEstablishmentDisplayFields'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Establishments]') AND [c].[name] = N'PNumber');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Establishments] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Establishments] ALTER COLUMN [PNumber] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523205639_IndexEstablishmentDisplayFields'
)
BEGIN
    CREATE INDEX [IX_Establishments_VirksomhedsType] ON [Establishments] ([VirksomhedsType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523205639_IndexEstablishmentDisplayFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523205639_IndexEstablishmentDisplayFields', N'9.0.16');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525165227_LinkApiKeyToBusiness'
)
BEGIN
    ALTER TABLE [ApiKeys] ADD [BusinessId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525165227_LinkApiKeyToBusiness'
)
BEGIN
    CREATE INDEX [IX_ApiKeys_BusinessId] ON [ApiKeys] ([BusinessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525165227_LinkApiKeyToBusiness'
)
BEGIN
    ALTER TABLE [ApiKeys] ADD CONSTRAINT [FK_ApiKeys_Businesses_BusinessId] FOREIGN KEY ([BusinessId]) REFERENCES [Businesses] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525165227_LinkApiKeyToBusiness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525165227_LinkApiKeyToBusiness', N'9.0.16');
END;
COMMIT;
GO

