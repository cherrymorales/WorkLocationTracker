using System.Globalization;
using System.Windows;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Monitor;

public partial class SplitWorkSessionWindow : Window
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-AU");
    private readonly DateTime _originalStartLocal;
    private readonly DateTime _originalEndLocal;

    public SplitWorkSessionWindow(WorkSessionRecord session)
    {
        InitializeComponent();
        var localStart = session.StartTimeUtc.ToLocalTime();
        var localEnd = session.EndTimeUtc.ToLocalTime();
        _originalStartLocal = localStart.DateTime;
        _originalEndLocal = localEnd.DateTime;
        SessionRangeText.Text = $"Current session: {localStart:h:mm tt} to {localEnd:h:mm tt}";

        var totalMinutes = (localEnd - localStart).TotalMinutes;
        var workMinutes = Math.Max(2, totalMinutes - session.BreakMinutes);
        var firstEnd = localStart.AddMinutes(workMinutes / 2);
        var secondStart = firstEnd.AddMinutes(Math.Max(1, session.BreakMinutes));
        if (secondStart >= localEnd)
        {
            secondStart = localEnd.AddMinutes(-1);
        }

        FirstEndText.Text = firstEnd.ToString("h:mm tt", DisplayCulture);
        SecondStartText.Text = secondStart.ToString("h:mm tt", DisplayCulture);
    }

    public TimeOnly FirstEnd { get; private set; }

    public TimeOnly SecondStart { get; private set; }

    public int FirstBreakMinutes { get; private set; }

    public int SecondBreakMinutes { get; private set; }

    private void Split_Click(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParse(FirstEndText.Text, DisplayCulture, out var firstEnd) ||
            !TimeOnly.TryParse(SecondStartText.Text, DisplayCulture, out var secondStart))
        {
            ShowWarning("Enter times such as 1:00 am and 7:00 am.");
            return;
        }

        var firstEndLocal = _originalStartLocal.Date.Add(firstEnd.ToTimeSpan());
        var secondStartLocal = _originalStartLocal.Date.Add(secondStart.ToTimeSpan());
        if (firstEndLocal <= _originalStartLocal ||
            secondStartLocal < firstEndLocal ||
            secondStartLocal >= _originalEndLocal)
        {
            ShowWarning("The first end and second start must be ordered within the original session.");
            return;
        }

        if (!TryReadBreak(FirstBreakText.Text, firstEndLocal - _originalStartLocal, out var firstBreak) ||
            !TryReadBreak(SecondBreakText.Text, _originalEndLocal - secondStartLocal, out var secondBreak))
        {
            ShowWarning("Each break must be a whole number that is shorter than its session.");
            return;
        }

        FirstEnd = firstEnd;
        SecondStart = secondStart;
        FirstBreakMinutes = firstBreak;
        SecondBreakMinutes = secondBreak;
        DialogResult = true;
    }

    private static bool TryReadBreak(string value, TimeSpan duration, out int breakMinutes) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out breakMinutes) &&
        breakMinutes >= 0 &&
        breakMinutes < duration.TotalMinutes;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowWarning(string message) => MessageBox.Show(
        this,
        message,
        "Split work session",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
}
