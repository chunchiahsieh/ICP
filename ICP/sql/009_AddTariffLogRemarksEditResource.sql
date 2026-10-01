-- Optional alternative to the application's resource scan, for published deployments.
-- Registers the edit resource only. Assign it to the desired roles in Role Permissions.
-- No existing resources or role grants are changed.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Resources', N'U') IS NULL
        THROW 50001, 'dbo.Resources does not exist in the selected database.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM dbo.Resources WITH (UPDLOCK, HOLDLOCK)
        WHERE ResourceCode = N'Views.Broker.TariffData.Edit')
    BEGIN
        INSERT INTO dbo.Resources
            (Id, ParentId, SystemCode, ModuleCode, ResourceCode, ResourceName,
             ResourceType, Route, Icon, Sort, IsVisible, IsEnabled, Description,
             CreateTime, CreateUser)
        VALUES
            (NEWID(), NULL, N'Views', N'Broker', N'Views.Broker.TariffData.Edit',
             N'報關資料：編輯 LOGRemarks', N'Button', N'/TariffData', NULL, 0, 1, 1,
             N'Edit LOGRemarks (maximum 100 characters). Role grants are managed separately.',
             GETDATE(), N'Migration.TariffLogRemarks');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
