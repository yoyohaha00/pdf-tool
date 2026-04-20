using System.IO;
using System.Text.RegularExpressions;
using PdfTool.Helpers;
using PdfTool.Models;
using UglyToad.PdfPig;

namespace PdfTool.Services;

public class TextService
{
    private static readonly Regex EnglishWordRegex = new(@"\b[A-Za-z]{2,}(?:'[A-Za-z]+)?\b", RegexOptions.Compiled);

    public async Task<TextStatistics> CountWordsAsync(
        IEnumerable<string> filePaths,
        IProgress<OperationProgress>? progress = null)
    {
        return await Task.Run(() =>
        {
            var files = filePaths.ToList();
            var statistics = new TextStatistics();

            for (var i = 0; i < files.Count; i++)
            {
                using var document = PdfDocument.Open(files[i]);
                statistics.PageCount += document.NumberOfPages;

                foreach (var page in document.GetPages())
                {
                    CountText(page.Text, statistics);
                }

                progress?.Report(new OperationProgress
                {
                    Percentage = ProgressHelper.CalculatePercentage(i + 1, files.Count),
                    Message = $"Counted text: {Path.GetFileName(files[i])}"
                });
            }

            return statistics;
        });
    }

    private static void CountText(string? text, TextStatistics statistics)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (var ch in text)
        {
            if (IsChineseCharacter(ch))
            {
                statistics.ChineseCharacterCount++;
            }
        }

        var englishWords = EnglishWordRegex.Matches(text);
        statistics.EnglishWordCount += englishWords.Count;

        foreach (Match word in englishWords)
        {
            statistics.EnglishCharacterCount += word.Length;
        }
    }

    private static bool IsChineseCharacter(char ch)
    {
        return ch is >= '\u4E00' and <= '\u9FFF'
            or >= '\u3400' and <= '\u4DBF'
            or >= '\uF900' and <= '\uFAFF';
    }

}
