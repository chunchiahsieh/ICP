-- Run after 026_SystemConfigsHardDelete.sql and deploy the matching application.
-- Optional blank text is stored as NULL so Excel blank cells round-trip unchanged.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.SystemConfigs', N'IsDeleted') IS NOT NULL
    THROW 51020, 'Run 026_SystemConfigsHardDelete.sql before this script.', 1;

DECLARE @lockedRows bigint;
SELECT @lockedRows = COUNT_BIG(*) FROM dbo.SystemConfigs WITH (TABLOCKX, HOLDLOCK);

IF OBJECT_ID(N'dbo.SystemConfigs_BlankBackup_20261028', N'U') IS NULL
BEGIN
    SELECT CAST(s.Id AS int) AS OriginalId,
           s.Category, s.FunctionCode, s.Key1, s.Key2,
           s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
           s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser,
           SYSDATETIME() AS BackupTime
    INTO dbo.SystemConfigs_BlankBackup_20261028
    FROM dbo.SystemConfigs AS s WHERE 1 = 0;
END;

SELECT Id INTO #BlankSystemConfigIds
FROM dbo.SystemConfigs
WHERE Key2 IS NULL
   OR (FunctionCode IS NOT NULL AND LTRIM(RTRIM(FunctionCode)) = N'')
   OR (Value1 IS NOT NULL AND LTRIM(RTRIM(Value1)) = N'')
   OR (Value2 IS NOT NULL AND LTRIM(RTRIM(Value2)) = N'')
   OR (Value3 IS NOT NULL AND LTRIM(RTRIM(Value3)) = N'')
   OR (Value4 IS NOT NULL AND LTRIM(RTRIM(Value4)) = N'')
   OR (Value5 IS NOT NULL AND LTRIM(RTRIM(Value5)) = N'')
   OR (Value6 IS NOT NULL AND LTRIM(RTRIM(Value6)) = N'');

INSERT dbo.SystemConfigs_BlankBackup_20261028
    (OriginalId, Category, FunctionCode, Key1, Key2,
     Value1, Value2, Value3, Value4, Value5, Value6,
     CreateTime, CreateUser, UpdateTime, UpdateUser, BackupTime)
SELECT s.Id, s.Category, s.FunctionCode, s.Key1, s.Key2,
       s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
       s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser, SYSDATETIME()
FROM dbo.SystemConfigs AS s
JOIN #BlankSystemConfigIds AS b ON b.Id = s.Id
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemConfigs_BlankBackup_20261028 AS old
    WHERE old.OriginalId = s.Id
);

-- The trigger is restored before commit; the table lock prevents concurrent writes.
DISABLE TRIGGER dbo.TR_SystemConfigs_ExactDuplicate ON dbo.SystemConfigs;

UPDATE s SET
    Key2 = ISNULL(Key2, N''),
    FunctionCode = CASE WHEN FunctionCode IS NOT NULL AND LTRIM(RTRIM(FunctionCode)) = N'' THEN NULL ELSE FunctionCode END,
    Value1 = CASE WHEN Value1 IS NOT NULL AND LTRIM(RTRIM(Value1)) = N'' THEN NULL ELSE Value1 END,
    Value2 = CASE WHEN Value2 IS NOT NULL AND LTRIM(RTRIM(Value2)) = N'' THEN NULL ELSE Value2 END,
    Value3 = CASE WHEN Value3 IS NOT NULL AND LTRIM(RTRIM(Value3)) = N'' THEN NULL ELSE Value3 END,
    Value4 = CASE WHEN Value4 IS NOT NULL AND LTRIM(RTRIM(Value4)) = N'' THEN NULL ELSE Value4 END,
    Value5 = CASE WHEN Value5 IS NOT NULL AND LTRIM(RTRIM(Value5)) = N'' THEN NULL ELSE Value5 END,
    Value6 = CASE WHEN Value6 IS NOT NULL AND LTRIM(RTRIM(Value6)) = N'' THEN NULL ELSE Value6 END
FROM dbo.SystemConfigs AS s
JOIN #BlankSystemConfigIds AS b ON b.Id = s.Id;

;WITH Ranked AS (
    SELECT Id, ROW_NUMBER() OVER (
        PARTITION BY Category, FunctionCode, Key1, Key2,
                     Value1, Value2, Value3, Value4, Value5, Value6
        ORDER BY Id
    ) AS rn
    FROM dbo.SystemConfigs
)
SELECT Id INTO #DuplicateBlankIds FROM Ranked WHERE rn > 1;

INSERT dbo.SystemConfigs_BlankBackup_20261028
    (OriginalId, Category, FunctionCode, Key1, Key2,
     Value1, Value2, Value3, Value4, Value5, Value6,
     CreateTime, CreateUser, UpdateTime, UpdateUser, BackupTime)
SELECT s.Id, s.Category, s.FunctionCode, s.Key1, s.Key2,
       s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6,
       s.CreateTime, s.CreateUser, s.UpdateTime, s.UpdateUser, SYSDATETIME()
FROM dbo.SystemConfigs AS s
JOIN #DuplicateBlankIds AS d ON d.Id = s.Id
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.SystemConfigs_BlankBackup_20261028 AS old
    WHERE old.OriginalId = s.Id
);

DELETE s FROM dbo.SystemConfigs AS s
JOIN #DuplicateBlankIds AS d ON d.Id = s.Id;

IF EXISTS (SELECT 1 FROM sys.indexes
           WHERE object_id = OBJECT_ID(N'dbo.SystemConfigs') AND name = N'UX_SystemConfigs')
    DROP INDEX UX_SystemConfigs ON dbo.SystemConfigs;
ALTER TABLE dbo.SystemConfigs ALTER COLUMN Key2 nvarchar(100) NOT NULL;
CREATE INDEX UX_SystemConfigs
ON dbo.SystemConfigs(Category, FunctionCode, Key1, Key2);

ENABLE TRIGGER dbo.TR_SystemConfigs_ExactDuplicate ON dbo.SystemConfigs;

IF EXISTS (
    SELECT 1 FROM dbo.SystemConfigs
    GROUP BY Category, FunctionCode, Key1, Key2,
             Value1, Value2, Value3, Value4, Value5, Value6
    HAVING COUNT_BIG(*) > 1
)
    THROW 51021, 'SystemConfigs still contains duplicate settings.', 1;

COMMIT TRANSACTION;

SELECT COUNT(*) AS BackedUpRows
FROM dbo.SystemConfigs_BlankBackup_20261028;
