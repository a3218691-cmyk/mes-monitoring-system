using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using MES;

namespace MES.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StatusLogsController : ControllerBase
{
    private readonly Db _db;
    public StatusLogsController(Db db) => _db = db;

    // 狀態記錄查詢(可選機台過濾)+ 停機原因 LEFT JOIN(運轉沒原因也要顯示)
    [HttpGet]
    public IActionResult List([FromQuery] int? machineId)
    {
        const string sql = @"
SELECT s.Id, m.Code AS Machine, s.Status, r.Category AS Reason, s.StartTime, s.EndTime
FROM StatusLogs s
JOIN Machines m ON s.MachineId = m.Id
LEFT JOIN DowntimeReasons r ON s.ReasonId = r.Id
WHERE (@machineId IS NULL OR s.MachineId = @machineId)
ORDER BY s.StartTime;";

        using var conn = _db.Open();
        return Ok(conn.Query(sql, new { machineId }));
    }

    // 報工改由 PlcSimulatorService 背景模擬產生,這裡只保留查詢 + Manager 修正髒資料用的刪除
    [HttpDelete("{id}")]
    [Authorize(Roles = "Manager")]
    public IActionResult Delete(int id)
    {
        const string sql = "DELETE FROM StatusLogs WHERE Id = @id;";
        using var conn = _db.Open();
        int rows = conn.Execute(sql, new { id });
        return rows == 0 ? NotFound() : NoContent();
    }
}
