/* =====================================================================
   05_Views.sql  -  the "interview" queries: CTE, window functions, etc.
   ===================================================================== */
USE TrackIt;
GO

/* CTE: bucket open tickets by age (0-2, 3-7, 8+ days). */
CREATE OR ALTER VIEW dbo.vw_OpenTicketAging
AS
WITH OpenTickets AS (
    SELECT TicketId, Title, Priority, Status, AssigneeId, ProjectId, CreatedAt,
           DATEDIFF(DAY, CreatedAt, SYSUTCDATETIME()) AS AgeDays
    FROM dbo.Tickets
    WHERE Status IN (N'Open', N'In Progress', N'Reopened')
)
SELECT TicketId, Title, Priority, Status, AssigneeId, ProjectId, CreatedAt, AgeDays,
       CASE WHEN AgeDays <= 2 THEN N'0-2 days'
            WHEN AgeDays <= 7 THEN N'3-7 days'
            ELSE N'8+ days' END AS AgeBucket
FROM OpenTickets;
GO

/* ROW_NUMBER(): newest comment per ticket. */
CREATE OR ALTER VIEW dbo.vw_LatestCommentPerTicket
AS
SELECT TicketId, CommentId, UserId, CommentText, CreatedAt
FROM (
    SELECT c.*, ROW_NUMBER() OVER (PARTITION BY c.TicketId ORDER BY c.CreatedAt DESC, c.CommentId DESC) AS rn
    FROM dbo.TicketComments c
) x
WHERE rn = 1;
GO

/* SUM() OVER: running total of tickets opened per day. */
CREATE OR ALTER VIEW dbo.vw_TicketsOpenedPerDay
AS
SELECT CreatedDay,
       OpenedThatDay,
       SUM(OpenedThatDay) OVER (ORDER BY CreatedDay ROWS UNBOUNDED PRECEDING) AS RunningTotal
FROM (
    SELECT CAST(CreatedAt AS DATE) AS CreatedDay, COUNT(*) AS OpenedThatDay
    FROM dbo.Tickets
    GROUP BY CAST(CreatedAt AS DATE)
) d;
GO

/* RANK(): developers by tickets closed this month (closure time comes from the audit log). */
CREATE OR ALTER VIEW dbo.vw_DeveloperMonthlyRank
AS
WITH ClosedThisMonth AS (
    SELECT DISTINCT a.TicketId, t.AssigneeId
    FROM dbo.TicketAuditLog a
    JOIN dbo.Tickets t ON t.TicketId = a.TicketId
    WHERE a.NewStatus = N'Closed'
      AND a.ChangedAt >= DATEFROMPARTS(YEAR(SYSUTCDATETIME()), MONTH(SYSUTCDATETIME()), 1)
      AND t.AssigneeId IS NOT NULL
)
SELECT u.Id AS DeveloperId,
       u.FullName AS DeveloperName,
       COUNT(*) AS ClosedCount,
       RANK() OVER (ORDER BY COUNT(*) DESC) AS Rnk
FROM ClosedThisMonth c
JOIN dbo.AspNetUsers u ON u.Id = c.AssigneeId
GROUP BY u.Id, u.FullName;
GO
