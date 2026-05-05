-- ========================================
-- Dự án Qaly - script khởi tạo SQL Server
-- Chạy tự động khi container khởi tạo lần đầu
-- ========================================

-- Tạo cơ sở dữ liệu nếu chưa có
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'QalyDb')
BEGIN
    CREATE DATABASE [QalyDb]
    COLLATE Vietnamese_CI_AS;
    PRINT N'Cơ sở dữ liệu QalyDb đã được tạo thành công.';
END
GO

USE [QalyDb];
GO

-- Tạo schema nếu cần dùng cho tổ chức module
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'qaly')
BEGIN
    EXEC('CREATE SCHEMA [qaly]');
    PRINT N'Schema [qaly] đã được tạo.';
END
GO

PRINT '============================================';
PRINT N' Khởi tạo cơ sở dữ liệu Qaly đã hoàn tất!';
PRINT '============================================';
GO
