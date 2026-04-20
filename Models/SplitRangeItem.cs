using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PdfTool.Models;

public class SplitRangeItem : INotifyPropertyChanged
{
    private string _startPage = "1";
    private string _endPage = "1";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string StartPage
    {
        get => _startPage;
        set => SetField(ref _startPage, value);
    }

    public string EndPage
    {
        get => _endPage;
        set => SetField(ref _endPage, value);
    }

    private bool SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
