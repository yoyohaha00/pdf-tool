using System.IO;
using PdfTool.Models;

namespace PdfTool.Helpers;

public static class FileHelper
{
    public static IEnumerable<string> GetPdfDropFiles(System.Windows.IDataObject data)
    {
        if (!data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return Enumerable.Empty<string>();
        }

        var files = data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        return files?.Where(IsPdfFile) ?? Enumerable.Empty<string>();
    }

    public static IEnumerable<FileModel> ToFileModels(IEnumerable<string> paths)
    {
        return paths
            .Where(IsPdfFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new FileModel
            {
                Name = Path.GetFileName(path),
                FullPath = path
            });
    }

    public static bool IsPdfFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
               && File.Exists(path)
               && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
    }
}
