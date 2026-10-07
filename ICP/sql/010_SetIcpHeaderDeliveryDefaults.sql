-- Run against the ICP application database. Existing rows are not modified.
-- New ICP_HEADER rows default to N/N/N and 0人 when these columns are omitted.
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @TableId int = OBJECT_ID(N'dbo.ICP_HEADER', N'U');
    IF @TableId IS NULL
        THROW 50001, 'dbo.ICP_HEADER does not exist in the selected database.', 1;

    DECLARE @ColumnName sysname, @ConstraintName sysname, @DefaultName sysname;
    DECLARE @DefaultSql nvarchar(20), @Sql nvarchar(max);

    DECLARE delivery_defaults CURSOR LOCAL FAST_FORWARD FOR
        SELECT ColumnName, ConstraintName, DefaultSql
        FROM (VALUES
            (N'DRIVER_DETAILS', N'DF_ICP_HEADER_DRIVER_DETAILS', N'(N''N'')'),
            (N'FORKLIFT', N'DF_ICP_HEADER_FORKLIFT', N'(N''N'')'),
            (N'WASTE_DISPOSAL', N'DF_ICP_HEADER_WASTE_DISPOSAL', N'(N''N'')'),
            (N'MOVING_LABOR', N'DF_ICP_HEADER_MOVING_LABOR', N'(N''0人'')')
        ) AS defaults(ColumnName, ConstraintName, DefaultSql);

    OPEN delivery_defaults;
    FETCH NEXT FROM delivery_defaults INTO @ColumnName, @ConstraintName, @DefaultSql;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @TableId AND name = @ColumnName)
            THROW 50002, 'An expected ICP_HEADER column does not exist.', 1;

        SELECT @DefaultName = dc.name
        FROM sys.columns AS c
        LEFT JOIN sys.default_constraints AS dc ON dc.object_id = c.default_object_id
        WHERE c.object_id = @TableId AND c.name = @ColumnName;

        IF @DefaultName IS NOT NULL
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.ICP_HEADER DROP CONSTRAINT ' + QUOTENAME(@DefaultName);
            EXEC sys.sp_executesql @Sql;
        END;

        SET @Sql = N'ALTER TABLE dbo.ICP_HEADER ADD CONSTRAINT '
            + QUOTENAME(@ConstraintName) + N' DEFAULT ' + @DefaultSql
            + N' FOR ' + QUOTENAME(@ColumnName);
        EXEC sys.sp_executesql @Sql;

        SET @DefaultName = NULL;
        FETCH NEXT FROM delivery_defaults INTO @ColumnName, @ConstraintName, @DefaultSql;
    END;
    CLOSE delivery_defaults;
    DEALLOCATE delivery_defaults;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
