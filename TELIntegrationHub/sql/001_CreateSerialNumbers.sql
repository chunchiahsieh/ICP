USE [ILC];
GO

IF OBJECT_ID(N'dbo.SerialNumbers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SerialNumbers
    (
        Prefix nvarchar(50) NOT NULL,
        DateKey char(8) NOT NULL,
        LastNumber int NOT NULL,
        CONSTRAINT PK_SerialNumbers PRIMARY KEY CLUSTERED (Prefix, DateKey),
        CONSTRAINT CK_SerialNumbers_LastNumber CHECK (LastNumber >= 0)
    );
END
GO
