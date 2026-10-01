-- Run against the ICP application database before deploying the updated application.
-- Existing records remain unchanged; their LOGRemarks starts as NULL.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TableId INT = OBJECT_ID(N'dbo.TariffData', N'U');
    IF @TableId IS NULL
        THROW 50001, 'dbo.TariffData does not exist in the selected database.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @TableId AND name = N'LOGRemarks')
        ALTER TABLE dbo.TariffData ADD LOGRemarks NVARCHAR(100) NULL;
    ELSE IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE object_id = @TableId AND name = N'LOGRemarks'
          AND (system_type_id <> TYPE_ID(N'nvarchar')
               OR (max_length <> -1 AND max_length < 200)
               OR is_nullable = 0))
        THROW 50002, 'Existing LOGRemarks must be nullable nvarchar(100) or wider; review the schema.', 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
