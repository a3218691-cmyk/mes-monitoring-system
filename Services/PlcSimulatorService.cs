using System.Data;
using Dapper;
using Microsoft.AspNetCore.SignalR;
using MES.Hubs;

namespace MES.Services;

// 模擬 PLC 背景回報:每 10 秒巡一次所有機台,依 StatusTransition 機率決定要不要換狀態,
// 換了就寫 StatusLogs + 用 SignalR 廣播,取代人工報工。
public class PlcSimulatorService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly Db _db;
    private readonly IHubContext<MachineStatusHub> _hub;
    private readonly ILogger<PlcSimulatorService> _logger;
    private readonly Random _random = new();

    public PlcSimulatorService(Db db, IHubContext<MachineStatusHub> hub, ILogger<PlcSimulatorService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Tick 本身(查機台清單等)出錯也不能讓整個 BackgroundService 掛掉
                _logger.LogError(ex, "PLC 模擬 tick 失敗");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        using var conn = _db.Open();

        var reasonIds = (await conn.QueryAsync<int>("SELECT Id FROM DowntimeReasons;")).ToList();

        // OUTER APPLY 讓沒有「進行中」(EndTime IS NULL)記錄的機台也留在結果集裡(StatusLogId/Status 為 NULL)
        const string currentSql = @"
SELECT m.Id AS MachineId, m.Code, s.Id AS StatusLogId, s.Status
FROM Machines m
OUTER APPLY (
    SELECT TOP 1 Id, Status FROM StatusLogs WHERE MachineId = m.Id AND EndTime IS NULL ORDER BY StartTime DESC
) s;";
        var machines = await conn.QueryAsync(currentSql);

        foreach (var machine in machines)
        {
            try
            {
                await ProcessMachineAsync(conn, machine, reasonIds, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "機台 {MachineId} 狀態模擬失敗", (int)machine.MachineId);
            }
        }
    }

    private async Task ProcessMachineAsync(IDbConnection conn, dynamic machine, List<int> reasonIds, CancellationToken stoppingToken)
    {
        int machineId = machine.MachineId;
        string code = machine.Code;
        int? statusLogId = machine.StatusLogId;
        string? status = machine.Status;

        // 沒有進行中記錄(PLC 模擬器第一次接管這台機台):直接補一筆「運轉」記錄,這次 tick 不跑轉換機率
        if (statusLogId is null || status is null)
        {
            await SeedRunningStatusAsync(conn, machineId, code, stoppingToken);
            return;
        }

        double roll = _random.NextDouble();
        var (newStatus, reasonId) = StatusTransition.Next(status!, roll, reasonIds);
        if (newStatus == status) return;   // 沒變化就不用寫資料庫/廣播

        var now = DateTime.Now;
        await conn.ExecuteAsync("UPDATE StatusLogs SET EndTime = @now WHERE Id = @statusLogId;", new { now, statusLogId });
        await conn.ExecuteAsync(
            "INSERT INTO StatusLogs (MachineId, Status, ReasonId, StartTime, EndTime) VALUES (@machineId, @newStatus, @reasonId, @now, NULL);",
            new { machineId, newStatus, reasonId, now });

        string? reasonCategory = reasonId is null
            ? null
            : await conn.QueryFirstOrDefaultAsync<string>("SELECT Category FROM DowntimeReasons WHERE Id = @reasonId;", new { reasonId });

        await _hub.Clients.All.SendAsync("MachineStatusChanged", new
        {
            machineId,
            code,
            status = newStatus,
            reasonCategory,
            timestamp = now
        }, stoppingToken);
    }

    // 補上第一次接管的機台:沒有「目前狀態」可轉換,直接建立一筆「運轉」記錄當作初始狀態
    private async Task SeedRunningStatusAsync(IDbConnection conn, int machineId, string code, CancellationToken stoppingToken)
    {
        var now = DateTime.Now;
        await conn.ExecuteAsync(
            "INSERT INTO StatusLogs (MachineId, Status, ReasonId, StartTime, EndTime) VALUES (@machineId, @status, NULL, @now, NULL);",
            new { machineId, status = StatusTransition.Running, now });

        await _hub.Clients.All.SendAsync("MachineStatusChanged", new
        {
            machineId,
            code,
            status = StatusTransition.Running,
            reasonCategory = (string?)null,
            timestamp = now
        }, stoppingToken);
    }
}
