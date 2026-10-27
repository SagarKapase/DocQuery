using Npgsql;
using Pgvector.Npgsql;

namespace DocQuery.Api.Data;

// Holds the connection pool and creates the tables.
public class Database
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<Database> _logger;
    private readonly int _dimensions; // how many numbers the embedding model returns

    public Database(IConfiguration config, ILogger<Database> logger)
    {
        _logger = logger;
        _dimensions = int.Parse(config[$"{config["AiProvider"]}:EmbeddingDimensions"] ?? "1536");
        var builder = new NpgsqlDataSourceBuilder(config.GetConnectionString("Default"));
        builder.UseVector(); // lets Npgsql send/receive pgvector values
        _dataSource = builder.Build();
    }

    public async Task<NpgsqlConnection> OpenAsync()
    {
        return await _dataSource.OpenConnectionAsync();
    }

    public async Task InitializeAsync()
    {
        // The extension must exist before we create the vector column.
        await using (var conn = await OpenAsync())
        {
            await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn);
            await cmd.ExecuteNonQueryAsync();
        }

        var sql = $"""
            CREATE TABLE IF NOT EXISTS users (
                id            SERIAL PRIMARY KEY,
                email         TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            CREATE TABLE IF NOT EXISTS documents (
                id          SERIAL PRIMARY KEY,
                user_id     INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                file_name   TEXT NOT NULL,
                page_count  INT NOT NULL,
                uploaded_at TIMESTAMPTZ NOT NULL DEFAULT now()
            );

            -- vector size must match the embedding model (EmbeddingDimensions in appsettings.json).
            -- If you switch models, drop this table so it is recreated with the new size.
            CREATE TABLE IF NOT EXISTS chunks (
                id          SERIAL PRIMARY KEY,
                document_id INT NOT NULL REFERENCES documents(id) ON DELETE CASCADE,
                user_id     INT NOT NULL,
                page_number INT NOT NULL,
                content     TEXT NOT NULL,
                embedding   vector({_dimensions}) NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_chunks_user ON chunks(user_id);
            """;

        // New connection so Npgsql reloads types (it now knows about "vector").
        await using var conn2 = await OpenAsync();
        await conn2.ReloadTypesAsync();
        await using var cmd2 = new NpgsqlCommand(sql, conn2);
        await cmd2.ExecuteNonQueryAsync();

        _logger.LogInformation("Database tables are ready");
    }
}
