-- Remove STATUS column from ICP_HEADER.
-- Run manually against the target environment after deploying application changes.
USE [TEL-ICP];
GO

IF COL_LENGTH('dbo.ICP_HEADER', 'STATUS') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[ICP_HEADER] DROP COLUMN [STATUS];
END
GO
