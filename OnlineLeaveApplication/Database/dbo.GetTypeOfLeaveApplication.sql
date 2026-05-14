IF OBJECT_ID(N'[dbo].[GetTypeOfLeaveApplication]', N'FN') IS NOT NULL
    DROP FUNCTION [dbo].[GetTypeOfLeaveApplication];
GO

CREATE FUNCTION [dbo].[GetTypeOfLeaveApplication]
(
    @LeaveApplicationID INT,
    @TypeOfLeave VARCHAR(250)
)
RETURNS VARCHAR(1)
AS
BEGIN
    DECLARE @Result VARCHAR(1);

    SELECT @Result = CASE WHEN EXISTS
    (
        SELECT 1
        FROM [dbo].[LeaveApplicationDetail] lad
        INNER JOIN [dbo].[TypeOfLeave] tol
            ON lad.[TypeOfLeaveID] = tol.[TypeOfLeaveID]
        WHERE lad.[LeaveApplicationID] = @LeaveApplicationID
          AND tol.[TypeOfLeave] = @TypeOfLeave
    )
    THEN '/' ELSE '' END;

    RETURN ISNULL(@Result, '');
END;
GO
