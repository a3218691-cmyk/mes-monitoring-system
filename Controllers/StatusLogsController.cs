using Microsoft.AspNetCore.Mvc;
using Dapper;
using MES;

namespace MES.Controllers;

[ApiController]
[Route("api/[controller]")]
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

    // 新增狀態記錄(報工就是打這支)
    [HttpPost]
    public IActionResult Create([FromBody] StatusLogInput input)
    {
        const string sql = @"
INSERT INTO StatusLogs (MachineId, Status, ReasonId, StartTime, EndTime)
VALUES (@MachineId, @Status, @ReasonId, @StartTime, @EndTime);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = _db.Open();
        int newId = conn.ExecuteScalar<int>(sql, input);
        return Ok(new { id = newId });
    }

    [HttpPut("{id}")]
    public IActionResult Update(int id, [FromBody] StatusLogInput input)
    {
        const string sql = @"
UPDATE StatusLogs
SET MachineId = @MachineId, Status = @Status, ReasonId = @ReasonId,
    StartTime = @StartTime, EndTime = @EndTime
WHERE Id = @id;";

        using var conn = _db.Open();
        int rows = conn.Execute(sql, new { id, input.MachineId, input.Status, input.ReasonId, input.StartTime, input.EndTime });
        return rows == 0 ? NotFound() : NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(int id)
    {
        const string sql = "DELETE FROM StatusLogs WHERE Id = @id;";
        using var conn = _db.Open();
        int rows = conn.Execute(sql, new { id });
        return rows == 0 ? NotFound() : NoContent();
    }
}

public record StatusLogInput(int MachineId, string Status, int? ReasonId, DateTime StartTime, DateTime? EndTime);
