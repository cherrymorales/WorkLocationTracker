using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed class WorkingDayOptionViewModel : INotifyPropertyChanged
{
    private bool _isSelected;

    public WorkingDayOptionViewModel(string name, WorkingDays value, bool isSelected)
    {
        Name = name;
        Value = value;
        _isSelected = isSelected;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public WorkingDays Value { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }
}
