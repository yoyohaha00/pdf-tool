using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfTool.Models;

public class FileModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _fullPath = string.Empty;
    private string _indexLabel = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string FullPath
    {
        get => _fullPath;
        set => SetField(ref _fullPath, value);
    }

    public string IndexLabel
    {
        get => _indexLabel;
        set => SetField(ref _indexLabel, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
