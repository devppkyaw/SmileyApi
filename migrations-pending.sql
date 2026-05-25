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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525172415_AddRevokedAtToApiKey'
)
BEGIN
    ALTER TABLE [ApiKeys] ADD [RevokedAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260525172415_AddRevokedAtToApiKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260525172415_AddRevokedAtToApiKey', N'9.0.16');
END;

COMMIT;
GO

