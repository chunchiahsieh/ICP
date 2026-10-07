IF OBJECT_ID(N'dbo.CONTROLLED_GOODS_NOTICE_SCHEDULE', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CONTROLLED_GOODS_NOTICE_SCHEDULE
    (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        HeaderId uniqueidentifier NOT NULL,
        InvoiceNo nvarchar(30) NOT NULL,
        Eta nvarchar(10) NOT NULL,
        ScheduledAtUtc datetime2(7) NOT NULL,
        State nvarchar(20) NOT NULL,
        CreatedUtc datetime2(7) NOT NULL,
        CreatedUser nvarchar(100) NULL,
        SendingUtc datetime2(7) NULL,
        SentUtc datetime2(7) NULL,
        CancelledUtc datetime2(7) NULL,
        CancelledUser nvarchar(100) NULL
    );
    CREATE UNIQUE INDEX UX_CONTROLLED_GOODS_NOTICE_SCHEDULE_PendingHeader
        ON dbo.CONTROLLED_GOODS_NOTICE_SCHEDULE (HeaderId) WHERE State = N'Pending';
    CREATE INDEX IX_CONTROLLED_GOODS_NOTICE_SCHEDULE_State_ScheduledAtUtc
        ON dbo.CONTROLLED_GOODS_NOTICE_SCHEDULE (State, ScheduledAtUtc);
END;
