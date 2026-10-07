IF OBJECT_ID(N'dbo.ARRIVAL_NOTICE_SCHEDULE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ARRIVAL_NOTICE_SCHEDULE
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        HeaderId uniqueidentifier NOT NULL,
        InvoiceNo nvarchar(30) NOT NULL,
        ArrivalNoticeValue nvarchar(300) NOT NULL,
        RecipientKey nvarchar(500) NOT NULL,
        ScheduledAtUtc datetime2(7) NOT NULL,
        State nvarchar(20) NOT NULL,
        CreatedUtc datetime2(7) NOT NULL,
        SendingUtc datetime2(7) NULL,
        CreatedUser nvarchar(100) NULL,
        SentUtc datetime2(7) NULL,
        CancelledUtc datetime2(7) NULL,
        CancelledUser nvarchar(100) NULL
    );
    CREATE UNIQUE INDEX UX_ARRIVAL_NOTICE_SCHEDULE_PendingHeader
        ON dbo.ARRIVAL_NOTICE_SCHEDULE (HeaderId) WHERE State = N'Pending';
    CREATE INDEX IX_ARRIVAL_NOTICE_SCHEDULE_State_ScheduledAtUtc
        ON dbo.ARRIVAL_NOTICE_SCHEDULE (State, ScheduledAtUtc);
END;

IF OBJECT_ID(N'dbo.ARRIVAL_NOTICE_SCHEDULE', N'U') IS NOT NULL
    AND COL_LENGTH(N'dbo.ARRIVAL_NOTICE_SCHEDULE', N'SendingUtc') IS NULL
BEGIN
    ALTER TABLE dbo.ARRIVAL_NOTICE_SCHEDULE ADD SendingUtc datetime2(7) NULL;
END;
