-- One-time migration. Run only when dbo.SystemConfigs still has IsDeleted.
-- Open this file directly in SSMS; do not paste it into another SQL wrapper.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.SystemConfigs', N'IsDeleted') IS NULL
    THROW 51010, 'SystemConfigs.IsDeleted is already absent; do not run this migration twice.', 1;

DECLARE @lockedRows bigint;
SELECT @lockedRows = COUNT_BIG(*) FROM dbo.SystemConfigs WITH (TABLOCKX, HOLDLOCK);

IF OBJECT_ID(N'dbo.SystemConfigs_HardDeleteBackup_20261028', N'U') IS NULL
BEGIN
    SELECT CAST(s.Id AS int) AS OriginalId,
           s.Category, s.FunctionCode, s.Key1, s.Key2,
           s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
           s.IsDeleted, s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser,
           SYSDATETIME() AS BackupTime
    INTO dbo.SystemConfigs_HardDeleteBackup_20261028
    FROM dbo.SystemConfigs AS s WHERE 1 = 0;
END;

;WITH Ranked AS (
    SELECT Id, IsDeleted,
           ROW_NUMBER() OVER (
               PARTITION BY Category, FunctionCode, Key1, Key2,
                            Value1, Value2, Value3, Value4, Value5, Value6
               ORDER BY IsDeleted, Id
           ) AS rn
    FROM dbo.SystemConfigs
)
SELECT Id INTO #SystemConfigIdsToDelete
FROM Ranked
WHERE IsDeleted = 1 OR rn > 1;

INSERT dbo.SystemConfigs_HardDeleteBackup_20261028
    (OriginalId, Category, FunctionCode, Key1, Key2,
     Value1, Value2, Value3, Value4, Value5, Value6,
     IsDeleted, CreateTime, CreateUser, UpdateTime, UpdateUser, BackupTime)
SELECT s.Id, s.Category, s.FunctionCode, s.Key1, s.Key2,
       s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
       s.IsDeleted, s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser,
       SYSDATETIME()
FROM dbo.SystemConfigs AS s
JOIN #SystemConfigIdsToDelete AS d ON d.Id = s.Id
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemConfigs_HardDeleteBackup_20261028 AS b
    WHERE b.OriginalId = s.Id
);

DELETE s
FROM dbo.SystemConfigs AS s
JOIN #SystemConfigIdsToDelete AS d ON d.Id = s.Id;

IF EXISTS (
    SELECT 1 FROM dbo.SystemConfigs
    GROUP BY Category, FunctionCode, Key1, Key2,
             Value1, Value2, Value3, Value4, Value5, Value6
    HAVING COUNT_BIG(*) > 1
)
    THROW 51011, 'SystemConfigs still contains duplicate ten-column settings.', 1;

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.SystemConfigs') AND name = N'UX_SystemConfigs')
    DROP INDEX UX_SystemConfigs ON dbo.SystemConfigs;

DECLARE @defaultName sysname;
SELECT @defaultName = dc.name
FROM sys.default_constraints AS dc
JOIN sys.columns AS c ON c.object_id = dc.parent_object_id
                     AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.SystemConfigs')
  AND c.name = N'IsDeleted';
IF @defaultName IS NOT NULL
BEGIN
    DECLARE @dropDefaultSql nvarchar(max) = N'ALTER TABLE dbo.SystemConfigs DROP CONSTRAINT ' + QUOTENAME(@defaultName);
    EXEC sys.sp_executesql @dropDefaultSql;
END;

ALTER TABLE dbo.SystemConfigs DROP COLUMN IsDeleted;

CREATE INDEX UX_SystemConfigs
ON dbo.SystemConfigs(Category, FunctionCode, Key1, Key2);

EXEC sys.sp_executesql N'
CREATE OR ALTER TRIGGER dbo.TR_SystemConfigs_ExactDuplicate
ON dbo.SystemConfigs AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM inserted AS i
        JOIN dbo.SystemConfigs AS s WITH (UPDLOCK, HOLDLOCK)
          ON s.Category = i.Category AND s.Key1 = i.Key1
         AND s.Id <> i.Id
        WHERE EXISTS (
            SELECT s.FunctionCode, s.Key2,
                   s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6
            INTERSECT
            SELECT i.FunctionCode, i.Key2,
                   i.Value1, i.Value2, i.Value3, i.Value4, i.Value5, i.Value6
        )
    )
        THROW 51001, ''SystemConfigs_ExactDuplicate: all ten setting fields must be unique.'', 1;
END;';

COMMIT TRANSACTION;

SELECT COUNT(*) AS BackedUpAndDeletedRows
FROM dbo.SystemConfigs_HardDeleteBackup_20261028;
