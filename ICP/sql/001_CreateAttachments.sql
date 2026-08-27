USE [TEL-ICP]
GO

IF OBJECT_ID(N'dbo.Attachments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Attachments (
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Attachments PRIMARY KEY DEFAULT NEWID(),
        AttachmentType NVARCHAR(50) NOT NULL,
        AttachmentOwnerId NVARCHAR(100) NOT NULL,
        OriginalFileName NVARCHAR(255) NOT NULL,
        StoredFileName NVARCHAR(255) NOT NULL,
        RelativePath NVARCHAR(500) NOT NULL,
        FileSize BIGINT NOT NULL,
        ContentType NVARCHAR(100) NULL,
        CreateTime DATETIME2(7) NOT NULL CONSTRAINT DF_Attachments_CreateTime DEFAULT GETDATE(),
        CreateUser NVARCHAR(100) NULL,
        UpdateTime DATETIME2(7) NULL,
        UpdateUser NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Attachments_IsDeleted DEFAULT 0
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Attachments_Active_Owner' AND object_id = OBJECT_ID(N'dbo.Attachments'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Attachments_Active_Owner
        ON dbo.Attachments(AttachmentType, AttachmentOwnerId)
        WHERE IsDeleted = 0;
END
GO
