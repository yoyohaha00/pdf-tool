using System.IO;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfTool.Helpers;
using PdfTool.Models;

namespace PdfTool.Services;

public class SplitService
{
    public async Task<IReadOnlyList<string>> SplitByEveryNPagesAsync(
        string sourcePath,
        int everyNPages,
        string outputFolder,
        IProgress<OperationProgress>? progress = null)
    {
        if (everyNPages <= 0)
        {
            throw new ArgumentException("Every N pages must be greater than zero.", nameof(everyNPages));
        }

        return await Task.Run(() =>
        {
            Directory.CreateDirectory(outputFolder);

            using var inputDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            var totalSegments = (int)Math.Ceiling(inputDocument.PageCount / (double)everyNPages);
            var outputs = new List<string>(totalSegments);

            for (var i = 0; i < totalSegments; i++)
            {
                var startPage = i * everyNPages;
                var endPage = Math.Min(startPage + everyNPages - 1, inputDocument.PageCount - 1);
                var outputPath = Path.Combine(outputFolder, $"split_{i + 1:00}.pdf");

                using var outputDocument = new PdfDocument();
                for (var pageIndex = startPage; pageIndex <= endPage; pageIndex++)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }

                outputDocument.Save(outputPath);
                outputs.Add(outputPath);

                progress?.Report(new OperationProgress
                {
                    Percentage = ProgressHelper.CalculatePercentage(i + 1, totalSegments),
                    Message = $"Created split file: {Path.GetFileName(outputPath)}"
                });
            }

            return (IReadOnlyList<string>)outputs;
        });
    }

    public async Task<IReadOnlyList<string>> SplitByRangesAsync(
        string sourcePath,
        string ranges,
        string outputFolder,
        IProgress<OperationProgress>? progress = null)
    {
        var parsedRanges = ParseRanges(ranges).ToList();
        if (parsedRanges.Count == 0)
        {
            throw new ArgumentException("Enter at least one valid page range, for example 1-3,4-6.", nameof(ranges));
        }

        return await Task.Run(() =>
        {
            Directory.CreateDirectory(outputFolder);

            using var inputDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
            var outputs = new List<string>(parsedRanges.Count);

            for (var i = 0; i < parsedRanges.Count; i++)
            {
                var (start, end) = parsedRanges[i];
                if (start < 1 || end > inputDocument.PageCount || start > end)
                {
                    throw new InvalidOperationException($"Page range {start}-{end} is outside the PDF page count.");
                }

                var outputPath = Path.Combine(outputFolder, $"range_{start}_{end}.pdf");

                using var outputDocument = new PdfDocument();
                for (var pageIndex = start - 1; pageIndex < end; pageIndex++)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }

                outputDocument.Save(outputPath);
                outputs.Add(outputPath);

                progress?.Report(new OperationProgress
                {
                    Percentage = ProgressHelper.CalculatePercentage(i + 1, parsedRanges.Count),
                    Message = $"Created range file: {start}-{end}"
                });
            }

            return (IReadOnlyList<string>)outputs;
        });
    }

    private static IEnumerable<(int Start, int End)> ParseRanges(string? ranges)
    {
        if (string.IsNullOrWhiteSpace(ranges))
        {
            yield break;
        }

        var segments = ranges.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var bounds = segment.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (bounds.Length == 1 && int.TryParse(bounds[0], out var singlePage))
            {
                yield return (singlePage, singlePage);
            }
            else if (bounds.Length == 2
                     && int.TryParse(bounds[0], out var start)
                     && int.TryParse(bounds[1], out var end))
            {
                yield return (start, end);
            }
        }
    }
}
