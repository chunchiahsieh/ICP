IF OBJECT_ID(N'dbo.NOTIFICATION_MAIL_DISPATCH', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NOTIFICATION_MAIL_DISPATCH
    (
        EventKey nvarchar(200) NOT NULL PRIMARY KEY,
        State nvarchar(20) NOT NULL,
        CreatedUtc datetime2(7) NOT NULL,
        UpdatedUtc datetime2(7) NOT NULL
    );
END;
