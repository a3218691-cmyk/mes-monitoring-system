# MES 面試關鍵 SQL 練習

> 在 SSMS 連上 `.\SQLEXPRESS` → 開 New Query(Ctrl+N)→ 貼進去 → 按 Execute(F5)。
> 每一句都先有 `USE MesMonitoring;` 切到正確的庫。
>
> **重點**:這些 SQL 就是你 C# 程式背後 EF Core 自動產生的東西。面試官會問「你這個數字怎麼算出來的」,你要能把下面的邏輯講出來。

---

## 1. 機台 JOIN 工站(最基本的關聯查詢)

每台機台屬於哪個工站、哪個區域。

```sql
USE MesMonitoring;

SELECT m.Code AS 機台, m.Name AS 機台名稱, w.Name AS 工站, w.Area AS 區域
FROM Machines m
INNER JOIN Workstations w ON m.WorkstationId = w.Id;
```

**講法**:「機台和工站是多對一,我用 `WorkstationId` 外鍵做 `INNER JOIN`,把機台所屬的工站名稱帶出來。」

---

## 2. 稼動率(核心!GROUP BY + 時間區間計算)

稼動率 = 運轉時數 ÷ 總時數(運轉+停機+待機)。
`DATEDIFF(MINUTE, StartTime, EndTime)` 算每段區間幾分鐘。

```sql
USE MesMonitoring;

SELECT
    m.Code AS 機台,
    SUM(CASE WHEN s.Status = N'運轉' THEN DATEDIFF(MINUTE, s.StartTime, s.EndTime) ELSE 0 END) AS 運轉分鐘,
    SUM(DATEDIFF(MINUTE, s.StartTime, s.EndTime)) AS 總分鐘,
    ROUND(
        100.0 * SUM(CASE WHEN s.Status = N'運轉' THEN DATEDIFF(MINUTE, s.StartTime, s.EndTime) ELSE 0 END)
        / SUM(DATEDIFF(MINUTE, s.StartTime, s.EndTime))
    , 1) AS 稼動率百分比
FROM StatusLogs s
JOIN Machines m ON s.MachineId = m.Id
WHERE s.EndTime IS NOT NULL          -- 只算已結束的區間才有時長
GROUP BY m.Code
ORDER BY 稼動率百分比 DESC;
```

**講法**:「稼動率的關鍵是 StatusLog 存的是『狀態的時間區間』,不是當下狀態。我用 `DATEDIFF` 把每段算成分鐘,再用 `GROUP BY 機台` 加總,運轉時數除以總時數就是稼動率。`WHERE EndTime IS NOT NULL` 是因為還在進行中的區間沒有結束時間,不能算。」

---

## 3. 停機原因 Pareto(找停機主因)

哪種停機原因累計時間最多 → 優先改善它。

```sql
USE MesMonitoring;

SELECT
    r.Category AS 停機分類,
    SUM(DATEDIFF(MINUTE, s.StartTime, s.EndTime)) AS 累計停機分鐘,
    COUNT(*) AS 停機次數
FROM StatusLogs s
JOIN DowntimeReasons r ON s.ReasonId = r.Id
WHERE s.Status = N'停機' AND s.EndTime IS NOT NULL
GROUP BY r.Category
ORDER BY 累計停機分鐘 DESC;
```

**講法**:「停機原因我獨立成一張對照表,用 `ReasonId` 關聯,因為現場原因要能統計分類、不能讓人自由打字。這樣才能 `GROUP BY 分類` 做 Pareto,找出最該優先改善的停機主因。」

---

## 4. 良率(往 OEE 靠近)

```sql
USE MesMonitoring;

SELECT
    m.Code AS 機台,
    SUM(p.GoodQty) AS 良品,
    SUM(p.DefectQty) AS 不良,
    ROUND(100.0 * SUM(p.GoodQty) / (SUM(p.GoodQty) + SUM(p.DefectQty)), 1) AS 良率百分比
FROM ProductionLogs p
JOIN Machines m ON p.MachineId = m.Id
GROUP BY m.Code;
```

**講法**:「良率 = 良品 ÷ 總產出。OEE = 稼動率 × 表現性 × 良率,我這版先做稼動率和良率兩塊。」

---

## 5. 加分:把停機區間和原因明細列出來(JOIN 三張表)

```sql
USE MesMonitoring;

SELECT
    m.Code AS 機台,
    s.Status AS 狀態,
    r.Category AS 停機原因,
    s.StartTime AS 開始,
    s.EndTime AS 結束,
    DATEDIFF(MINUTE, s.StartTime, s.EndTime) AS 持續分鐘
FROM StatusLogs s
JOIN Machines m ON s.MachineId = m.Id
LEFT JOIN DowntimeReasons r ON s.ReasonId = r.Id   -- LEFT JOIN:運轉時沒原因也要顯示
ORDER BY m.Code, s.StartTime;
```

**講法**:「這裡用 `LEFT JOIN` 而不是 `INNER JOIN`,因為運轉狀態沒有停機原因(ReasonId 是 NULL),用 INNER JOIN 會把運轉的記錄漏掉。」

---

## 面試三個必背重點(從 SQL 帶出設計思維)

1. **StatusLog 存時間區間,不存當下狀態** → 管理者要的是稼動率分析,全靠歷史區間用 `DATEDIFF` + `GROUP BY` 算。
2. **停機原因獨立成對照表(正規化)** → 用 `ReasonId` 關聯,才能統計分類、不被自由打字汙染。
3. **INNER JOIN vs LEFT JOIN 的選擇** → 有沒有可能為 NULL,決定用哪種 JOIN。
