/* =====================================================================
   06_SeedData.sql  -  reference data (idempotent, safe to re-run)
   Demo users and sample tickets are seeded by the web app on first start
   (DbSeeder.cs) because Identity password hashes must be produced by
   ASP.NET Core, not by T-SQL. That also exercises the stored procedures.
   ===================================================================== */
USE TrackIt;
GO

MERGE dbo.SlaPolicies AS tgt
USING (VALUES (N'Critical', 4), (N'High', 24), (N'Medium', 72), (N'Low', 120)) AS src(Priority, ResolutionHours)
   ON tgt.Priority = src.Priority
WHEN MATCHED THEN UPDATE SET ResolutionHours = src.ResolutionHours
WHEN NOT MATCHED THEN INSERT (Priority, ResolutionHours) VALUES (src.Priority, src.ResolutionHours);

INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT CONVERT(NVARCHAR(450), NEWID()), r.Name, UPPER(r.Name), CONVERT(NVARCHAR(36), NEWID())
FROM (VALUES (N'Admin'), (N'Developer'), (N'Reporter')) AS r(Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles x WHERE x.NormalizedName = UPPER(r.Name));

INSERT INTO dbo.Projects (Name, Description, IsActive)
SELECT p.Name, p.Description, 1
FROM (VALUES (N'Customer Portal',  N'Public web portal for customers'),
             (N'Billing Service',  N'Invoicing and payment back-end'),
             (N'Mobile App',       N'iOS and Android client')) AS p(Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Projects x WHERE x.Name = p.Name);
GO

/* ---------------------------------------------------------------------
   SSMS test queries (run after the app has seeded demo data)
   ---------------------------------------------------------------------
-- 1. SLA due date:
SELECT dbo.fn_GetSlaDueDate(N'Critical', SYSUTCDATETIME()) AS DueIn4Hours;

-- 2. Create -> assign -> status flow (replace the IDs with real ones from AspNetUsers):
-- EXEC dbo.usp_CreateTicket N'Test', N'Body', 1, '<reporterId>', N'High';
-- EXEC dbo.usp_AssignTicket 1, '<developerId>', '<adminId>';
-- EXEC dbo.usp_UpdateTicketStatus 1, N'In Progress', '<developerId>';
-- EXEC dbo.usp_UpdateTicketStatus 1, N'Closed', '<adminId>';   -- fails: 50005 invalid transition

-- 3. Audit trail written by the trigger:
SELECT * FROM dbo.TicketAuditLog ORDER BY AuditId DESC;

-- 4. Filtered + paged list:
EXEC dbo.usp_GetTicketsFiltered @Status = N'Open', @PageNumber = 1, @PageSize = 5;

-- 5. Dashboard and analytics:
EXEC dbo.usp_GetDashboardSummary;
EXEC dbo.usp_GetResolutionStatsByDeveloper;
SELECT * FROM dbo.vw_OpenTicketAging;
SELECT * FROM dbo.vw_DeveloperMonthlyRank ORDER BY Rnk;
SELECT * FROM dbo.vw_LatestCommentPerTicket;
SELECT * FROM dbo.vw_TicketsOpenedPerDay ORDER BY CreatedDay;
SELECT * FROM dbo.fn_GetOpenTicketsByAssignee('<developerId>');
--------------------------------------------------------------------- */
