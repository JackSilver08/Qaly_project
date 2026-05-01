-- ========================================
-- Qaly Project - SQL Server Init Script
-- Chạy tự động khi container khởi tạo lần đầu
-- ========================================

-- Tạo database nếu chưa có
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'QalyDb')
BEGIN
    CREATE DATABASE [QalyDb]
    COLLATE Vietnamese_CI_AS;
    PRINT 'Database QalyDb created successfully.';
END
GO

USE [QalyDb];
GO

-- Tạo schema (optional, dùng cho tổ chức module)
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'qaly')
BEGIN
    EXEC('CREATE SCHEMA [qaly]');
    PRINT 'Schema [qaly] created.';
END
GO

PRINT '============================================';
PRINT ' Qaly Database initialization completed!';
PRINT '============================================';
GO
