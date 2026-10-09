-- Runs every script in order. Use from inside the Database folder:
--   sqlcmd -S localhost -E -C -i run_all.sql                       (Windows auth)
--   sqlcmd -S localhost,1433 -U sa -P "<password>" -C -i run_all.sql   (SQL login / Docker)
-- :r is a sqlcmd command, so this file must be run with sqlcmd (or SSMS in SQLCMD mode).
:r 01_Tables.sql
:r 02_Functions.sql
:r 05_Views.sql
:r 03_StoredProcs.sql
:r 04_Triggers.sql
:r 06_SeedData.sql
