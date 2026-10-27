-- Expand ShipInfo fields, preserving nullability and existing values.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @table sysname, @column sysname, @type nvarchar(40), @sql nvarchar(max);
DECLARE fields CURSOR LOCAL FAST_FORWARD FOR
SELECT t.name, c.name, v.TargetType
FROM (VALUES
    (N'ICP_HEADER', N'FLT', N'nvarchar(40)', 80, 0),
    (N'ICP_DETAIL', N'ECCN', N'nvarchar(20)', 40, 0),
    (N'ICP_DETAIL', N'GROSS_WEIGHT', N'numeric(13,3)', 0, 13)
) v(TableName, ColumnName, TargetType, TargetLength, TargetPrecision)
JOIN sys.tables t ON t.name = v.TableName AND t.schema_id = SCHEMA_ID(N'dbo')
JOIN sys.columns c ON c.object_id = t.object_id AND c.name = v.ColumnName
WHERE (v.TargetLength > 0 AND c.max_length > 0 AND c.max_length < v.TargetLength)
   OR (v.TargetPrecision > 0 AND c.precision < v.TargetPrecision AND c.scale = 3);
OPEN fields;
FETCH NEXT FROM fields INTO @table, @column, @type;
WHILE @@FETCH_STATUS = 0
BEGIN
    SELECT @sql = N'ALTER TABLE dbo.' + QUOTENAME(@table)
        + N' ALTER COLUMN ' + QUOTENAME(@column) + N' ' + @type
        + CASE WHEN c.is_nullable = 1 THEN N' NULL;' ELSE N' NOT NULL;' END
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.' + @table) AND c.name = @column;
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM fields INTO @table, @column, @type;
END;
CLOSE fields;
DEALLOCATE fields;
COMMIT TRANSACTION;
