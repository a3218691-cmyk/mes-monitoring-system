using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;
using MES;

namespace MES.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MachinesController : ControllerBase
{
    private readonly Db _db;
    public MachinesController(Db db) => _db = db;

    // 機台清單(含所屬工站)— JOIN(機台 ↔ 工站)
    [HttpGet]
    public IActionResult List()
    {
        const string sql = @"
SELECT m.Id, m.Code, m.Name, w.Name AS Workstation, w.Area
FROM Machines m
JOIN Workstations w ON m.WorkstationId = w.Id
ORDER BY m.Code;";

        using var conn = _db.Open();
        return Ok(conn.Query(sql));
    }

    [HttpGet("{id}")]
    public IActionResult Get(int id)
    {
        const string sql = @"
SELECT m.Id, m.Code, m.Name, w.Name AS Workstation, w.Area
FROM Machines m
JOIN Workstations w ON m.WorkstationId = w.Id
WHERE m.Id = @id;";

        using var conn = _db.Open();
        var row = conn.QueryFirstOrDefault(sql, new { id });
        return row is null ? NotFound() : Ok(row);
    }

    // 新增機台。@參數 由 Dapper 安全帶入(防 SQL injection)
    [HttpPost]
    public IActionResult Create([FromBody] MachineInput input)
    {
        const string sql = @"
INSERT INTO Machines (WorkstationId, Code, Name)
VALUES (@WorkstationId, @Code, @Name);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = _db.Open();
        int newId = conn.ExecuteScalar<int>(sql, input);
        return Ok(new { id = newId });
    }
}

public record MachineInput(int WorkstationId, string Code, string Name);
