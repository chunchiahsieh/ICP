-- Run after deploying the IntegrationHub version that removes routing rules.
-- Back up the database before running: existing routing-rule data is deleted.
USE [TEL_IntegrationHub];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ROUTING_RULE', N'U') IS NOT NULL
    DROP TABLE dbo.ROUTING_RULE;

COMMIT TRANSACTION;
