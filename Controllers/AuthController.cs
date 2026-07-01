using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MES;

namespace MES.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly Db _db;
    private readonly IConfiguration _config;
    public AuthController(Db db, IConfiguration config) { _db = db; _config = config; }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginInput input)
    {
        const string sql = "SELECT Id, Username, PasswordHash, Role FROM Users WHERE Username = @username;";

        using var conn = _db.Open();
        var user = conn.QueryFirstOrDefault(sql, new { username = input.Username });
        if (user is null || !BCrypt.Net.BCrypt.Verify(input.Password, (string)user.PasswordHash))
            return Unauthorized();

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, (string)user.Username),
            new Claim(ClaimTypes.Role, (string)user.Role)
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiresMinutes"]!)),
            signingCredentials: creds);

        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            username = (string)user.Username,
            role = (string)user.Role
        });
    }
}

public record LoginInput(string Username, string Password);
