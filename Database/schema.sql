-- ============================================================
-- MES 設備稼動監控 — 資料庫結構 + 種子資料(純 SQL 版)
-- 這份就是「手寫 SQL 建表」,取代 EF Core 的 Migration。
-- app 啟動時會自動執行;你也可以直接在 SSMS 開來跑。
-- 全部用 IF NOT EXISTS,重複跑也不會出錯。
-- ============================================================

-- 1. 工站(最上層)
IF OBJECT_ID(N'Workstations', N'U') IS NULL
CREATE TABLE Workstations (
    Id   INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50)  NOT NULL,
    Area NVARCHAR(50)  NULL          -- 所屬區域
);

-- 2. 機台(屬於某工站,外鍵 WorkstationId)
IF OBJECT_ID(N'Machines', N'U') IS NULL
CREATE TABLE Machines (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    WorkstationId INT NOT NULL,
    Code          NVARCHAR(20) NOT NULL,    -- 機台代號 M-001
    Name          NVARCHAR(50) NOT NULL,
    CONSTRAINT FK_Machines_Workstations FOREIGN KEY (WorkstationId) REFERENCES Workstations(Id)
);

-- 4. 停機原因(對照表;先建,因為 StatusLogs 要參照它)
IF OBJECT_ID(N'DowntimeReasons', N'U') IS NULL
CREATE TABLE DowntimeReasons (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    Category    NVARCHAR(30)  NOT NULL,     -- 換線 / 故障 / 待料 / 保養
    Description NVARCHAR(100) NULL
);

-- 3. 狀態時間記錄(系統心臟;每筆 = 一段狀態的時間區間)
IF OBJECT_ID(N'StatusLogs', N'U') IS NULL
CREATE TABLE StatusLogs (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    MachineId INT NOT NULL,
    Status    NVARCHAR(20) NOT NULL,        -- 運轉 / 停機 / 待機
    ReasonId  INT NULL,                     -- 停機原因,非停機時為 NULL
    StartTime DATETIME NOT NULL,
    EndTime   DATETIME NULL,                -- 仍進行中可為 NULL
    CONSTRAINT FK_StatusLogs_Machines FOREIGN KEY (MachineId) REFERENCES Machines(Id),
    CONSTRAINT FK_StatusLogs_Reasons  FOREIGN KEY (ReasonId)  REFERENCES DowntimeReasons(Id)
);

-- 5. 產量記錄
IF OBJECT_ID(N'ProductionLogs', N'U') IS NULL
CREATE TABLE ProductionLogs (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    MachineId  INT NOT NULL,
    GoodQty    INT NOT NULL DEFAULT 0,      -- 良品數
    DefectQty  INT NOT NULL DEFAULT 0,      -- 不良品數
    RecordTime DATETIME NOT NULL,
    CONSTRAINT FK_ProductionLogs_Machines FOREIGN KEY (MachineId) REFERENCES Machines(Id)
);

-- 6. 使用者(登入 + 角色權限;密碼雜湊由 C# BCrypt 產生,種子帳號在 Program.cs 塞)
IF OBJECT_ID(N'Users', N'U') IS NULL
CREATE TABLE Users (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL UNIQUE,
    PasswordHash NVARCHAR(200) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL       -- Operator / Manager
);

-- ============================================================
-- 種子資料(只在表為空時塞,避免重複)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Workstations)
BEGIN
    SET IDENTITY_INSERT Workstations ON;
    INSERT INTO Workstations (Id, Name, Area) VALUES
        (1, N'SMT 一線', N'A 區'),
        (2, N'組裝二線', N'B 區');
    SET IDENTITY_INSERT Workstations OFF;

    SET IDENTITY_INSERT Machines ON;
    INSERT INTO Machines (Id, WorkstationId, Code, Name) VALUES
        (1, 1, N'M-001', N'貼片機 1 號'),
        (2, 1, N'M-002', N'貼片機 2 號'),
        (3, 2, N'M-101', N'鎖螺絲機 3 號');
    SET IDENTITY_INSERT Machines OFF;

    SET IDENTITY_INSERT DowntimeReasons ON;
    INSERT INTO DowntimeReasons (Id, Category, Description) VALUES
        (1, N'換線', N'換線/換料切換'),
        (2, N'故障', N'設備異常停機'),
        (3, N'待料', N'等待物料供應'),
        (4, N'保養', N'計畫性保養');
    SET IDENTITY_INSERT DowntimeReasons OFF;

    -- 用今天日期 + 固定時段建狀態區間
    DECLARE @d DATETIME = CAST(CAST(GETDATE() AS DATE) AS DATETIME);
    SET IDENTITY_INSERT StatusLogs ON;
    INSERT INTO StatusLogs (Id, MachineId, Status, ReasonId, StartTime, EndTime) VALUES
        -- 3 號機:運轉 → 故障 → 修好運轉 → 換線
        (1, 3, N'運轉', NULL, DATEADD(HOUR, 8,  @d), DATEADD(MINUTE, 630, @d)),  -- 08:00-10:30
        (2, 3, N'停機', 2,    DATEADD(MINUTE, 630, @d), DATEADD(HOUR, 11, @d)),   -- 10:30-11:00
        (3, 3, N'運轉', NULL, DATEADD(HOUR, 11, @d), DATEADD(HOUR, 12, @d)),      -- 11:00-12:00
        (4, 3, N'停機', 1,    DATEADD(HOUR, 12, @d), DATEADD(MINUTE, 740, @d)),   -- 12:00-12:20
        -- 1 號機:運轉 → 待料 → 運轉
        (5, 1, N'運轉', NULL, DATEADD(HOUR, 8,  @d), DATEADD(HOUR, 11, @d)),      -- 08:00-11:00
        (6, 1, N'停機', 3,    DATEADD(HOUR, 11, @d), DATEADD(MINUTE, 700, @d)),   -- 11:00-11:40
        (7, 1, N'運轉', NULL, DATEADD(MINUTE, 700, @d), DATEADD(HOUR, 13, @d));   -- 11:40-13:00
    SET IDENTITY_INSERT StatusLogs OFF;

    SET IDENTITY_INSERT ProductionLogs ON;
    INSERT INTO ProductionLogs (Id, MachineId, GoodQty, DefectQty, RecordTime) VALUES
        (1, 1, 980, 20, DATEADD(HOUR, 13, @d)),
        (2, 3, 450, 35, DATEADD(MINUTE, 740, @d));
    SET IDENTITY_INSERT ProductionLogs OFF;
END
