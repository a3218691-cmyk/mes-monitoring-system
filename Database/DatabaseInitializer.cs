using Microsoft.Data.SqlClient;

namespace MES;

// 建庫 + 跑 schema.sql，從 Program.cs 抽出來讓測試專案可以重用同一套邏輯。
public static class DatabaseInitializer
{
    // 先連到 master 把目標庫建出來,再跑 schema.sql 建表
    public static void EnsureDatabase(string connectionString)
    {
        var dbName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;

        // 連 master 建庫
        var masterConn = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString;
        using (var c = new SqlConnection(masterConn))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}];";
            cmd.ExecuteNonQuery();
        }

        // 連目標庫跑 schema.sql(用 GO 之外的整段;這份腳本沒有 GO,可一次送)
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql"));
        using (var c = new SqlConnection(connectionString))
        {
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
