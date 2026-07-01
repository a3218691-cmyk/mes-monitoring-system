using Microsoft.AspNetCore.Mvc;
using Dapper;
using MES;

namespace MES.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly Db _db;
    public DashboardController(Db db) => _db = db;

    // 目前有幾台機台 — COUNT(*) 數有幾筆資料
    [HttpGet("machine-count")]
    public IActionResult MachineCount()
    {
        const string sql = "SELECT COUNT(*) FROM Machines;";
        using var conn = _db.Open();
        int count = conn.ExecuteScalar<int>(sql);
        return Ok(new { count });
    }

    // 稼動率 = 運轉時數 ÷ 總時數(運轉+停機+待機)。DATEDIFF 算每段區間幾分鐘,GROUP BY 機台加總。
    [HttpGet("utilization")]
    public IActionResult Utilization()
    {
        const string sql = @"
SELECT
    m.Code AS Code,
    m.Name AS Name,
    SUM(CASE WHEN s.Status = N'運轉' THEN DATEDIFF(MINUTE, s.StartTime, s.EndTime) ELSE 0 END) AS RunMinutes,
    SUM(CASE WHEN s.Status = N'停機' THEN DATEDIFF(MINUTE, s.StartTime, s.EndTime) ELSE 0 END) AS DownMinutes,
    ROUND(
        100.0 * SUM(CASE WHEN s.Status = N'運轉' THEN DATEDIFF(MINUTE, s.StartTime, s.EndTime) ELSE 0 END)
        / NULLIF(SUM(DATEDIFF(MINUTE, s.StartTime, s.EndTime)), 0)
    , 1) AS Utilization
FROM StatusLogs s
JOIN Machines m ON s.MachineId = m.Id
WHERE s.EndTime IS NOT NULL
GROUP BY m.Code, m.Name
ORDER BY Utilization DESC;";

        using var conn = _db.Open();
        return Ok(conn.Query(sql));
    }

    // 停機原因 Pareto：各分類累計停機時數,找停機主因。
    [HttpGet("downtime-by-reason")]
    public IActionResult DowntimeByReason()
    {
        const string sql = @"
SELECT
    r.Category AS Category,
    SUM(DATEDIFF(MINUTE, s.StartTime, s.EndTime)) AS Minutes,
    COUNT(*) AS Count
FROM StatusLogs s
JOIN DowntimeReasons r ON s.ReasonId = r.Id
WHERE s.Status = N'停機' AND s.EndTime IS NOT NULL
GROUP BY r.Category
ORDER BY Minutes DESC;";

        using var conn = _db.Open();
        return Ok(conn.Query(sql));
    }

    // 良率 = 良品 ÷ 總產出。GROUP BY 機台。
    [HttpGet("yield")]
    public IActionResult Yield()
    {
        const string sql = @"
SELECT
    m.Code AS Code,
    SUM(p.GoodQty) AS Good,
    SUM(p.DefectQty) AS Defect,
    ROUND(100.0 * SUM(p.GoodQty) / NULLIF(SUM(p.GoodQty) + SUM(p.DefectQty), 0), 1) AS YieldRate
FROM ProductionLogs p
JOIN Machines m ON p.MachineId = m.Id
GROUP BY m.Code;";

        using var conn = _db.Open();
        return Ok(conn.Query(sql));
    }
}
