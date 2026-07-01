using Dapper;
using MES.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MES.Tests;

[Collection("Database collection")]
public class YieldTests
{
    private readonly DatabaseFixture _fixture;
    public YieldTests(DatabaseFixture fixture) => _fixture = fixture;

    private int SeedMachine(string code)
    {
        using var conn = _fixture.Open();
        int wsId = conn.ExecuteScalar<int>(
            "INSERT INTO Workstations (Name, Area) OUTPUT INSERTED.Id VALUES (N'測試工站', N'測試區');");
        return conn.ExecuteScalar<int>(
            "INSERT INTO Machines (WorkstationId, Code, Name) OUTPUT INSERTED.Id VALUES (@WorkstationId, @Code, N'測試機台');",
            new { WorkstationId = wsId, Code = code });
    }

    private void InsertProductionLog(int machineId, int good, int defect)
    {
        using var conn = _fixture.Open();
        conn.Execute(
            "INSERT INTO ProductionLogs (MachineId, GoodQty, DefectQty, RecordTime) VALUES (@MachineId, @Good, @Defect, GETDATE());",
            new { MachineId = machineId, Good = good, Defect = defect });
    }

    [Fact]
    public void 良品90不良10_良率應為90()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-Y01");
        InsertProductionLog(machineId, 90, 10);

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Yield());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(90.0m, dict["YieldRate"]);
    }

    [Fact]
    public void 良品不良各半_良率應為50()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-Y02");
        InsertProductionLog(machineId, 50, 50);

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Yield());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(50.0m, dict["YieldRate"]);
    }

    [Fact]
    public void 同機台多筆產量記錄應加總後計算良率()
    {
        _fixture.Reset();
        var machineId = SeedMachine("M-Y03");
        InsertProductionLog(machineId, 80, 0);   // 累計 80 良品 0 不良
        InsertProductionLog(machineId, 0, 20);   // 累計 80 良品 20 不良 → 80%

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.Yield());
        var row = Assert.Single((IEnumerable<dynamic>)result.Value!);
        var dict = (IDictionary<string, object>)row;

        Assert.Equal(80, (int)dict["Good"]);
        Assert.Equal(20, (int)dict["Defect"]);
        Assert.Equal(80.0m, dict["YieldRate"]);
    }
}
