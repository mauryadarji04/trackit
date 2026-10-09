/* =====================================================================
   03_StoredProcs.sql  -  business logic in T-SQL
   Error numbers (THROW): 50001 project, 50002 priority, 50003 ticket not
   found, 50004 assignee not a Developer, 50005 invalid transition,
   50006 ticket closed.
   ===================================================================== */
USE TrackIt;
GO

/* ---------------------------------------------------------------------
   usp_CreateTicket: insert + compute DueAt from the SLA function.
   Returns the new TicketId as a single-row result set.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_CreateTicket
    @Title       NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @ProjectId   INT,
    @ReporterId  NVARCHAR(450),
    @Priority    NVARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM dbo.Projects WHERE ProjectId = @ProjectId AND IsActive = 1)
            THROW 50001, 'Project does not exist or is inactive.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.SlaPolicies WHERE Priority = @Priority)
            THROW 50002, 'Unknown priority.', 1;

        DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();

        INSERT INTO dbo.Tickets (Title, Description, ProjectId, ReporterId, Priority, Status, CreatedAt, UpdatedAt, DueAt)
        VALUES (@Title, @Description, @ProjectId, @ReporterId, @Priority, N'Open', @Now, @Now,
                dbo.fn_GetSlaDueDate(@Priority, @Now));

        SELECT CAST(SCOPE_IDENTITY() AS INT) AS TicketId;
    END TRY
    BEGIN CATCH
        THROW;   -- re-raise the original error to the caller
    END CATCH
END;
GO

/* ---------------------------------------------------------------------
   usp_AssignTicket: assignee must be in the Developer role.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_AssignTicket
    @TicketId   INT,
    @AssigneeId NVARCHAR(450),
    @ChangedBy  NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Status NVARCHAR(15) = (SELECT Status FROM dbo.Tickets WITH (UPDLOCK) WHERE TicketId = @TicketId);
        IF @Status IS NULL THROW 50003, 'Ticket not found.', 1;
        IF @Status = N'Closed' THROW 50006, 'Closed tickets cannot be reassigned. Reopen it first.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM dbo.AspNetUserRoles ur
            JOIN dbo.AspNetRoles r ON r.Id = ur.RoleId
            JOIN dbo.AspNetUsers u ON u.Id = ur.UserId
            WHERE ur.UserId = @AssigneeId AND r.Name = N'Developer' AND u.IsActive = 1)
            THROW 50004, 'Assignee must be an active user in the Developer role.', 1;

        -- Tell the audit trigger who made the change.
        EXEC sys.sp_set_session_context @key = N'UserId', @value = @ChangedBy;

        UPDATE dbo.Tickets
        SET AssigneeId = @AssigneeId,
            UpdatedAt  = SYSUTCDATETIME()
        WHERE TicketId = @TicketId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ---------------------------------------------------------------------
   usp_UpdateTicketStatus: validates the allowed transitions.
     Open        -> In Progress
     Reopened    -> In Progress
     In Progress -> Resolved
     Resolved    -> Closed | Reopened
     Closed      -> Reopened
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_UpdateTicketStatus
    @TicketId  INT,
    @NewStatus NVARCHAR(15),
    @ChangedBy NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @Old NVARCHAR(15) = (SELECT Status FROM dbo.Tickets WITH (UPDLOCK) WHERE TicketId = @TicketId);
        IF @Old IS NULL THROW 50003, 'Ticket not found.', 1;

        IF NOT EXISTS (
            SELECT 1
            FROM (VALUES (N'Open',        N'In Progress'),
                         (N'Reopened',    N'In Progress'),
                         (N'In Progress', N'Resolved'),
                         (N'Resolved',    N'Closed'),
                         (N'Resolved',    N'Reopened'),
                         (N'Closed',      N'Reopened')) AS allowed(FromStatus, ToStatus)
            WHERE allowed.FromStatus = @Old AND allowed.ToStatus = @NewStatus)
        BEGIN
            DECLARE @Msg NVARCHAR(200) = CONCAT(N'Invalid status transition: ', @Old, N' -> ', @NewStatus, N'.');
            THROW 50005, @Msg, 1;
        END

        EXEC sys.sp_set_session_context @key = N'UserId', @value = @ChangedBy;

        UPDATE dbo.Tickets
        SET Status     = @NewStatus,
            UpdatedAt  = SYSUTCDATETIME(),
            ResolvedAt = CASE WHEN @NewStatus = N'Resolved' THEN SYSUTCDATETIME()
                              WHEN @NewStatus = N'Reopened' THEN NULL
                              ELSE ResolvedAt END
        WHERE TicketId = @TicketId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/* ---------------------------------------------------------------------
   usp_GetTicketsFiltered: optional filters + OFFSET/FETCH paging.
   NULL means "don't filter on this". TotalCount repeats on every row.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetTicketsFiltered
    @Status     NVARCHAR(15)  = NULL,
    @Priority   NVARCHAR(10)  = NULL,
    @AssigneeId NVARCHAR(450) = NULL,
    @ProjectId  INT           = NULL,
    @ReporterId NVARCHAR(450) = NULL,
    @Search     NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize   INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 SET @PageSize = 10;
    IF @PageSize > 100 SET @PageSize = 100;

    SELECT
        t.TicketId, t.Title, t.Priority, t.Status, t.ProjectId,
        p.Name AS ProjectName,
        t.ReporterId, rep.FullName AS ReporterName,
        t.AssigneeId, asg.FullName AS AssigneeName,
        t.CreatedAt, t.UpdatedAt, t.ResolvedAt, t.DueAt,
        dbo.fn_IsSlaBreached(t.TicketId) AS IsSlaBreached,
        COUNT(*) OVER () AS TotalCount
    FROM dbo.Tickets t
    JOIN dbo.Projects p        ON p.ProjectId = t.ProjectId
    JOIN dbo.AspNetUsers rep   ON rep.Id = t.ReporterId
    LEFT JOIN dbo.AspNetUsers asg ON asg.Id = t.AssigneeId
    WHERE (@Status     IS NULL OR t.Status     = @Status)
      AND (@Priority   IS NULL OR t.Priority   = @Priority)
      AND (@AssigneeId IS NULL OR t.AssigneeId = @AssigneeId)
      AND (@ProjectId  IS NULL OR t.ProjectId  = @ProjectId)
      AND (@ReporterId IS NULL OR t.ReporterId = @ReporterId)
      AND (@Search     IS NULL OR t.Title LIKE N'%' + @Search + N'%'
                               OR t.Description LIKE N'%' + @Search + N'%')
    ORDER BY t.CreatedAt DESC, t.TicketId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE);   -- a good plan for each filter combination
END;
GO

/* ---------------------------------------------------------------------
   usp_GetDashboardSummary: four result sets (read with QueryMultiple)
     1 counts by status      2 counts by priority
     3 totals (open/closed/SLA breaches)   4 ticket aging buckets
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetDashboardSummary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Status AS Label, COUNT(*) AS Total
    FROM dbo.Tickets GROUP BY Status ORDER BY Status;

    SELECT Priority AS Label, COUNT(*) AS Total
    FROM dbo.Tickets GROUP BY Priority
    ORDER BY CASE Priority WHEN N'Critical' THEN 1 WHEN N'High' THEN 2 WHEN N'Medium' THEN 3 ELSE 4 END;

    SELECT
        SUM(CASE WHEN Status <> N'Closed' THEN 1 ELSE 0 END)                        AS OpenCount,
        SUM(CASE WHEN Status =  N'Closed' THEN 1 ELSE 0 END)                        AS ClosedCount,
        SUM(CASE WHEN dbo.fn_IsSlaBreached(TicketId) = 1 THEN 1 ELSE 0 END)         AS SlaBreaches
    FROM dbo.Tickets;

    SELECT AgeBucket AS Label, COUNT(*) AS Total
    FROM dbo.vw_OpenTicketAging
    GROUP BY AgeBucket
    ORDER BY MIN(AgeDays);
END;
GO

/* ---------------------------------------------------------------------
   usp_GetResolutionStatsByDeveloper: average hours from created to resolved.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetResolutionStatsByDeveloper
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.Id        AS DeveloperId,
        u.FullName  AS DeveloperName,
        COUNT(*)    AS ResolvedCount,
        AVG(CAST(DATEDIFF(MINUTE, t.CreatedAt, t.ResolvedAt) AS FLOAT)) / 60.0 AS AvgResolutionHours
    FROM dbo.Tickets t
    JOIN dbo.AspNetUsers u ON u.Id = t.AssigneeId
    WHERE t.ResolvedAt IS NOT NULL
    GROUP BY u.Id, u.FullName
    ORDER BY AvgResolutionHours;
END;
GO

/* ---------------------------------------------------------------------
   usp_GetWeeklyDefectSummary: data for the weekly PDF report.
     1 totals for the window   2 by priority   3 by project   4 by developer
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.usp_GetWeeklyDefectSummary
    @WeekStart DATETIME2(0),
    @WeekEnd   DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM dbo.Tickets WHERE CreatedAt  >= @WeekStart AND CreatedAt  < @WeekEnd) AS Opened,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE ResolvedAt >= @WeekStart AND ResolvedAt < @WeekEnd) AS Resolved,
        (SELECT COUNT(DISTINCT TicketId) FROM dbo.TicketAuditLog
          WHERE NewStatus = N'Closed' AND ChangedAt >= @WeekStart AND ChangedAt < @WeekEnd)         AS Closed,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE Status <> N'Closed')                                AS StillOpen,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE dbo.fn_IsSlaBreached(TicketId) = 1
                                            AND (CreatedAt >= @WeekStart AND CreatedAt < @WeekEnd)) AS SlaBreaches;

    SELECT Priority AS Label, COUNT(*) AS Total
    FROM dbo.Tickets WHERE CreatedAt >= @WeekStart AND CreatedAt < @WeekEnd
    GROUP BY Priority
    ORDER BY CASE Priority WHEN N'Critical' THEN 1 WHEN N'High' THEN 2 WHEN N'Medium' THEN 3 ELSE 4 END;

    SELECT p.Name AS Label, COUNT(*) AS Total
    FROM dbo.Tickets t JOIN dbo.Projects p ON p.ProjectId = t.ProjectId
    WHERE t.CreatedAt >= @WeekStart AND t.CreatedAt < @WeekEnd
    GROUP BY p.Name ORDER BY Total DESC;

    SELECT u.FullName AS Label, COUNT(*) AS Total
    FROM dbo.Tickets t JOIN dbo.AspNetUsers u ON u.Id = t.AssigneeId
    WHERE t.ResolvedAt >= @WeekStart AND t.ResolvedAt < @WeekEnd
    GROUP BY u.FullName ORDER BY Total DESC;
END;
GO
