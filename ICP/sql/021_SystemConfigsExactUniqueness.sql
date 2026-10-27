-- Run with the matching application version (SQL OUTPUT must be disabled for this table).
-- Deleted rows are excluded. NULL equals NULL; other text follows database collation.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
-- Lock the table during migration; do not remove any existing duplicates automatically.
IF EXISTS (
    SELECT 1 FROM dbo.SystemConfigs WITH (TABLOCKX, HOLDLOCK)
    WHERE IsDeleted = 0
    GROUP BY Category, FunctionCode, Key1, Key2, Value1, Value2, Value3, Value4, Value5, Value6
    HAVING COUNT_BIG(*) > 1
)
    THROW 51001, 'SystemConfigs_ExactDuplicate: resolve existing duplicates before migration.', 1;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SystemConfigs') AND name = N'UX_SystemConfigs')
    DROP INDEX UX_SystemConfigs ON dbo.SystemConfigs;
CREATE INDEX UX_SystemConfigs ON dbo.SystemConfigs(Category, FunctionCode, Key1, Key2)
    WHERE IsDeleted = 0;

EXEC sys.sp_executesql N'
CREATE OR ALTER TRIGGER dbo.TR_SystemConfigs_ExactDuplicate
ON dbo.SystemConfigs AFTER INSERT, UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1 FROM inserted i
        JOIN dbo.SystemConfigs s WITH (UPDLOCK, HOLDLOCK)
          ON s.Category = i.Category AND s.Key1 = i.Key1 AND s.Key2 = i.Key2
         AND (s.FunctionCode = i.FunctionCode OR (s.FunctionCode IS NULL AND i.FunctionCode IS NULL))
         AND s.Id <> i.Id AND s.IsDeleted = 0
        WHERE i.IsDeleted = 0 AND EXISTS (
            SELECT s.Value1, s.Value2, s.Value3, s.Value4, s.Value5, s.Value6
            INTERSECT
            SELECT i.Value1, i.Value2, i.Value3, i.Value4, i.Value5, i.Value6
        )
    )
        THROW 51001, ''SystemConfigs_ExactDuplicate: all ten setting fields must be unique.'', 1;
END;';
COMMIT TRANSACTION;
