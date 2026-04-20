using System.IO;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfTool.Helpers;
using PdfTool.Models;

namespace PdfTool.Services;

public class MergeService
{
    public async Task MergeAsync(
        IEnumerable<string> filePaths,
        string outputPath,
        IProgress<OperationProgress>? progress = null)
    {
        await Task.Run(() =>
        {
            var files = filePaths.ToList();
            using var outputDocument = new PdfDocument();

            for (var i = 0; i < files.Count; i++)
            {
                using var inputDocument = PdfReader.Open(files[i], PdfDocumentOpenMode.Import);
                for (var pageIndex = 0; pageIndex < inputDocument.PageCount; pageIndex++)
                {
                    outputDocument.AddPage(inputDocument.Pages[pageIndex]);
                }

                progress?.Report(new OperationProgress
                {
                    Percentage = ProgressHelper.CalculatePercentage(i + 1, files.Count),
                    Message = $"Merged input: {Path.GetFileName(files[i])}"
                });
            }

            outputDocument.Save(outputPath);
        });
    }
}
