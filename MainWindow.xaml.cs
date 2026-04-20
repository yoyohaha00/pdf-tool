using PdfTool.Helpers;
using PdfTool.Models;
using PdfTool.ViewModels;
using PdfSharpCore.Pdf.IO;
using System.Text.RegularExpressions;
using Forms = System.Windows.Forms;

namespace PdfTool;

public partial class MainWindow : System.Windows.Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void BrowseCountSource_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectSinglePdf(
            "Select PDF for text counting",
            filePath => ViewModel.CountSourcePath = filePath);
    }

    private void AddFiles_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = CreateOpenFileDialog(true, "Select PDF files for merge");
        if (dialog.ShowDialog() == true)
        {
            ViewModel.AddFiles(dialog.FileNames);
        }
    }

    private void BrowseMergeOutput_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "merged.pdf",
            Title = "Save merged PDF"
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.MergeOutputPath = dialog.FileName;
        }
    }

    private async void Merge_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await ViewModel.MergeAsync();
    }

    private void BrowseSplitSource_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        SelectSinglePdf(
            "Select source PDF",
            filePath =>
            {
                ViewModel.SplitSourcePath = filePath;
                ViewModel.SetDefaultSplitOutputFolder(filePath);
            });
    }

    private void BrowseOutputFolder_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose split output folder"
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            ViewModel.SplitOutputFolder = dialog.SelectedPath;
        }
    }

    private async void Split_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        await ViewModel.SplitAsync();
    }

    private void SplitByCount_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel.SplitMode = SplitMode.EveryNPages;
    }

    private void SplitByRange_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        ViewModel.SplitMode = SplitMode.PageRanges;
    }

    private void RemoveSplitRange_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { DataContext: SplitRangeItem item })
        {
            return;
        }

        ViewModel.RemoveSplitRange(item);
    }

    private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
    {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
    }

    private void NumericTextBox_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(typeof(string)) as string ?? string.Empty;
        if (!Regex.IsMatch(pastedText, "^[0-9]+$"))
        {
            e.CancelCommand();
        }
    }

    private void PositiveNumberTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (!int.TryParse(textBox.Text, out var value) || value <= 0)
        {
            textBox.Text = "1";
            return;
        }

        textBox.Text = value.ToString();
    }

    private void SplitEveryNPagesTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        if (!int.TryParse(textBox.Text, out var value) || value <= 0)
        {
            textBox.Text = "1";
            return;
        }

        if (!FileHelper.IsPdfFile(ViewModel.SplitSourcePath))
        {
            textBox.Text = value.ToString();
            return;
        }

        using var inputDocument = PdfReader.Open(ViewModel.SplitSourcePath, PdfDocumentOpenMode.Import);
        if (value > inputDocument.PageCount)
        {
            textBox.Text = inputDocument.PageCount.ToString();
            ViewModel.SetStatus("Failed: Cannot exceed total pages.", string.Empty);
            return;
        }

        textBox.Text = value.ToString();
    }

    private void ContentPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)
            ? System.Windows.DragDropEffects.Copy
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void ContentPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var files = FileHelper.GetPdfDropFiles(e.Data).ToArray();
        if (files.Length == 0)
        {
            ViewModel.SetStatus("Failed: dropped content is not a PDF file.", string.Empty);
            return;
        }

        switch (ViewModel.CurrentPanel)
        {
            case ToolPanel.Count:
                ViewModel.CountSourcePath = files[0];
                ViewModel.SetStatus("Success: loaded PDF for counting.", files[0]);
                break;
            case ToolPanel.Split:
                ViewModel.SplitSourcePath = files[0];
                ViewModel.SetDefaultSplitOutputFolder(files[0]);
                ViewModel.SetStatus("Success: loaded PDF for splitting.", files[0]);
                break;
            default:
                ViewModel.AddFiles(files);
                break;
        }
    }

    private void SelectSinglePdf(string title, Action<string> onSelected)
    {
        var dialog = CreateOpenFileDialog(false, title);
        if (dialog.ShowDialog() == true)
        {
            onSelected(dialog.FileName);
        }
    }

    private static Microsoft.Win32.OpenFileDialog CreateOpenFileDialog(bool multiselect, string title)
    {
        return new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = multiselect,
            Title = title
        };
    }
}
