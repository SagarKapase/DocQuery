using DocQuery.Api.Models;
using DocQuery.Api.Services;

namespace DocQuery.Tests;

public class ChunkingTests
{
    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => $"w{i}"));

    [Fact]
    public void ShortPage_GivesOneChunk()
    {
        var chunks = PdfService.ChunkPages(new List<string> { Words(50) });

        Assert.Single(chunks);
        Assert.Equal(1, chunks[0].PageNumber);
    }

    [Fact]
    public void LongPage_IsSplitWithOverlap()
    {
        // 500 words, chunk 200, overlap 40 -> starts at 0, 160, 320 -> 3 chunks
        var chunks = PdfService.ChunkPages(new List<string> { Words(500) });

        Assert.Equal(3, chunks.Count);
        Assert.StartsWith("w161 ", chunks[1].Content); // second chunk starts inside the first one
    }

    [Fact]
    public void EmptyPages_AreSkipped_AndPageNumbersKept()
    {
        var chunks = PdfService.ChunkPages(new List<string> { "", "hello world" });

        Assert.Single(chunks);
        Assert.Equal(2, chunks[0].PageNumber);
    }

    [Fact]
    public void BuildPrompt_IncludesPageCitations()
    {
        var sources = new List<SourceChunk> { new(1, "guide.pdf", 3, "Some text", 0.1) };

        var prompt = DocumentService.BuildPrompt("What?", sources);

        Assert.Contains("[1] (File: guide.pdf, Page 3)", prompt);
        Assert.Contains("Question: What?", prompt);
    }
}
