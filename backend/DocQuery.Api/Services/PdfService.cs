using DocQuery.Api.Models;
using UglyToad.PdfPig;

namespace DocQuery.Api.Services;

public class PdfService
{
    // Returns the text of every page. Index 0 = page 1.
    public List<string> ExtractPages(Stream pdfStream)
    {
        var pages = new List<string>();
        using var pdf = PdfDocument.Open(pdfStream);
        foreach (var page in pdf.GetPages())
        {
            pages.Add(page.Text);
        }
        return pages;
    }

    // Splits each page into overlapping chunks of words.
    // Overlap keeps sentences that cross a chunk border searchable.
    public static List<TextChunk> ChunkPages(List<string> pages, int chunkSize = 200, int overlap = 40)
    {
        var chunks = new List<TextChunk>();

        for (int i = 0; i < pages.Count; i++)
        {
            var words = pages[i].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            int step = chunkSize - overlap;

            for (int start = 0; start < words.Length; start += step)
            {
                var piece = words.Skip(start).Take(chunkSize);
                chunks.Add(new TextChunk(i + 1, string.Join(' ', piece)));

                if (start + chunkSize >= words.Length) break; // last piece reached
            }
        }

        return chunks;
    }
}
