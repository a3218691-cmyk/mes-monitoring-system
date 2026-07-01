using Dapper;
using MES.Controllers;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace MES.Tests;

[Collection("Database collection")]
public class DowntimeByReasonTests
{
    private readonly DatabaseFixture _fixture;
    public DowntimeByReasonTests(DatabaseFixture fixture) => _fixture = fixture;

    private int SeedMachine()
    {
        using var conn = _fixture.Open();
        int wsId = conn.ExecuteScalar<int>(
            "INSERT INTO Workstations (Name, Area) OUTPUT INSERTED.Id VALUES (N'測試工站', N'測試區');");
        return conn.ExecuteScalar<int>(
            "INSERT INTO Machines (WorkstationId, Code, Name) OUTPUT INSERTED.Id VALUES (@WorkstationId, N'M-D01', N'測試機台');",
            new { WorkstationId = wsId });
    }

    private int SeedReason(string category)
    {
        using var conn = _fixture.Open();
        return conn.ExecuteScalar<int>(
            "INSERT INTO DowntimeReasons (Category, Description) OUTPUT INSERTED.Id VALUES (@Category, N'測試原因');",
            new { Category = category });
    }

    private void InsertDowntime(int machineId, int reasonId, DateTime start, DateTime end)
    {
        using var conn = _fixture.Open();
        conn.Execute(
            "INSERT INTO StatusLogs (MachineId, Status, ReasonId, StartTime, EndTime) VALUES (@MachineId, N'停機', @ReasonId, @Start, @End);",
            new { MachineId = machineId, ReasonId = reasonId, Start = start, End = end });
    }

    [Fact]
    public void 各分類停機時數應正確加總且依分鐘數由大到小排序()
    {
        _fixture.Reset();
        var machineId = SeedMachine();
        var t0 = new DateTime(2026, 1, 1, 8, 0, 0);

        // 故障:80 分鐘,1 筆
        var faultId = SeedReason("故障");
        InsertDowntime(machineId, faultId, t0, t0.AddMinutes(80));

        // 換線:30 + 20 = 50 分鐘,2 筆
        var changeoverId = SeedReason("換線");
        InsertDowntime(machineId, changeoverId, t0.AddMinutes(80), t0.AddMinutes(110));
        InsertDowntime(machineId, changeoverId, t0.AddMinutes(110), t0.AddMinutes(130));

        // 待料:10 分鐘,1 筆
        var waitId = SeedReason("待料");
        InsertDowntime(machineId, waitId, t0.AddMinutes(130), t0.AddMinutes(140));

        var controller = new DashboardController(_fixture.CreateDb());
        var result = Assert.IsType<OkObjectResult>(controller.DowntimeByReason());
        var rows = ((IEnumerable<dynamic>)result.Value!)
            .Select(r => (IDictionary<string, object>)r)
            .ToList();

        Assert.Equal(3, rows.Count);

        // 由多到少：故障(80) > 換線(50) > 待料(10)
        Assert.Equal("故障", rows[0]["Category"]);
        Assert.Equal(80, (int)rows[0]["Minutes"]);
        Assert.Equal(1, (int)rows[0]["Count"]);

        Assert.Equal("換線", rows[1]["Category"]);
        Assert.Equal(50, (int)rows[1]["Minutes"]);
        Assert.Equal(2, (int)rows[1]["Count"]);

        Assert.Equal("待料", rows[2]["Category"]);
        Assert.Equal(10, (int)rows[2]["Minutes"]);
        Assert.Equal(1, (int)rows[2]["Count"]);
    }
}
