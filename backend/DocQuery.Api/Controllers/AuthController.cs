using DocQuery.Api.Data;
using DocQuery.Api.Models;
using DocQuery.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace DocQuery.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : BaseController
{
    private readonly Database _db;
    private readonly JwtService _jwt;

    public AuthController(Database db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Password.Length < 6)
            return BadRequest("Email is required and password must be at least 6 characters.");

        var email = request.Email.Trim().ToLower();
        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        await using var conn = await _db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO users (email, password_hash) VALUES (@e, @h) ON CONFLICT (email) DO NOTHING RETURNING id", conn);
        cmd.Parameters.AddWithValue("e", email);
        cmd.Parameters.AddWithValue("h", hash);

        var id = await cmd.ExecuteScalarAsync();
        if (id == null) return Conflict("Email already registered.");

        return Ok(new AuthResponse(_jwt.CreateToken((int)id, email), email));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLower();

        await using var conn = await _db.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT id, password_hash FROM users WHERE email = @e", conn);
        cmd.Parameters.AddWithValue("e", email);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return Unauthorized("Invalid email or password.");

        var id = reader.GetInt32(0);
        var hash = reader.GetString(1);
        if (!BCrypt.Net.BCrypt.Verify(request.Password, hash)) return Unauthorized("Invalid email or password.");

        return Ok(new AuthResponse(_jwt.CreateToken(id, email), email));
    }
}
