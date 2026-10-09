/* =====================================================================
   01_Tables.sql  -  TrackIt schema
   Run order: 01 -> 02 -> 03 -> 04 -> 05 -> 06
   Creates the database, the ASP.NET Core Identity tables (standard
   schema + FullName/IsActive), and the TrackIt tables.
   ===================================================================== */
IF DB_ID(N'TrackIt') IS NULL CREATE DATABASE TrackIt;
GO
USE TrackIt;
GO

/* ---------- ASP.NET Core Identity tables ---------- */
CREATE TABLE dbo.AspNetRoles (
    Id               NVARCHAR(450) NOT NULL CONSTRAINT PK_AspNetRoles PRIMARY KEY,
    Name             NVARCHAR(256) NULL,
    NormalizedName   NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL
);
CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles(NormalizedName) WHERE NormalizedName IS NOT NULL;

CREATE TABLE dbo.AspNetUsers (
    Id                   NVARCHAR(450)  NOT NULL CONSTRAINT PK_AspNetUsers PRIMARY KEY,
    FullName             NVARCHAR(150)  NOT NULL CONSTRAINT DF_Users_FullName DEFAULT (N''),
    IsActive             BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    UserName             NVARCHAR(256)  NULL,
    NormalizedUserName   NVARCHAR(256)  NULL,
    Email                NVARCHAR(256)  NULL,
    NormalizedEmail      NVARCHAR(256)  NULL,
    EmailConfirmed       BIT            NOT NULL,
    PasswordHash         NVARCHAR(MAX)  NULL,
    SecurityStamp        NVARCHAR(MAX)  NULL,
    ConcurrencyStamp     NVARCHAR(MAX)  NULL,
    PhoneNumber          NVARCHAR(MAX)  NULL,
    PhoneNumberConfirmed BIT            NOT NULL,
    TwoFactorEnabled     BIT            NOT NULL,
    LockoutEnd           DATETIMEOFFSET NULL,
    LockoutEnabled       BIT            NOT NULL,
    AccessFailedCount    INT            NOT NULL
);
CREATE INDEX EmailIndex ON dbo.AspNetUsers(NormalizedEmail);
CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers(NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;

CREATE TABLE dbo.AspNetUserRoles (
    UserId NVARCHAR(450) NOT NULL,
    RoleId NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AspNetUserClaims (
    Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetUserClaims PRIMARY KEY,
    UserId     NVARCHAR(450) NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AspNetRoleClaims (
    Id         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY,
    RoleId     NVARCHAR(450) NOT NULL,
    ClaimType  NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AspNetUserLogins (
    LoginProvider       NVARCHAR(128) NOT NULL,
    ProviderKey         NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId              NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
    CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
);

CREATE TABLE dbo.AspNetUserTokens (
    UserId        NVARCHAR(450) NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    Name          NVARCHAR(128) NOT NULL,
    Value         NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
    CONSTRAINT FK_UserTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
);
GO

/* ---------- TrackIt tables ---------- */
CREATE TABLE dbo.Projects (
    ProjectId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Projects PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Projects_Name UNIQUE,
    Description NVARCHAR(500) NULL,
    IsActive    BIT NOT NULL CONSTRAINT DF_Projects_IsActive DEFAULT (1)
);

CREATE TABLE dbo.SlaPolicies (
    Priority        NVARCHAR(10) NOT NULL CONSTRAINT PK_SlaPolicies PRIMARY KEY,
    ResolutionHours INT NOT NULL,
    CONSTRAINT CK_SlaPolicies_Priority CHECK (Priority IN (N'Critical', N'High', N'Medium', N'Low')),
    CONSTRAINT CK_SlaPolicies_Hours    CHECK (ResolutionHours > 0)
);

CREATE TABLE dbo.Tickets (
    TicketId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Tickets PRIMARY KEY,
    Title       NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    ProjectId   INT NOT NULL,
    ReporterId  NVARCHAR(450) NOT NULL,
    AssigneeId  NVARCHAR(450) NULL,
    Priority    NVARCHAR(10) NOT NULL CONSTRAINT DF_Tickets_Priority DEFAULT (N'Medium'),
    Status      NVARCHAR(15) NOT NULL CONSTRAINT DF_Tickets_Status   DEFAULT (N'Open'),
    CreatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_Tickets_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt   DATETIME2(0) NOT NULL CONSTRAINT DF_Tickets_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    ResolvedAt  DATETIME2(0) NULL,
    DueAt       DATETIME2(0) NULL,
    CONSTRAINT FK_Tickets_Projects FOREIGN KEY (ProjectId)  REFERENCES dbo.Projects(ProjectId),
    CONSTRAINT FK_Tickets_Reporter FOREIGN KEY (ReporterId) REFERENCES dbo.AspNetUsers(Id),
    CONSTRAINT FK_Tickets_Assignee FOREIGN KEY (AssigneeId) REFERENCES dbo.AspNetUsers(Id),
    CONSTRAINT FK_Tickets_Sla      FOREIGN KEY (Priority)   REFERENCES dbo.SlaPolicies(Priority),
    CONSTRAINT CK_Tickets_Status   CHECK (Status IN (N'Open', N'In Progress', N'Resolved', N'Closed', N'Reopened'))
);
-- Nonclustered indexes from the design
CREATE NONCLUSTERED INDEX IX_Tickets_Status_Assignee ON dbo.Tickets(Status, AssigneeId);
CREATE NONCLUSTERED INDEX IX_Tickets_Project_Created ON dbo.Tickets(ProjectId, CreatedAt);

CREATE TABLE dbo.TicketComments (
    CommentId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TicketComments PRIMARY KEY,
    TicketId        INT NOT NULL,
    UserId          NVARCHAR(450) NOT NULL,
    ParentCommentId INT NULL,            -- NULL = top-level comment, otherwise a reply (threading)
    CommentText     NVARCHAR(2000) NOT NULL,
    CreatedAt       DATETIME2(0) NOT NULL CONSTRAINT DF_Comments_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Comments_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets(TicketId) ON DELETE CASCADE,
    CONSTRAINT FK_Comments_Users   FOREIGN KEY (UserId)   REFERENCES dbo.AspNetUsers(Id),
    CONSTRAINT FK_Comments_Parent  FOREIGN KEY (ParentCommentId) REFERENCES dbo.TicketComments(CommentId)
);
CREATE NONCLUSTERED INDEX IX_Comments_Ticket ON dbo.TicketComments(TicketId, CreatedAt);

CREATE TABLE dbo.TicketAuditLog (
    AuditId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TicketAuditLog PRIMARY KEY,
    TicketId       INT NOT NULL,
    OldStatus      NVARCHAR(15) NULL,
    NewStatus      NVARCHAR(15) NULL,
    OldAssigneeId  NVARCHAR(450) NULL,
    NewAssigneeId  NVARCHAR(450) NULL,
    ChangedBy      NVARCHAR(450) NULL,
    ChangedAt      DATETIME2(0) NOT NULL CONSTRAINT DF_Audit_ChangedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Audit_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets(TicketId) ON DELETE CASCADE
);
CREATE NONCLUSTERED INDEX IX_Audit_Ticket ON dbo.TicketAuditLog(TicketId, ChangedAt);
GO
