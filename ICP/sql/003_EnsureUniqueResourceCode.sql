SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF EXISTS
(
    SELECT 1
    FROM dbo.Resources
    GROUP BY ResourceCode
    HAVING COUNT(*) > 1
)
BEGIN
    SELECT ResourceCode, COUNT(*) AS DuplicateCount
    FROM dbo.Resources
    GROUP BY ResourceCode
    HAVING COUNT(*) > 1
    ORDER BY ResourceCode;

    ROLLBACK TRANSACTION;
    THROW 50001, 'Cannot create UX_Resources_ResourceCode because duplicate ResourceCode values exist. Merge duplicate resources and their RolePermissions first.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Resources')
      AND name = N'UX_Resources_ResourceCode'
)
BEGIN
    CREATE UNIQUE INDEX UX_Resources_ResourceCode
        ON dbo.Resources(ResourceCode);
END;

COMMIT TRANSACTION;
