# MES 設備稼動監控系統

電子製造 MES 風格作品集。技術棧:**C# / ASP.NET Core 6 + SQL Server 2022 Express**。

## 怎麼跑

```bash
cd C:\Users\tang\MES
dotnet run
```

- 啟動時會**自動建庫建表 + 種子資料**(`Database.Migrate()`),不必手動下 SQL。
- Dashboard 網頁:啟動後看 console 印出的網址(預設 `https://localhost:7xxx` / `http://localhost:5xxx`),直接開根目錄 `/`。
- API 文件(Swagger):`/swagger`

連線字串在 `appsettings.json` → `ConnectionStrings:Mes`,目前指向 `.\SQLEXPRESS`。

## 五張表(`Models/Entities.cs`)

| 表 | 角色 |
|----|------|
| Workstation | 工站(最上層) |
| Machine | 機台(屬於某工站,FK) |
| **StatusLog** | **狀態時間區間 — 系統心臟**,稼動率全靠它算 |
| DowntimeReason | 停機原因對照表(正規化,避免自由打字) |
| ProductionLog | 產量(良品/不良 → 良率) |

## 關鍵 API

| 端點 | 練到的技術 |
|------|-----------|
| `GET /api/machines` | JOIN(機台 ↔ 工站) |
| `GET /api/statuslogs` `POST/PUT/DELETE` | CRUD 核心(報工) |
| `GET /api/dashboard/utilization` | GROUP BY 算稼動率 |
| `GET /api/dashboard/downtime-by-reason` | GROUP BY 停機原因 Pareto |
| `GET /api/dashboard/yield` | GROUP BY 良率 |

## 面試講法

- **StatusLog 存時間區間而非當下狀態** → 管理者要的是稼動率分析,得從歷史區間算。
- **停機原因獨立成對照表用 ReasonId 關聯** → 現場原因要能統計分類,不能自由打字(正規化)。
- 稼動率 = 運轉時數 ÷ (運轉+停機+待機) 時數,在 `DashboardController` 用 GROUP BY 算。
