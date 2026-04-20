using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PdfSharpCore.Pdf.IO;
using PdfTool.Helpers;
using PdfTool.Models;
using PdfTool.Services;

namespace PdfTool.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly TextService _textService = new();
    private readonly MergeService _mergeService = new();
    private readonly SplitService _splitService = new();

    private FileModel? _selectedFile;
    private int _progressValue;
    private string _progressMessage = "Idle";
    private string _statusMessage = "Ready";
    private string _outputPathMessage = "Output path: -";
    private string _countSourcePath = string.Empty;
    private string _mergeOutputPath = string.Empty;
    private string _splitSourcePath = string.Empty;
    private string _splitOutputFolder = string.Empty;
    private string _splitEveryNPages = "5";
    private ToolPanel _currentPanel;
    private SplitMode _splitMode;
    private TextStatistics _currentStatistics = new();

    public MainViewModel()
    {
        SplitRangeItems.CollectionChanged += SplitRangeItems_CollectionChanged;
        RemoveSelectedCommand = new RelayCommand(RemoveSelected, () => SelectedFile is not null);
        MoveUpCommand = new RelayCommand(MoveSelectedUp, () => CanMove(-1));
        MoveDownCommand = new RelayCommand(MoveSelectedDown, () => CanMove(1));
        CountWordsCommand = new RelayCommand(async () => await CountWordsAsync(), () => FileHelper.IsPdfFile(CountSourcePath));
        AddSplitRangeCommand = new RelayCommand(AddSplitRange, () => CanAddSplitRange);
        ShowCountPanelCommand = new RelayCommand(() => CurrentPanel = ToolPanel.Count);
        ShowSplitPanelCommand = new RelayCommand(() => CurrentPanel = ToolPanel.Split);
        ShowMergePanelCommand = new RelayCommand(() => CurrentPanel = ToolPanel.Merge);

        AddSplitRangeRow();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FileModel> Files { get; } = new();

    public ObservableCollection<SplitRangeItem> SplitRangeItems { get; } = new();

    public RelayCommand RemoveSelectedCommand { get; }

    public RelayCommand MoveUpCommand { get; }

    public RelayCommand MoveDownCommand { get; }

    public RelayCommand CountWordsCommand { get; }

    public RelayCommand AddSplitRangeCommand { get; }

    public RelayCommand ShowCountPanelCommand { get; }

    public RelayCommand ShowSplitPanelCommand { get; }

    public RelayCommand ShowMergePanelCommand { get; }

    public FileModel? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetField(ref _selectedFile, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ToolPanel CurrentPanel
    {
        get => _currentPanel;
        set => SetField(ref _currentPanel, value);
    }

    public SplitMode SplitMode
    {
        get => _splitMode;
        set => SetField(ref _splitMode, value);
    }

    public TextStatistics CurrentStatistics
    {
        get => _currentStatistics;
        set
        {
            if (SetField(ref _currentStatistics, value))
            {
                OnPropertyChanged(nameof(TotalCharacterSummary));
            }
        }
    }

    public string TotalCharacterSummary => $"Total characters: {CurrentStatistics.ChineseCharacterCount + CurrentStatistics.EnglishCharacterCount}";

    public string CountSourcePath
    {
        get => _countSourcePath;
        set
        {
            if (SetField(ref _countSourcePath, value))
            {
                CountWordsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string MergeOutputPath
    {
        get => _mergeOutputPath;
        set => SetField(ref _mergeOutputPath, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        set => SetField(ref _progressValue, value);
    }

    public string ProgressMessage
    {
        get => _progressMessage;
        set => SetField(ref _progressMessage, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string OutputPathMessage
    {
        get => _outputPathMessage;
        set => SetField(ref _outputPathMessage, value);
    }

    public string SplitSourcePath
    {
        get => _splitSourcePath;
        set => SetField(ref _splitSourcePath, value);
    }

    public string SplitOutputFolder
    {
        get => _splitOutputFolder;
        set => SetField(ref _splitOutputFolder, value);
    }

    public string SplitEveryNPages
    {
        get => _splitEveryNPages;
        set => SetField(ref _splitEveryNPages, value);
    }

    public string SplitRanges => string.Join(",", SplitRangeItems.Select(item => $"{item.StartPage}-{item.EndPage}"));

    public bool CanAddSplitRange => SplitRangeItems.Count == 0 || IsRangeRowReady(SplitRangeItems[^1]);

    public void AddFiles(IEnumerable<string> filePaths)
    {
        var existing = Files.Select(x => x.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var model in FileHelper.ToFileModels(filePaths))
        {
            if (existing.Add(model.FullPath))
            {
                Files.Add(model);
            }
        }

        ReindexFiles();
        RaiseCommandStates();
        SetStatus("Success: PDF files added.", $"Files loaded: {Files.Count}");
    }

    public void RemoveSplitRange(SplitRangeItem item)
    {
        if (SplitRangeItems.Count == 1)
        {
            item.StartPage = "1";
            item.EndPage = "1";
            return;
        }

        UnsubscribeRangeItem(item);
        SplitRangeItems.Remove(item);
        SyncSplitRanges();
    }

    public void SetDefaultSplitOutputFolder(string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(SplitOutputFolder))
        {
            return;
        }

        SplitOutputFolder = Path.Combine(
            Path.GetDirectoryName(sourcePath) ?? AppDomain.CurrentDomain.BaseDirectory,
            "SplitOutput");
    }

    public Task CountWordsAsync()
    {
        return RunOperationAsync(
            ValidateCountInput,
            "Counting text...",
            "Text count completed",
            async progress =>
            {
                CurrentStatistics = await _textService.CountWordsAsync(new[] { CountSourcePath }, progress);
                return CountSourcePath;
            });
    }

    public Task MergeAsync()
    {
        return RunOperationAsync(
            ValidateMergeInput,
            "Merging PDF files...",
            "PDF merge completed",
            async progress =>
            {
                await _mergeService.MergeAsync(Files.Select(x => x.FullPath), MergeOutputPath, progress);
                return MergeOutputPath;
            });
    }

    public Task SplitAsync()
    {
        return RunOperationAsync(
            ValidateSplitInput,
            "Splitting PDF...",
            "PDF split completed",
            async progress =>
            {
                var outputs = await ExecuteSplitAsync(progress);
                return $"Output folder: {SplitOutputFolder}, Files: {outputs.Count}";
            });
    }

    public void SetStatus(string status, string outputPath)
    {
        StatusMessage = status;
        OutputPathMessage = string.IsNullOrWhiteSpace(outputPath) ? "Output path: -" : outputPath;
    }

    private void RemoveSelected()
    {
        if (SelectedFile is null)
        {
            return;
        }

        Files.Remove(SelectedFile);
        SelectedFile = null;
        ReindexFiles();
        RaiseCommandStates();
        SetStatus("Success: file removed.", $"Files loaded: {Files.Count}");
    }

    private void MoveSelectedUp() => MoveSelected(-1);

    private void MoveSelectedDown() => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        if (!TryGetMoveIndices(direction, out var oldIndex, out var newIndex))
        {
            return;
        }

        Files.Move(oldIndex, newIndex);
        ReindexFiles();
        RaiseCommandStates();
    }

    private bool CanMove(int direction)
    {
        return TryGetMoveIndices(direction, out _, out _);
    }

    private bool TryGetMoveIndices(int direction, out int oldIndex, out int newIndex)
    {
        oldIndex = -1;
        newIndex = -1;

        if (SelectedFile is null)
        {
            return false;
        }

        oldIndex = Files.IndexOf(SelectedFile);
        newIndex = oldIndex + direction;
        return oldIndex >= 0 && newIndex >= 0 && newIndex < Files.Count;
    }

    private void ReindexFiles()
    {
        for (var i = 0; i < Files.Count; i++)
        {
            Files[i].IndexLabel = $"#{i + 1:00}";
        }
    }

    private async Task RunOperationAsync(
        Func<bool> canRun,
        string startMessage,
        string successMessage,
        Func<IProgress<OperationProgress>, Task<string>> operation)
    {
        if (!canRun())
        {
            return;
        }

        try
        {
            PrepareProgress(startMessage);
            var outputMessage = await operation(CreateProgress());
            ProgressValue = 100;
            ProgressMessage = successMessage;
            SetStatus($"Success: {char.ToLowerInvariant(successMessage[0])}{successMessage[1..]}.", outputMessage);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    private async Task<IReadOnlyList<string>> ExecuteSplitAsync(IProgress<OperationProgress> progress)
    {
        if (SplitMode == SplitMode.EveryNPages)
        {
            if (!int.TryParse(SplitEveryNPages, out var everyNPages))
            {
                throw new InvalidOperationException("Every N pages value is invalid.");
            }

            return await _splitService.SplitByEveryNPagesAsync(
                SplitSourcePath,
                everyNPages,
                SplitOutputFolder,
                progress);
        }

        return await _splitService.SplitByRangesAsync(
            SplitSourcePath,
            SplitRanges,
            SplitOutputFolder,
            progress);
    }

    private bool ValidateCountInput()
    {
        return Require(FileHelper.IsPdfFile(CountSourcePath), "Failed: select a valid PDF for text counting.");
    }

    private bool ValidateMergeInput()
    {
        return Require(Files.Count > 0, "Failed: no PDF files to merge.")
            && Require(!string.IsNullOrWhiteSpace(MergeOutputPath), "Failed: select an output file.");
    }

    private bool ValidateSplitInput()
    {
        return Require(FileHelper.IsPdfFile(SplitSourcePath), "Failed: select a valid source PDF.")
            && Require(!string.IsNullOrWhiteSpace(SplitOutputFolder), "Failed: select an output folder.")
            && Require(SplitMode != SplitMode.EveryNPages || IsSplitEveryNPagesValid(), "Failed: Cannot exceed total pages.")
            && Require(SplitMode != SplitMode.PageRanges || SplitRangeItems.All(IsRangeRowReady), "Failed: fill valid split ranges before splitting.")
            && Require(SplitMode != SplitMode.PageRanges || SplitRangeItems.All(item => ParsePage(item.StartPage) <= ParsePage(item.EndPage)), "Failed: range start must be less than or equal to range end.");
    }

    private bool Require(bool condition, string failureMessage)
    {
        if (condition)
        {
            return true;
        }

        SetStatus(failureMessage, string.Empty);
        return false;
    }

    private IProgress<OperationProgress> CreateProgress()
    {
        return new Progress<OperationProgress>(progress =>
        {
            ProgressValue = progress.Percentage;
            ProgressMessage = progress.Message;
        });
    }

    private void PrepareProgress(string message)
    {
        ProgressValue = 0;
        ProgressMessage = message;
    }

    private void HandleError(Exception ex)
    {
        ProgressMessage = "Operation failed";
        SetStatus($"Failed: {ex.Message}", string.Empty);
    }

    private void RaiseCommandStates()
    {
        RemoveSelectedCommand.RaiseCanExecuteChanged();
        MoveUpCommand.RaiseCanExecuteChanged();
        MoveDownCommand.RaiseCanExecuteChanged();
        CountWordsCommand.RaiseCanExecuteChanged();
        AddSplitRangeCommand.RaiseCanExecuteChanged();
    }

    private void AddSplitRange()
    {
        if (!CanAddSplitRange)
        {
            SetStatus("Failed: complete the previous page range before adding a new one.", string.Empty);
            return;
        }

        AddSplitRangeRow();
        SyncSplitRanges();
    }

    private void AddSplitRangeRow()
    {
        var item = new SplitRangeItem();
        SubscribeRangeItem(item);
        SplitRangeItems.Add(item);
    }

    private void SplitRangeItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncSplitRanges();
    }

    private void SubscribeRangeItem(SplitRangeItem item)
    {
        item.PropertyChanged += SplitRangeItem_PropertyChanged;
    }

    private void UnsubscribeRangeItem(SplitRangeItem item)
    {
        item.PropertyChanged -= SplitRangeItem_PropertyChanged;
    }

    private void SplitRangeItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        SyncSplitRanges();
    }

    private void SyncSplitRanges()
    {
        OnPropertyChanged(nameof(SplitRanges));
        OnPropertyChanged(nameof(CanAddSplitRange));
        AddSplitRangeCommand.RaiseCanExecuteChanged();
    }

    private static bool IsRangeRowReady(SplitRangeItem item)
    {
        var start = ParsePage(item.StartPage);
        var end = ParsePage(item.EndPage);
        return start > 0 && end > 0;
    }

    private bool IsSplitEveryNPagesValid()
    {
        var splitPages = ParsePage(SplitEveryNPages);
        if (splitPages <= 0)
        {
            return false;
        }

        using var inputDocument = PdfReader.Open(SplitSourcePath, PdfDocumentOpenMode.Import);
        return splitPages <= inputDocument.PageCount;
    }

    private static int ParsePage(string value)
    {
        return int.TryParse(value, out var number) ? number : 0;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
