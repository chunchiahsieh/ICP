-- DataScope now stores structured JSON conditions. Run once before deploying this feature.
ALTER TABLE dbo.RolePermissions
ALTER COLUMN DataScope nvarchar(max) NULL;
