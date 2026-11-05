-- Run once by the keycloak-db-init service. Safe to run again: everything is created only when it is missing.
-- sqlcmd passes the password as $(KeycloakPassword).
IF DB_ID(N'keycloak') IS NULL
BEGIN
    CREATE DATABASE [keycloak];
END
GO

IF SUSER_ID(N'keycloak') IS NULL
BEGIN
    CREATE LOGIN [keycloak] WITH PASSWORD = N'$(KeycloakPassword)', CHECK_POLICY = OFF;
END
GO

USE [keycloak];
GO

IF USER_ID(N'keycloak') IS NULL
BEGIN
    CREATE USER [keycloak] FOR LOGIN [keycloak];
END
ALTER ROLE db_owner ADD MEMBER [keycloak];
GO
