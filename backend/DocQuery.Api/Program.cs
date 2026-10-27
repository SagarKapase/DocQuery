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

// ---- CORS: allow the hosted React app to call this API ----
// FrontendUrl can hold several addresses separated by commas.
var frontendUrls = (builder.Configuration["FrontendUrl"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.WithOrigins(frontendUrls).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Create tables on startup (simple alternative to migrations)
await app.Services.GetRequiredService<Database>().InitializeAsync();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Open the API's address in a browser to check it is running
app.MapGet("/", () => "DocQuery API is running");

app.Run();

// Needed so the test project can reference Program if required
public partial class Program { }
