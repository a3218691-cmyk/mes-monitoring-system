using Microsoft.Data.SqlClient;
using MES;

var builder = WebApplication.CreateBuilder(args);

// Dapper 回傳的欄位用 SQL 的大寫別名(Code/Utilization),這裡讓 JSON 統一轉成小寫開頭,
// 對應前端 JavaScript 用的 r.code / r.utilization
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<Db>();   // 連線小工具

var app = builder.Build();

// 啟動時執行 Database/schema.sql:建庫、建表、塞種子資料(純 SQL,不用 EF Migration)
EnsureDatabase(builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();   // / → wwwroot/index.html
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.Run();

// 先連到 master 把 MesMonitoring 庫建出來,再跑 schema.sql 建表
static void EnsureDatabase(IConfiguration config)
{
    var full = config.GetConnectionString("Mes")!;
    var dbName = new SqlConnectionStringBuilder(full).InitialCatalog;

    // 連 master 建庫
    var masterConn = new SqlConnectionStringBuilder(full) { InitialCatalog = "master" }.ConnectionString;
    using (var c = new SqlConnection(masterConn))
    {
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"IF DB_ID(N'{dbName}') IS NULL CREATE DATABASE [{dbName}];";
        cmd.ExecuteNonQuery();
    }

    // 連目標庫跑 schema.sql(用 GO 之外的整段;這份腳本沒有 GO,可一次送)
    var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Database", "schema.sql"));
    using (var c = new SqlConnection(full))
    {
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
