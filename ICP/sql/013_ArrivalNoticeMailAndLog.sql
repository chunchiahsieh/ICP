IF OBJECT_ID(N'dbo.NOTIFICATION_MAIL_LOG', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NOTIFICATION_MAIL_LOG
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        MailType nvarchar(40) NOT NULL,
        EventKey nvarchar(200) NOT NULL,
        MailTo nvarchar(max) NOT NULL,
        CcTo nvarchar(max) NOT NULL,
        Subject nvarchar(500) NOT NULL,
        BodyHtml nvarchar(max) NOT NULL,
        State nvarchar(20) NOT NULL,
        CreatedUtc datetime2(7) NOT NULL,
        SentUtc datetime2(7) NULL,
        ErrorMessage nvarchar(2000) NULL
    );
    CREATE INDEX IX_NOTIFICATION_MAIL_LOG_CreatedUtc ON dbo.NOTIFICATION_MAIL_LOG (CreatedUtc DESC);
END;

IF COL_LENGTH(N'dbo.ICP_HEADER', N'ARRIVAL_NOTICE') IS NOT NULL
BEGIN
    ALTER TABLE dbo.ICP_HEADER ALTER COLUMN ARRIVAL_NOTICE nvarchar(300) NULL;
END;
