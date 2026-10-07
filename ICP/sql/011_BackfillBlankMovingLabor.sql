-- Run against the ICP application database after 010_SetIcpHeaderDeliveryDefaults.sql.
-- Only existing blank MOVING_LABOR values are changed; populated values remain intact.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ICP_HEADER', N'U') IS NULL
        THROW 50001, 'dbo.ICP_HEADER does not exist in the selected database.', 1;

    IF COL_LENGTH(N'dbo.ICP_HEADER', N'MOVING_LABOR') IS NULL
        THROW 50002, 'dbo.ICP_HEADER.MOVING_LABOR does not exist.', 1;

    UPDATE dbo.ICP_HEADER
    SET MOVING_LABOR = N'0人'
    WHERE MOVING_LABOR IS NULL
       OR LTRIM(RTRIM(MOVING_LABOR)) = N'';

    SELECT @@ROWCOUNT AS UpdatedRows;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
