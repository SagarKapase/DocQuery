using System.Text.Json;
using DocQuery.Api.Models;
using DocQuery.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController : BaseController
{
    private readonly DocumentService _documents;
    private readonly AzureOpenAiService _ai;
    private readonly ILogger<ChatController> _logger;

    public ChatController(DocumentService documents, AzureOpenAiService ai, ILogger<ChatController> logger)
    {
        _documents = documents;
        _ai = ai;
        _logger = logger;
    }

    // Streams the answer with Server-Sent Events.
    // Each event is one line:  data: {"type":"...", ...}\n\n
    //   type "sources" -> list of citations (sent first)
    //   type "token"   -> next piece of the answer
    //   type "done"    -> finished
    [HttpPost("ask")]
    public async Task Ask(AskRequest request, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");

        try
        {
            _logger.LogInformation("User {UserId} asked: {Question}", CurrentUserId, request.Question);

            var sources = await _documents.SearchAsync(CurrentUserId, request.Question, request.DocumentId);
            await SendEvent(new { type = "sources", sources }, ct);

            if (sources.Count == 0)
            {
                await SendEvent(new { type = "token", text = "No documents found. Please upload a PDF first." }, ct);
            }
            else
            {
                var prompt = DocumentService.BuildPrompt(request.Question, sources);
                await foreach (var piece in _ai.StreamChatAsync(DocumentService.SystemPrompt, prompt, ct))
                {
                    await SendEvent(new { type = "token", text = piece }, ct);
                }
            }

            await SendEvent(new { type = "done" }, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Client disconnected");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ask failed");
            await SendEvent(new { type = "error", message = "Something went wrong while answering." }, ct);
        }
    }

    private async Task SendEvent(object data, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await Response.WriteAsync($"data: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct); // push it to the browser right away
    }
}
