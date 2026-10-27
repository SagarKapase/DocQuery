using DocQuery.Api.Data;
using DocQuery.Api.Models;
using Npgsql;
using Pgvector;

namespace DocQuery.Api.Services;

// Ingestion (PDF -> chunks -> embeddings -> DB) and vector search.
// Every query filters by user_id so users only ever see their own documents.
public class DocumentService
{
    private readonly Database _db;
    private readonly PdfService _pdf;
    private readonly AzureOpenAiService _ai;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(Database db, PdfService pdf, AzureOpenAiService ai, ILogger<DocumentService> logger)
    {
        _db = db;
        _pdf = pdf;
        _ai = ai;
        _logger = logger;
    }

    public async Task<DocumentDto> IngestAsync(int userId, string fileName, Stream pdfStream)
    {
        // 1. Extract text per page
        var pages = _pdf.ExtractPages(pdfStream);

        // 2. Split into chunks
        var chunks = PdfService.ChunkPages(pages);
        _logger.LogInformation("{File}: {Pages} pages, {Chunks} chunks", fileName, pages.Count, chunks.Count);

        if (chunks.Count == 0)
            throw new InvalidOperationException("No text found in PDF (it may be a scanned image).");

        // 3. Create embeddings in small batches
        var embeddings = new List<float[]>();
        const int batchSize = 16;
        for (int i = 0; i < chunks.Count; i += batchSize)
        {
            var batch = chunks.Skip(i).Take(batchSize).Select(c => c.Content).ToList();
            embeddings.AddRange(await _ai.GetEmbeddingsAsync(batch));
        }

        // 4. Save document + chunks in one transaction
        await using var conn = await _db.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        int documentId;
        DateTime uploadedAt;
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO documents (user_id, file_name, page_count) VALUES (@u, @f, @p) RETURNING id, uploaded_at", conn, tx))
        {
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("f", fileName);
            cmd.Parameters.AddWithValue("p", pages.Count);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            documentId = reader.GetInt32(0);
            uploadedAt = reader.GetDateTime(1);
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO chunks (document_id, user_id, page_number, content, embedding) VALUES (@d, @u, @p, @c, @e)", conn, tx);
            cmd.Parameters.AddWithValue("d", documentId);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("p", chunks[i].PageNumber);
            cmd.Parameters.AddWithValue("c", chunks[i].Content);
            cmd.Parameters.AddWithValue("e", new Vector(embeddings[i]));
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        _logger.LogInformation("Saved document {DocumentId} for user {UserId}", documentId, userId);

        return new DocumentDto(documentId, fileName, pages.Count, uploadedAt);
    }

    public async Task<List<DocumentDto>> ListAsync(int userId)
    {
        var list = new List<DocumentDto>();
        await using var conn = await _db.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, file_name, page_count, uploaded_at FROM documents WHERE user_id = @u ORDER BY uploaded_at DESC", conn);
        cmd.Parameters.AddWithValue("u", userId);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new DocumentDto(reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTime(3)));
        }
        return list;
    }

    public async Task<bool> DeleteAsync(int userId, int documentId)
    {
        await using var conn = await _db.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM documents WHERE id = @id AND user_id = @u", conn);
        cmd.Parameters.AddWithValue("id", documentId);
        cmd.Parameters.AddWithValue("u", userId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    // Finds the chunks most similar to the question.
    // "<=>" is pgvector's cosine distance: smaller = more similar.
    public async Task<List<SourceChunk>> SearchAsync(int userId, string question, int? documentId, int topK = 5)
    {
        var questionVector = (await _ai.GetEmbeddingsAsync(new List<string> { question }))[0];

        var sql = """
            SELECT c.document_id, d.file_name, c.page_number, c.content, c.embedding <=> @q AS distance
            FROM chunks c
            JOIN documents d ON d.id = c.document_id
            WHERE c.user_id = @u AND (@doc::int IS NULL OR c.document_id = @doc)
            ORDER BY distance
            LIMIT @k
            """;

        var results = new List<SourceChunk>();
        await using var conn = await _db.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("q", new Vector(questionVector));
        cmd.Parameters.AddWithValue("u", userId);
        cmd.Parameters.AddWithValue("doc", (object?)documentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("k", topK);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SourceChunk(
                reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetString(3), reader.GetDouble(4)));
        }
        return results;
    }

    // Builds the prompt that gives the model the found chunks as context.
    public static string BuildPrompt(string question, List<SourceChunk> sources)
    {
        var context = string.Join("\n\n", sources.Select((s, i) =>
            $"[{i + 1}] (File: {s.FileName}, Page {s.PageNumber})\n{s.Content}"));

        return $"Context:\n{context}\n\nQuestion: {question}";
    }

    public const string SystemPrompt =
        "You answer questions using ONLY the provided context. " +
        "Cite sources inline like [1] or [2] matching the context numbers. " +
        "If the answer is not in the context, say you could not find it in the documents.";
}
