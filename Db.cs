using System.Data;
using Microsoft.Data.SqlClient;

namespace MES;

// 提供資料庫連線。純 SQL 版只需要「給我一條連到 SQL Server 的連線」,
// 查詢由 Dapper 執行你手寫的 SQL 字串。
public class Db
{
    private readonly string _conn;
    public Db(IConfiguration config) => _conn = config.GetConnectionString("Mes")!;

    public IDbConnection Open()
    {
        var c = new SqlConnection(_conn);
        c.Open();
        return c;
    }
}
