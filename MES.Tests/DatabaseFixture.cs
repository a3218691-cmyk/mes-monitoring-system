using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MES.Tests;

// 所有測試共用同一個實體測試資料庫,靠 ICollectionFixture 整個測試 assembly 只建一次
public class DatabaseFixture : IDisposable
{
    public string ConnectionString { get; }

    public DatabaseFixture()
    {
        // 優先讀環境變數(CI 用),沒設定就用本機 SQLEXPRESS 的測試專用庫(絕不能跟 MesMonitoring 共用)
        ConnectionString = Environment.GetEnvironmentVariable("MES_TEST_CONNECTION_STRING")
            ?? @"Server=.\SQLEXPRESS;Database=MesMonitoringTest;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        DatabaseInitializer.EnsureDatabase(ConnectionString);
    }

    public IDbConnection Open()
    {
        var c = new SqlConnection(ConnectionString);
        c.Open();
        return c;
    }

    // 給 controller 建構子用的假 Db(指向測試連線字串)
    public Db CreateDb()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mes"] = ConnectionString
            })
            .Build();
        return new Db(config);
    }

    // 每個測試方法執行前呼叫:依外鍵順序清空資料表(不用 DROP TABLE,結構留著)
    public void Reset()
    {
        using var conn = Open();
        conn.Execute(@"
DELETE FROM StatusLogs;
DELETE FROM ProductionLogs;
DELETE FROM DowntimeReasons;
DELETE FROM Machines;
DELETE FROM Workstations;");
    }

    public void Dispose() { }
}

[CollectionDefinition("Database collection")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
