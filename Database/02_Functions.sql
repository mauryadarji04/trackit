/* =====================================================================
   02_Functions.sql  -  scalar + inline table-valued functions
   ===================================================================== */
USE TrackIt;
GO

/* Due date = created time + the SLA hours for that priority. */
CREATE OR ALTER FUNCTION dbo.fn_GetSlaDueDate (@Priority NVARCHAR(10), @CreatedAt DATETIME2(0))
RETURNS DATETIME2(0)
AS
BEGIN
    DECLARE @Hours INT = (SELECT ResolutionHours FROM dbo.SlaPolicies WHERE Priority = @Priority);
    RETURN DATEADD(HOUR, @Hours, @CreatedAt);
END;
GO

/* 1 if the ticket missed its SLA:
   - resolved/closed tickets: resolved after DueAt
   - still-open tickets: DueAt has already passed */
CREATE OR ALTER FUNCTION dbo.fn_IsSlaBreached (@TicketId INT)
RETURNS BIT
AS
BEGIN
    DECLARE @Due DATETIME2(0), @Resolved DATETIME2(0), @Breached BIT = 0;

    SELECT @Due = DueAt, @Resolved = ResolvedAt
    FROM dbo.Tickets
    WHERE TicketId = @TicketId;

    IF @Due IS NULL RETURN 0;

    IF @Resolved IS NOT NULL
        SET @Breached = CASE WHEN @Resolved > @Due THEN 1 ELSE 0 END;
    ELSE
        SET @Breached = CASE WHEN SYSUTCDATETIME() > @Due THEN 1 ELSE 0 END;

    RETURN @Breached;
END;
GO

/* Open work for one developer (inline TVF: the optimizer can expand it like a view). */
CREATE OR ALTER FUNCTION dbo.fn_GetOpenTicketsByAssignee (@UserId NVARCHAR(450))
RETURNS TABLE
AS
RETURN
(
    SELECT t.TicketId, t.Title, t.ProjectId, t.Priority, t.Status, t.CreatedAt, t.DueAt
    FROM dbo.Tickets AS t
    WHERE t.AssigneeId = @UserId
      AND t.Status IN (N'Open', N'In Progress', N'Reopened')
);
GO
