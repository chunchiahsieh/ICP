USE [TEL-ICP];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @Category NVARCHAR(50) = N'Shipper';
DECLARE @Actor NVARCHAR(100) = N'Seed.Shipper';
DECLARE @Now DATETIME2(7) = GETDATE();

DECLARE @Source TABLE
(
    Key1 NVARCHAR(100) NOT NULL PRIMARY KEY,
    Value1 NVARCHAR(1000) NOT NULL
);

INSERT INTO @Source (Key1, Value1)
VALUES
    (N'TEL-JP', N'Tokyo Electron Ltd.'),
    (N'TEL-US', N'Tokyo Electron America, Inc.'),
    (N'TEL-US-TC', N'TEL Technology Center, America, LLC'),
    (N'TEL-US-ME', N'TEL Manufacturing and Engineering of America, Inc.'),
    (N'TEL-EU', N'Tokyo Electron Europe Ltd.'),
    (N'TEL-KR', N'Tokyo Electron Korea Ltd.'),
    (N'TEL-CN', N'Tokyo Electron (Shanghai) Ltd.'),
    (N'TEL-SG', N'Tokyo Electron Singapore Pte. Ltd.'),
    (N'TEL-MY', N'Tokyo Electron (Malaysia ) Sdn. Bhd.'),
    (N'TEL-IN', N'Tokyo Electron India Private Ltd.');

UPDATE target
SET target.Value1 = source.Value1,
    target.UpdateTime = @Now,
    target.UpdateUser = @Actor
FROM dbo.SystemConfigs AS target
INNER JOIN @Source AS source ON source.Key1 = target.Key1
WHERE target.Category = @Category
  AND target.FunctionCode IS NULL
  AND target.Key2 = N''
  AND target.IsDeleted = 0
  AND ISNULL(target.Value1, N'') <> source.Value1;

INSERT INTO dbo.SystemConfigs
(
    Category,
    FunctionCode,
    Key1,
    Key2,
    Value1,
    Value2,
    Value3,
    Value4,
    Value5,
    Value6,
    IsDeleted,
    CreateTime,
    CreateUser,
    UpdateTime,
    UpdateUser
)
SELECT
    @Category,
    NULL,
    source.Key1,
    N'',
    source.Value1,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    0,
    @Now,
    @Actor,
    NULL,
    NULL
FROM @Source AS source
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.SystemConfigs AS target WITH (UPDLOCK, HOLDLOCK)
    WHERE target.Category = @Category
      AND target.FunctionCode IS NULL
      AND target.Key1 = source.Key1
      AND target.Key2 = N''
      AND target.IsDeleted = 0
);

COMMIT TRANSACTION;

SELECT Key1, Value1
FROM dbo.SystemConfigs
WHERE Category = N'Shipper'
  AND FunctionCode IS NULL
  AND Key2 = N''
  AND IsDeleted = 0
ORDER BY Key1;
GO
