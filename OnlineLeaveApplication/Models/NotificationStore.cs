using System;

namespace OnlineLeaveApplication
{
    public static class NotificationStore
    {
        const string EnsureNotificationTableSql = @"
IF OBJECT_ID(N'dbo.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notification
    (
        NotificationID INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_Notification PRIMARY KEY,
        EmployeeID SMALLINT NOT NULL,
        RegionalOrderID INT NULL,
        NotificationType NVARCHAR(50) NOT NULL,
        Title NVARCHAR(160) NOT NULL,
        Message NVARCHAR(500) NOT NULL,
        TargetUrl NVARCHAR(300) NULL,
        ReadAt DATETIME NULL,
        CreatedAt DATETIME NOT NULL
            CONSTRAINT DF_Notification_CreatedAt DEFAULT (GETDATE()),
        CreatedByEmployeeID SMALLINT NULL,
        CONSTRAINT FK_Notification_Employee
            FOREIGN KEY (EmployeeID) REFERENCES dbo.Employee(EmployeeID),
        CONSTRAINT FK_Notification_RegionalOrder
            FOREIGN KEY (RegionalOrderID) REFERENCES dbo.RegionalOrder(RegionalOrderID)
    );

    CREATE INDEX IX_Notification_Employee_ReadAt_CreatedAt
        ON dbo.Notification (EmployeeID, ReadAt, CreatedAt DESC);
END";

        public static void EnsureNotificationTableExists(OnlineLeaveApplicationEntities db)
        {
            db.Database.ExecuteSqlCommand(EnsureNotificationTableSql);
        }

        public static bool TryEnsureNotificationTableExists(OnlineLeaveApplicationEntities db)
        {
            try
            {
                EnsureNotificationTableExists(db);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
