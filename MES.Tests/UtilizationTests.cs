using Dapper;
using MES.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MES.Tests;

[Collection("Database collection")]
public class UtilizationTests
{
    private readonly DatabaseFixture _fixture;
    public UtilizationTests(DatabaseFixture fixture) => _fixture = fixture;

    // 塞一台機台的 StatusLogs,回傳 machineId
    private int SeedMachine(string code)
    {
        using var conn = _fixture.Open();
        int wsId = conn.ExecuteScalar<int>(
            "INSERT INTO Workstations (Name, Area) OUTPUT INSERTED.Id VALUES (N'測試工站', N'測試區');");
        int machineId = conn.ExecuteScalar<int>(
            "INSERT INTO Machines (WorkstationId, Code, Name) OUTPUT INSERTED.Id VALUES (@WorkstationId, @Code, N'測試機台');",
            new { WorkstationId = wsId, Code = code });
        return machineId;
    }

    private void InsertStatusLog(int machineId, string status, DateTime start, DateTime end)
    {
        using var conn = _fixture.Open();
        conn.Execute(
            "INSERT INTO StatusLogs (MachineId, Status, ReasonId, StartTime, EndTime) VALUES (@MachineId, @Status, NULL, @Start, @End);",
            new { MachineId = machineId, Status = status, Start = start, End = end });
    }

    [Fact]
    public void 運轉停機各半_稼動率應為50()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-U01");
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);
        InsertStatusLog(machineId, "運轉", t0, t0.AddMinutes(100));            // 100 分鐘運轉
        InsertStatusLog(machineId, "停機", t0.AddMinutes(100), t0.AddMinutes(200)); // 100 分鐘停機

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Utilization());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(50.0m, dict["Utilization"]);
    }

    [Fact]
    public void 運轉時數較多_稼動率應為75()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-U02");
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);
        InsertStatusLog(machineId, "運轉", t0, t0.AddMinutes(150));                       // 150 分鐘運轉
        InsertStatusLog(machineId, "停機", t0.AddMinutes(150), t0.AddMinutes(200));        // 50 分鐘停機

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Utilization());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(75.0m, dict["Utilization"]);
    }

    [Fact]
    public void 全程運轉無停機_稼動率應為100()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-U03");
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);
        InsertStatusLog(machineId, "運轉", t0, t0.AddMinutes(60));

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Utilization());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(100.0m, dict["Utilization"]);
    }
}
