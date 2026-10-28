-- Superseded by 026_SystemConfigsHardDelete.sql. Do not use for the new
-- physical-delete design; retained only to document the earlier migration.
-- Run against the intended ICP database with the application version that disables
-- the SQL OUTPUT clause for SystemConfigs. All rows are unique across ten fields.
-- Keeps the lowest Id in each identical group; backs up and removes the others.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Hold an exclusive lock until the trigger and supporting index are installed.
DECLARE @lockedRows bigint;
SELECT @lockedRows = COUNT_BIG(*)
FROM dbo.SystemConfigs WITH (TABLOCKX, HOLDLOCK);

IF OBJECT_ID(N'dbo.SystemConfigs_DedupBackup_20261028', N'U') IS NULL
BEGIN
    SELECT CAST(s.Id AS int) AS OriginalId,
           s.Category, s.FunctionCode, s.Key1, s.Key2,
           s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
           s.IsDeleted, s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser,
           SYSDATETIME() AS BackupTime
    INTO dbo.SystemConfigs_DedupBackup_20261028
    FROM dbo.SystemConfigs AS s
    WHERE 1 = 0;
END;

;WITH Ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY Category, FunctionCode, Key1, Key2,
                            Value1, Value2, Value3, Value4, Value5, Value6
               ORDER BY Id
           ) AS rn
    FROM dbo.SystemConfigs
)
SELECT Id INTO #DuplicateSystemConfigIds
FROM Ranked WHERE rn > 1;

INSERT dbo.SystemConfigs_DedupBackup_20261028
    (OriginalId, Category, FunctionCode, Key1, Key2,
     Value1, Value2, Value3, Value4, Value5, Value6,
     IsDeleted, CreateTime, CreateUser, UpdateTime, UpdateUser, BackupTime)
SELECT s.Id, s.Category, s.FunctionCode, s.Key1, s.Key2,
       s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
       s.IsDeleted, s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser,
       SYSDATETIME()
FROM dbo.SystemConfigs AS s
JOIN #DuplicateSystemConfigIds AS d ON d.Id = s.Id
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemConfigs_DedupBackup_20261028 AS b
    WHERE b.OriginalId = s.Id
);

DELETE s
FROM dbo.SystemConfigs AS s
JOIN #DuplicateSystemConfigIds AS d ON d.Id = s.Id;

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.SystemConfigs')
             AND name = N'UX_SystemConfigs')
    DROP INDEX UX_SystemConfigs ON dbo.SystemConfigs;

-- The six Value columns are too wide for a conventional ten-column unique index.
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

IF EXISTS (
    SELECT 1 FROM dbo.SystemConfigs
    GROUP BY Category, FunctionCode, Key1, Key2,
             Value1, Value2, Value3, Value4, Value5, Value6
    HAVING COUNT_BIG(*) > 1
)
    THROW 51002, 'SystemConfigs still contains duplicates.', 1;

COMMIT TRANSACTION;

SELECT OriginalId, Category, Key1, BackupTime
FROM dbo.SystemConfigs_DedupBackup_20261028
ORDER BY OriginalId;
