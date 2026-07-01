using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;
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

// Jwt:Key 沒設定就直接炸,不要讓系統靜默用空字串簽 JWT(等於沒有密鑰保護)
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key 未設定。請先執行 dotnet user-secrets set \"Jwt:Key\" \"<至少32字元的隨機字串>\" 再啟動。");
}

// JWT 驗證:對稱金鑰驗簽,Issuer 檢查,Audience 不檢查(單一前端,不需要)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// 啟動時執行 Database/schema.sql:建庫、建表、塞種子資料(純 SQL,不用 EF Migration)
EnsureDatabase(builder.Configuration);
EnsureSeedUsers(builder.Configuration);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();   // / → wwwroot/index.html
app.UseStaticFiles();
app.UseAuthentication();
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

// Users 表為空時塞兩組測試帳密;密碼雜湊只能在 C# 算(SQL 沒有 BCrypt),不能寫進 schema.sql
static void EnsureSeedUsers(IConfiguration config)
{
    using var conn = new SqlConnection(config.GetConnectionString("Mes"));
    conn.Open();
    int count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM Users;");
    if (count > 0) return;

    const string sql = "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@Username, @PasswordHash, @Role);";
    conn.Execute(sql, new[]
    {
        new { Username = "operator", PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator@123"), Role = "Operator" },
        new { Username = "manager",  PasswordHash = BCrypt.Net.BCrypt.HashPassword("Manager@123"),  Role = "Manager" }
    });
}
