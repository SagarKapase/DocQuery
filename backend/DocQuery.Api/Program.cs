using System.Text;
using DocQuery.Api.Data;
using DocQuery.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (Dependency Injection) ----
builder.Services.AddControllers();
builder.Services.AddSingleton<Database>();
builder.Services.AddSingleton<JwtService>();
builder.Services.AddSingleton<PdfService>();
builder.Services.AddHttpClient<AzureOpenAiService>();
builder.Services.AddScoped<DocumentService>();

// ---- JWT Authentication ----
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep claim names exactly as we wrote them ("userId")
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Create tables on startup (simple alternative to migrations)
await app.Services.GetRequiredService<Database>().InitializeAsync();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Needed so the test project can reference Program if required
public partial class Program { }
