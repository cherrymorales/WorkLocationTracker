using System.Globalization;
using System.Windows;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Monitor;

public partial class WorkSessionEditorWindow : Window
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-AU");

    public WorkSessionEditorWindow(
        DateOnly date,
        WorkSessionRecord? session = null,
        LocationType initialLocation = LocationType.Home)
    {
        InitializeComponent();
        LocationCombo.ItemsSource = new[]
        {
            LocationType.Home,
            LocationType.Office,
            LocationType.Other,
            LocationType.Unknown
        };
        LocationCombo.SelectedItem = session?.LocationOverride ?? initialLocation;
        DateText.Text = date.ToString("dddd d MMMM yyyy", DisplayCulture);
        HeadingText.Text = session is null ? "Add work session" : "Review work session";

        var start = session?.StartTimeUtc.ToLocalTime().TimeOfDay ?? new TimeSpan(9, 0, 0);
        var end = session?.EndTimeUtc.ToLocalTime().TimeOfDay ?? new TimeSpan(17, 0, 0);
        StartTimeText.Text = DateTime.Today.Add(start).ToString("h:mm tt", DisplayCulture);
        EndTimeText.Text = DateTime.Today.Add(end).ToString("h:mm tt", DisplayCulture);
        BreakMinutesText.Text = (session?.BreakMinutes ?? 0).ToString(CultureInfo.InvariantCulture);
        NoteText.Text = session?.Note ?? string.Empty;
    }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public int BreakMinutes { get; private set; }

    public LocationType Location { get; private set; }

    public string SessionNote { get; private set; } = string.Empty;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParse(StartTimeText.Text, DisplayCulture, out var startTime) ||
            !TimeOnly.TryParse(EndTimeText.Text, DisplayCulture, out var endTime))
        {
            ShowWarning("Enter start and end times such as 7:00 am and 5:00 pm.");
            return;
        }

        if (endTime <= startTime && endTime != TimeOnly.MinValue)
        {
            ShowWarning("End time must be later than start time. Midnight is allowed as the end of the selected date.");
            return;
        }

        var duration = endTime == TimeOnly.MinValue && startTime > TimeOnly.MinValue
            ? TimeSpan.FromDays(1) - startTime.ToTimeSpan()
            : endTime - startTime;
        if (!int.TryParse(BreakMinutesText.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var breakMinutes) ||
            breakMinutes < 0 || breakMinutes >= duration.TotalMinutes)
        {
            ShowWarning("Break minutes must be a whole number that is shorter than the session.");
            return;
        }

        if (LocationCombo.SelectedItem is not LocationType location)
        {
            ShowWarning("Choose the location for this work session.");
            return;
        }

        StartTime = startTime;
        EndTime = endTime;
        BreakMinutes = breakMinutes;
        Location = location;
        SessionNote = NoteText.Text.Trim();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowWarning(string message) => MessageBox.Show(
        this,
        message,
        "Work session",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
}
