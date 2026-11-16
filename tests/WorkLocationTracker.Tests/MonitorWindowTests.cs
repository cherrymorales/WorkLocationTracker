using System.Windows.Threading;
using Microsoft.Data.Sqlite;

namespace WorkLocationTracker.Tests;

public sealed class MonitorWindowTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "WorkLocationTracker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void MainWindow_CompletesDeferredLayoutAndBindingWithoutCrashing()
    {
        Exception? failure = null;
        var previousDataDirectory = Environment.GetEnvironmentVariable("WORK_LOCATION_TRACKER_DATA_DIR");
        Environment.SetEnvironmentVariable("WORK_LOCATION_TRACKER_DATA_DIR", _temporaryDirectory);

        var thread = new Thread(() =>
        {
            try
            {
                var application = new WorkLocationTracker.Monitor.App
                {
                    ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
                };
                application.InitializeComponent();
                application.DispatcherUnhandledException += (_, eventArgs) =>
                {
                    failure = eventArgs.Exception;
                    eventArgs.Handled = true;
                };

                var window = new WorkLocationTracker.Monitor.MainWindow();
                window.Show();
                window.UpdateLayout();
                Assert.NotNull(window.Icon);

                var tabs = Assert.IsType<System.Windows.Controls.TabControl>(window.FindName("MainTabs"));
                var dashboardTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("DashboardTab"));
                var dashboardLayout = Assert.IsType<System.Windows.Controls.Grid>(window.FindName("DashboardLayoutGrid"));
                var openDailyDetailButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("OpenDailyDetailButton"));
                var networkTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("NetworkTab"));
                var networkLayout = Assert.IsType<System.Windows.Controls.Grid>(window.FindName("NetworkLocationLayoutGrid"));
                var networkGrid = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName("NetworkLocationsGrid"));
                var dailyTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("DailyDetailTab"));
                var dailyHeading = Assert.IsType<System.Windows.Controls.TextBlock>(
                    window.FindName("DailyWorkSessionsHeadingText"));
                var dailyGrid = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName("DailySessionsGrid"));
                var dailyPrimaryActions = Assert.IsType<System.Windows.Controls.StackPanel>(
                    window.FindName("DailyPrimaryActionsPanel"));
                var monthlyTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("MonthlyRecordsTab"));
                var monthlyGrid = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName("MonthlyRecordsGrid"));
                var exportTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("ExportTab"));
                var settingsTab = Assert.IsType<System.Windows.Controls.TabItem>(window.FindName("SettingsTab"));
                var deleteNetworkButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("DeleteNetworkButton"));
                var previousDailyDateButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("PreviousDailyDateButton"));
                var nextDailyDateButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("NextDailyDateButton"));
                var todayDailyDateButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("TodayDailyDateButton"));
                var previousMonthButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("PreviousMonthButton"));
                var nextMonthButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("NextMonthButton"));
                var currentMonthButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("CurrentMonthButton"));
                Assert.Equal(6, tabs.Items.Count);
                Assert.Equal(6, dashboardLayout.RowDefinitions.Count);
                Assert.All(
                    dashboardLayout.RowDefinitions,
                    row => Assert.Equal(System.Windows.GridUnitType.Auto, row.Height.GridUnitType));
                Assert.Equal(
                    new[] { "Dashboard", "Daily detail", "Monthly records", "Network", "Export", "Settings" },
                    tabs.Items.Cast<System.Windows.Controls.TabItem>().Select(tab => tab.Header).ToArray());
                Assert.Equal("Network", networkTab.Header);
                Assert.Equal("Open Daily detail", openDailyDetailButton.Content);
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("WeekTargetDaysText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("WeekTargetLocationsText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("WeekTotalHoursText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("WeekAdditionalHoursText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("WeekEventsText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("MonthTargetDaysText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("MonthTargetLocationsText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("MonthTotalHoursText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("MonthAdditionalHoursText"));
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("MonthEventsText"));
                Assert.Equal(System.Windows.GridUnitType.Star, networkLayout.RowDefinitions[1].Height.GridUnitType);
                Assert.True(double.IsNaN(networkGrid.Height));
                tabs.SelectedItem = networkTab;
                window.UpdateLayout();
                Assert.True(networkGrid.ActualWidth > 500, "The network table did not stretch across the Network tab.");
                Assert.Equal("Daily detail", dailyTab.Header);
                Assert.StartsWith("Daily work sessions - ", dailyHeading.Text, StringComparison.Ordinal);
                Assert.Equal(8, dailyGrid.Columns.Count);
                Assert.Equal(System.Windows.Controls.DataGridSelectionMode.Extended, dailyGrid.SelectionMode);
                Assert.Collection(
                    dailyPrimaryActions.Children.Cast<System.Windows.Controls.Button>(),
                    button => Assert.Equal("Add session", button.Content),
                    button => Assert.Equal("Confirm all", button.Content));
                var sessionMenu = Assert.IsType<System.Windows.Controls.ContextMenu>(dailyGrid.ContextMenu);
                var sessionMenuItems = sessionMenu.Items
                    .OfType<System.Windows.Controls.MenuItem>()
                    .Select(item => item.Header)
                    .ToArray();
                Assert.Equal(new[] { "Edit session", "Split session", "Merge selected sessions", "Delete session" }, sessionMenuItems);
                Assert.Equal("Monthly records", monthlyTab.Header);
                Assert.Equal(10, monthlyGrid.Columns.Count);
                Assert.Equal(
                    new[] { "Date", "Start", "End", "Break", "Home", "Office", "Other", "Unclassified", "Additional", "Total" },
                    monthlyGrid.Columns.Select(column => column.Header).ToArray());
                Assert.Equal("Export", exportTab.Header);
                Assert.Equal("Settings", settingsTab.Header);
                Assert.Equal("Delete network", deleteNetworkButton.Content);
                Assert.IsType<System.Windows.Shapes.Path>(previousDailyDateButton.Content);
                Assert.IsType<System.Windows.Shapes.Path>(nextDailyDateButton.Content);
                Assert.Equal("Today", todayDailyDateButton.Content);
                Assert.IsType<System.Windows.Shapes.Path>(previousMonthButton.Content);
                Assert.IsType<System.Windows.Shapes.Path>(nextMonthButton.Content);
                Assert.Equal("Current", currentMonthButton.Content);
                Assert.False(nextDailyDateButton.IsEnabled);
                Assert.False(nextMonthButton.IsEnabled);
                var viewModel = Assert.IsType<WorkLocationTracker.Monitor.ViewModels.MainViewModel>(window.DataContext);
                viewModel.DailyDetailDate = DateTime.Today.AddDays(-1);
                window.UpdateLayout();
                Assert.Equal(
                    $"Daily work sessions - {DateTime.Today.AddDays(-1):d MMMM yyyy, dddd}",
                    dailyHeading.Text);
                var previousMonth = DateTime.Today.AddMonths(-1);
                viewModel.SelectedYear = previousMonth.Year;
                viewModel.SelectedMonthIndex = previousMonth.Month - 1;
                window.UpdateLayout();
                Assert.True(nextDailyDateButton.IsEnabled);
                Assert.True(nextMonthButton.IsEnabled);
                Assert.IsType<System.Windows.Controls.Button>(window.FindName("ExportHtmlButton"));
                Assert.IsType<System.Windows.Controls.Button>(window.FindName("ExportCsvButton"));
                Assert.Same(application.FindResource("MainNavigationTabs"), tabs.Style);

                tabs.SelectedItem = dashboardTab;
                window.UpdateLayout();
                AssertUsesPageForeground(window, "TodayTotalWorkHoursText");
                AssertUsesPageForeground(window, "WeekTotalHoursText");
                AssertUsesPageForeground(window, "MonthTotalHoursText");
                Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("TodayTargetStatusText"));

                tabs.SelectedItem = dailyTab;
                var targetDate = new DateOnly(2026, 9, 7);
                viewModel.UpdateDailyTargetProgress(targetDate, 0);
                window.UpdateLayout();
                AssertUsesPageForeground(window, "DailySessionTotalHoursText");
                var dailyTarget = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("DailyTargetStatusText"));
                var dailyTargetCompletion = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("DailyTargetCompletionText"));
                var dailyAdditional = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("DailyAdditionalHoursText"));
                Assert.Equal("7 h 36 min remaining", dailyTarget.Text);
                Assert.Equal("Start work to estimate", dailyTargetCompletion.Text);
                Assert.Equal("0 h 00 min", dailyAdditional.Text);
                Assert.Equal(
                    System.Windows.Media.Color.FromRgb(0xB4, 0x23, 0x18),
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(dailyTarget.Foreground).Color);
                Assert.Equal(
                    System.Windows.Media.Color.FromRgb(0xB4, 0x53, 0x09),
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(dailyAdditional.Foreground).Color);
                var completionSession = new WorkLocationTracker.Core.Reporting.WorkSessionReport(
                    targetDate,
                    "completion-session",
                    new DateTimeOffset(2026, 9, 7, 8, 0, 0, TimeSpan.FromHours(10)),
                    new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.FromHours(10)),
                    0,
                    2,
                    "Home",
                    WorkLocationTracker.Core.Models.WorkSessionStatus.Confirmed,
                    WorkLocationTracker.Core.Models.WorkSessionSource.Manual,
                    string.Empty);
                viewModel.UpdateDailyTargetProgress(
                    targetDate,
                    2,
                    new[] { completionSession },
                    completionSession.EndTime);
                window.UpdateLayout();
                Assert.Equal("Estimated finish: 3:36 pm", dailyTargetCompletion.Text, ignoreCase: true);

                viewModel.UpdateDailyTargetProgress(targetDate, 7.6);
                window.UpdateLayout();
                Assert.Equal("Target met", dailyTarget.Text);
                Assert.Equal("Target completed", dailyTargetCompletion.Text);
                Assert.Equal(
                    System.Windows.Media.Color.FromRgb(0x14, 0x7A, 0x65),
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(dailyTarget.Foreground).Color);
                AssertUsesPageForeground(window, "DailyBreakTotalText");

                tabs.SelectedItem = monthlyTab;
                window.UpdateLayout();
                var selectedTabSurface = Assert.IsType<System.Windows.Controls.Border>(
                    System.Windows.Media.VisualTreeHelper.GetChild(monthlyTab, 0));
                var selectedBrush = Assert.IsType<System.Windows.Media.SolidColorBrush>(selectedTabSurface.Background);
                Assert.Equal(System.Windows.Media.Color.FromRgb(0x17, 0x3D, 0x36), selectedBrush.Color);

                AssertUsesPageForeground(window, "MonthlyHomeHoursText");
                AssertUsesPageForeground(window, "MonthlyOfficeHoursText");
                var monthlyAdditional = Assert.IsType<System.Windows.Controls.TextBlock>(
                    window.FindName("MonthlyAdditionalHoursText"));
                Assert.Equal(
                    System.Windows.Media.Color.FromRgb(0xB4, 0x53, 0x09),
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(monthlyAdditional.Foreground).Color);

                tabs.SelectedItem = settingsTab;
                window.UpdateLayout();
                var workingDays = Assert.IsType<System.Windows.Controls.ItemsControl>(window.FindName("WorkingDaysList"));
                var publicHolidays = Assert.IsType<System.Windows.Controls.DataGrid>(window.FindName("PublicHolidaysGrid"));
                Assert.Equal(7, workingDays.Items.Count);
                Assert.Equal(4, publicHolidays.Columns.Count);
                Assert.Equal("7", Assert.IsType<System.Windows.Controls.TextBox>(window.FindName("DailyTargetHoursTextBox")).Text);
                Assert.Equal("36", Assert.IsType<System.Windows.Controls.TextBox>(window.FindName("DailyTargetMinutesTextBox")).Text);
                Assert.IsType<System.Windows.Controls.Button>(window.FindName("SaveWorkScheduleButton"));
                var addHolidayButton = Assert.IsType<System.Windows.Controls.Button>(
                    window.FindName("AddPublicHolidayButton"));
                Assert.Equal("Add date", addHolidayButton.Content);
                Assert.IsType<System.Windows.Controls.Button>(window.FindName("DeletePublicHolidayButton"));
                Assert.IsType<System.Windows.Controls.Button>(window.FindName("EditPublicHolidayButton"));
                var cancelHolidayEditButton = Assert.IsType<System.Windows.Controls.Button>(
                    window.FindName("CancelPublicHolidayEditButton"));
                Assert.False(cancelHolidayEditButton.IsEnabled);
                Assert.IsType<System.Windows.Controls.ComboBox>(window.FindName("NewPublicHolidayTypeComboBox"));
                Assert.IsType<System.Windows.Controls.TextBox>(window.FindName("NewHolidayLeaveHoursTextBox"));
                Assert.IsType<System.Windows.Controls.TextBox>(window.FindName("NewHolidayLeaveMinutesTextBox"));

                viewModel.UpdateDailyTargetProgress(new DateOnly(2026, 9, 5), 2);
                tabs.SelectedItem = dailyTab;
                window.UpdateLayout();
                Assert.Equal("Non-working day", dailyTarget.Text);
                Assert.Equal("No target for this date", dailyTargetCompletion.Text);
                Assert.Equal("2 h 00 min", dailyAdditional.Text);
                Assert.Equal(
                    System.Windows.Media.Color.FromRgb(0x14, 0x7A, 0x65),
                    Assert.IsType<System.Windows.Media.SolidColorBrush>(dailyTarget.Foreground).Color);

                var sessionEditor = new WorkLocationTracker.Monitor.WorkSessionEditorWindow(
                    new DateOnly(2026, 9, 4))
                {
                    Height = 430
                };
                sessionEditor.Show();
                sessionEditor.UpdateLayout();
                Assert.NotNull(sessionEditor.Icon);
                AssertButtonIsInsideWindow(sessionEditor, "SaveSessionButton");
                sessionEditor.Close();

                var splitSession = new WorkLocationTracker.Core.Models.WorkSessionRecord(
                    "session-to-split",
                    new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero),
                    360,
                    new Dictionary<string, int> { ["home"] = 21600 },
                    WorkLocationTracker.Core.Models.LocationType.Home,
                    WorkLocationTracker.Core.Models.WorkSessionStatus.Suggested,
                    WorkLocationTracker.Core.Models.WorkSessionSource.ImportedSummary,
                    string.Empty);
                var splitEditor = new WorkLocationTracker.Monitor.SplitWorkSessionWindow(splitSession)
                {
                    Height = 420
                };
                splitEditor.Show();
                splitEditor.UpdateLayout();
                Assert.NotNull(splitEditor.Icon);
                AssertButtonIsInsideWindow(splitEditor, "SplitConfirmButton");
                splitEditor.Close();

                var frame = new DispatcherFrame();
                window.Dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);

                window.Close();
                application.Shutdown();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        var completed = thread.Join(TimeSpan.FromSeconds(10));
        Environment.SetEnvironmentVariable("WORK_LOCATION_TRACKER_DATA_DIR", previousDataDirectory);
        Assert.True(completed, "The monitor window did not complete its layout check.");
        Assert.Null(failure);
    }

    private static void AssertUsesPageForeground(
        WorkLocationTracker.Monitor.MainWindow window,
        string elementName)
    {
        var textBlock = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName(elementName));
        var foreground = Assert.IsType<System.Windows.Media.SolidColorBrush>(textBlock.Foreground);

        Assert.Equal(System.Windows.Media.Color.FromRgb(0x17, 0x3D, 0x36), foreground.Color);
        Assert.NotEqual(System.Windows.Media.Colors.White, foreground.Color);
    }

    private static void AssertButtonIsInsideWindow(System.Windows.Window window, string elementName)
    {
        var button = Assert.IsType<System.Windows.Controls.Button>(window.FindName(elementName));
        var bounds = button
            .TransformToAncestor(window)
            .TransformBounds(new System.Windows.Rect(new System.Windows.Point(), button.RenderSize));

        Assert.True(button.ActualHeight > 0, $"{elementName} was not arranged.");
        Assert.InRange(bounds.Top, 0, window.RenderSize.Height);
        Assert.InRange(bounds.Bottom, 0, window.RenderSize.Height);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
