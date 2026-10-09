/* =====================================================================
   04_Triggers.sql  -  automatic audit history
   Fires after any UPDATE on Tickets and logs a row for every ticket whose
   Status or AssigneeId actually changed. Works for multi-row updates
   because it joins the `inserted` (new) and `deleted` (old) tables.
   The procedures call sp_set_session_context('UserId', ...) first, so the
   trigger knows who made the change.
   ===================================================================== */
USE TrackIt;
GO

CREATE OR ALTER TRIGGER dbo.trg_Tickets_AuditChanges
ON dbo.Tickets
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ChangedBy NVARCHAR(450) = CAST(SESSION_CONTEXT(N'UserId') AS NVARCHAR(450));

    INSERT INTO dbo.TicketAuditLog
        (TicketId, OldStatus, NewStatus, OldAssigneeId, NewAssigneeId, ChangedBy, ChangedAt)
    SELECT
        i.TicketId, d.Status, i.Status, d.AssigneeId, i.AssigneeId, @ChangedBy, SYSUTCDATETIME()
    FROM inserted i
    JOIN deleted  d ON d.TicketId = i.TicketId
    WHERE ISNULL(i.Status, N'')     <> ISNULL(d.Status, N'')
       OR ISNULL(i.AssigneeId, N'') <> ISNULL(d.AssigneeId, N'');
END;
GO
