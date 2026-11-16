using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Monitor.ViewModels;

namespace WorkLocationTracker.Monitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private readonly DispatcherTimer _refreshTimer;
    private bool _isLoaded;
    private bool _isPeriodNavigationInProgress;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += async (_, _) => await RunUiActionAsync(_viewModel.RefreshAsync);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(_viewModel.InitialiseAsync);
        _isLoaded = true;
        _refreshTimer.Start();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) =>
        await RunUiActionAsync(_viewModel.RefreshAsync);

    private async void OpenDailyDetail_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedItem = DailyDetailTab;
        await RunPeriodNavigationAsync(_viewModel.GoToTodayAsync);
    }

    private async void PreviousDailyDate_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(token => _viewModel.MoveDailyDetailDateAsync(-1, token));

    private async void NextDailyDate_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(token => _viewModel.MoveDailyDetailDateAsync(1, token));

    private async void TodayDailyDate_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(_viewModel.GoToTodayAsync);

    private async void PreviousMonth_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(token => _viewModel.MoveSelectedMonthAsync(-1, token));

    private async void NextMonth_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(token => _viewModel.MoveSelectedMonthAsync(1, token));

    private async void CurrentMonth_Click(object sender, RoutedEventArgs e) =>
        await RunPeriodNavigationAsync(_viewModel.GoToCurrentMonthAsync);

    private async void DailyDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded && !_isPeriodNavigationInProgress)
        {
            await RunPeriodNavigationAsync(_viewModel.RefreshDailyDetailAsync);
        }
    }

    private async void MonthSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoaded && !_isPeriodNavigationInProgress)
        {
            await RunPeriodNavigationAsync(_viewModel.RefreshSelectedMonthAsync);
        }
    }

    private async void MarkHome_Click(object sender, RoutedEventArgs e) =>
        await ClassifyAsync(LocationType.Home);

    private async void MarkOffice_Click(object sender, RoutedEventArgs e) =>
        await ClassifyAsync(LocationType.Office);

    private async void MarkOther_Click(object sender, RoutedEventArgs e) =>
        await ClassifyAsync(LocationType.Other);

    private async void ClearClassification_Click(object sender, RoutedEventArgs e) =>
        await ClassifyAsync(LocationType.Unknown);

    private async void DeleteNetwork_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedNetwork;
        if (selected is null)
        {
            MessageBox.Show(
                this,
                "Select a network to delete.",
                "Delete network",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        int? linkedDayCount = null;
        await RunUiActionAsync(async token =>
            linkedDayCount = await _viewModel.GetNetworkUsageDayCountAsync(selected, token));
        if (linkedDayCount is null)
        {
            return;
        }

        var message = linkedDayCount > 0
            ? $"{selected.NetworkName} is linked to {linkedDayCount} daily usage {(linkedDayCount == 1 ? "record" : "records")}.\n\n" +
              "Deleting this network removes its saved classification. Its recorded hours will not be deleted, but linked time will become Unclassified.\n\n" +
              "If the service detects this network again, it will be added back as Unclassified.\n\nDelete this network?"
            : $"Delete {selected.NetworkName}?\n\nIf the service detects this network again, it will be added back as Unclassified.";
        var answer = MessageBox.Show(
            this,
            message,
            "Delete network",
            MessageBoxButton.YesNo,
            linkedDayCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            await RunUiActionAsync(token => _viewModel.DeleteNetworkAsync(selected, token));
        }
    }

    private async void AddWorkSession_Click(object sender, RoutedEventArgs e) =>
        await OpenWorkSessionEditorAsync(null);

    private async void EditWorkSession_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedWorkSession is null)
        {
            MessageBox.Show(this, "Select a work session to edit.", "Work session", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await OpenWorkSessionEditorAsync(_viewModel.SelectedWorkSession);
    }

    private async void DailySessionsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedWorkSession is not null)
        {
            await OpenWorkSessionEditorAsync(_viewModel.SelectedWorkSession);
        }
    }

    private void DailySessionsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource);
        if (row is null)
        {
            return;
        }

        if (!row.IsSelected)
        {
            DailySessionsGrid.SelectedItems.Clear();
            row.IsSelected = true;
        }

        row.Focus();
    }

    private void DailySessionActionsMenu_Opened(object sender, RoutedEventArgs e)
    {
        var selectionCount = DailySessionsGrid.SelectedItems.Count;
        EditSessionMenuItem.IsEnabled = selectionCount == 1;
        SplitSessionMenuItem.IsEnabled = selectionCount == 1;
        DeleteSessionMenuItem.IsEnabled = selectionCount == 1;
        MergeSessionMenuItem.IsEnabled = selectionCount >= 2;
    }

    private async void SplitWorkSession_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedWorkSession is null)
        {
            MessageBox.Show(this, "Select a work session to split.", "Work session", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var editor = new SplitWorkSessionWindow(_viewModel.SelectedWorkSession.Record)
        {
            Owner = this
        };
        if (editor.ShowDialog() == true)
        {
            await RunUiActionAsync(token => _viewModel.SplitSelectedWorkSessionAsync(
                editor.FirstEnd,
                editor.SecondStart,
                editor.FirstBreakMinutes,
                editor.SecondBreakMinutes,
                token));
        }
    }

    private async void MergeWorkSession_Click(object sender, RoutedEventArgs e)
    {
        var selectedSessions = DailySessionsGrid.SelectedItems
            .OfType<WorkSessionRowViewModel>()
            .OrderBy(session => session.Record.StartTimeUtc)
            .ToArray();
        if (selectedSessions.Length < 2)
        {
            MessageBox.Show(
                this,
                "Select two or more consecutive work sessions to merge. Use Ctrl-click or Shift-click to select multiple rows.",
                "Merge work sessions",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Merge the {selectedSessions.Length} selected work sessions? Any gaps between them will become break time.",
            "Merge work sessions",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            await RunUiActionAsync(token => _viewModel.MergeWorkSessionsAsync(selectedSessions, token));
        }
    }

    private async void DeleteWorkSession_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedWorkSession is null)
        {
            MessageBox.Show(this, "Select a work session to delete.", "Work session", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            "Delete the selected work session? The underlying computer-activity summary remains in the protected daily record.",
            "Delete work session",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes)
        {
            await RunUiActionAsync(_viewModel.DeleteSelectedWorkSessionAsync);
        }
    }

    private async void ConfirmWorkSessions_Click(object sender, RoutedEventArgs e) =>
        await RunUiActionAsync(_viewModel.ConfirmDailyWorkSessionsAsync);

    private async void ExportHtml_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export printable usage report",
            Filter = "HTML report (*.html)|*.html",
            FileName = $"work-location-report-{DateTime.Today:yyyy-MM-dd}.html",
            AddExtension = true,
            DefaultExt = ".html"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await RunUiActionAsync(token => _viewModel.ExportHtmlAsync(dialog.FileName, token));
        }
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export usage report CSV files",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"work-location-report-{DateTime.Today:yyyy-MM-dd}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog(this) == true)
        {
            await RunUiActionAsync(async token =>
            {
                var paths = await _viewModel.ExportCsvAsync(dialog.FileName, token);
                MessageBox.Show(
                    this,
                    $"Created:\n{string.Join("\n", paths)}",
                    "Report exported",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }
    }

    private async void SaveWorkSchedule_Click(object sender, RoutedEventArgs e) =>
        await RunUiActionAsync(_viewModel.SaveWorkScheduleAsync);

    private async void AddPublicHoliday_Click(object sender, RoutedEventArgs e) =>
        await RunUiActionAsync(_viewModel.SavePublicHolidayAsync);

    private void EditPublicHoliday_Click(object sender, RoutedEventArgs e) =>
        RunUiAction(_viewModel.BeginEditSelectedPublicHoliday);

    private void PublicHolidaysGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridRow>((DependencyObject)e.OriginalSource) is not null)
        {
            RunUiAction(_viewModel.BeginEditSelectedPublicHoliday);
        }
    }

    private void CancelPublicHolidayEdit_Click(object sender, RoutedEventArgs e) =>
        _viewModel.CancelPublicHolidayEdit();

    private async void DeletePublicHoliday_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.SelectedPublicHoliday;
        if (selected is null)
        {
            MessageBox.Show(
                this,
                "Select a Holiday to delete.",
                "Delete Holiday",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Delete {selected.DateDisplay}{(selected.Record.Name.Length > 0 ? $" ({selected.Record.Name})" : string.Empty)}?\n\nThe date will return to its normal workday setting. No recorded work sessions will be deleted.",
            "Delete Holiday",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            await RunUiActionAsync(_viewModel.DeleteSelectedPublicHolidayAsync);
        }
    }

    private async Task ClassifyAsync(LocationType locationType) =>
        await RunUiActionAsync(token => _viewModel.ClassifySelectedNetworkAsync(locationType, token));

    private async Task OpenWorkSessionEditorAsync(WorkSessionRowViewModel? selected)
    {
        if (_viewModel.DailyDetailDate is null)
        {
            MessageBox.Show(this, "Choose a daily-detail date first.", "Work session", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var initialLocation = selected?.Location switch
        {
            nameof(LocationType.Home) => LocationType.Home,
            nameof(LocationType.Office) => LocationType.Office,
            nameof(LocationType.Other) => LocationType.Other,
            _ => LocationType.Unknown
        };
        if (selected is null)
        {
            initialLocation = LocationType.Home;
        }

        var editor = new WorkSessionEditorWindow(
            DateOnly.FromDateTime(_viewModel.DailyDetailDate.Value),
            selected?.Record,
            initialLocation)
        {
            Owner = this
        };
        if (editor.ShowDialog() == true)
        {
            await RunUiActionAsync(token => _viewModel.SaveWorkSessionAsync(
                selected?.Record,
                editor.StartTime,
                editor.EndTime,
                editor.BreakMinutes,
                editor.Location,
                editor.SessionNote,
                token));
        }
    }

    private async Task RunUiActionAsync(Func<CancellationToken, Task> action)
    {
        try
        {
            await action(CancellationToken.None);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Work Location Tracker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RunUiAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Work Location Tracker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private async Task RunPeriodNavigationAsync(Func<CancellationToken, Task> action)
    {
        if (_isPeriodNavigationInProgress)
        {
            return;
        }

        _isPeriodNavigationInProgress = true;
        try
        {
            await RunUiActionAsync(action);
        }
        finally
        {
            _isPeriodNavigationInProgress = false;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject child)
        where T : DependencyObject
    {
        DependencyObject? current = child;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
