-- Run against the ICP application database before deploying the updated application.
-- Expands the existing column only; does not truncate data or narrow wider columns.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TableId INT = OBJECT_ID(N'dbo.TariffData', N'U');
    IF @TableId IS NULL
        THROW 50001, 'dbo.TariffData does not exist in the selected database.', 1;

    DECLARE @TypeId INT;
    DECLARE @MaxLength SMALLINT;
    DECLARE @IsNullable BIT;

    SELECT @TypeId = system_type_id,
           @MaxLength = max_length,
           @IsNullable = is_nullable
    FROM sys.columns
    WHERE object_id = @TableId AND name = N'DescriptionOfGoods';

    IF @TypeId IS NULL
        THROW 50002, 'dbo.TariffData.DescriptionOfGoods does not exist.', 1;

    IF @TypeId <> TYPE_ID(N'nvarchar')
        THROW 50003, 'Expected DescriptionOfGoods to be nvarchar; review the database schema before proceeding.', 1;

    -- sys.columns.max_length is measured in bytes; nvarchar(500) uses 1000 bytes.
    -- -1 represents nvarchar(max), which must not be narrowed by this script.
    IF @MaxLength <> -1 AND @MaxLength < 1000
    BEGIN
        IF @IsNullable = 1
            ALTER TABLE dbo.TariffData ALTER COLUMN DescriptionOfGoods NVARCHAR(500) NULL;
        ELSE
            ALTER TABLE dbo.TariffData ALTER COLUMN DescriptionOfGoods NVARCHAR(500) NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
