using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using WorkLocationTracker.Core;
using WorkLocationTracker.Core.Data;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ITrackerRepository _repository;
    private readonly UsageReportBuilder _reportBuilder;
    private readonly ReportExporter _reportExporter;
    private string _serviceStatus = "Checking…";
    private string _currentNetworkName = "Waiting for the first service sample…";
    private string _currentNetworkClassification = "Unknown";
    private string _todayHeading = DateTime.Today.ToString("dddd d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU"));
    private string _todayTotalWorkHours = "0 h 00 min";
    private string _todayTargetStatus = "7 h 36 min remaining";
    private string _todayTargetCompletionText = "Start work to estimate";
    private bool _isTodayTargetMet;
    private string _todayAdditionalHours = "0 h 00 min";
    private string _todayBreakTotal = "0 h 00 min";
    private string _weekHeading = string.Empty;
    private string _weekTargetDays = "0 / 5";
    private string _weekTargetLocations = "Home: 0  |  Office: 0";
    private string _weekTotalHours = "0 h 00 min";
    private string _weekAdditionalHours = "0 h 00 min";
    private string _weekEvents = string.Empty;
    private string _monthHeading = string.Empty;
    private string _monthTargetDays = "0 / 22";
    private string _monthTargetLocations = "Home: 0  |  Office: 0";
    private string _monthTotalHours = "0 h 00 min";
    private string _monthAdditionalHours = "0 h 00 min";
    private string _monthEvents = string.Empty;
    private string _statusMessage = "Ready";
    private NetworkRowViewModel? _selectedNetwork;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private string _hourlyRateText = "0.00";
    private int _selectedMonthIndex = DateTime.Today.Month - 1;
    private int _selectedYear = DateTime.Today.Year;
    private string _monthlyHomeHours = "0 h 00 min";
    private string _monthlyOfficeHours = "0 h 00 min";
    private string _monthlyAdditionalHours = "0 h 00 min";
    private string _monthlyTotalHours = "0 h 00 min";
    private DateTime? _dailyDetailDate = DateTime.Today;
    private WorkSessionRowViewModel? _selectedWorkSession;
    private string _dailySessionTotalHours = "0 h 00 min";
    private string _dailyTargetStatus = "7 h 36 min remaining";
    private string _dailyTargetExplanation = "Configured daily target: 7 h 36 min.";
    private string _dailyTargetCompletionText = "Start work to estimate";
    private bool _isDailyTargetMet;
    private string _dailyAdditionalHours = "0 h 00 min";
    private string _dailyBreakTotal = "0 h 00 min";
    private string _dailyReviewStatus = "No sessions";
    private WorkScheduleSettings _workSchedule = WorkScheduleSettings.Default;
    private string _dailyTargetHoursText = "7";
    private string _dailyTargetMinutesText = "36";
    private DateTime? _newPublicHolidayDate = DateTime.Today;
    private string _newPublicHolidayName = string.Empty;
    private HolidayType _newPublicHolidayType = HolidayType.PublicHoliday;
    private string _newHolidayLeaveHoursText = "7";
    private string _newHolidayLeaveMinutesText = "36";
    private DateOnly? _editingPublicHolidayDate;
    private PublicHolidayRowViewModel? _selectedPublicHoliday;

    public MainViewModel()
        : this(
            new TrackerRepository(new AppPaths()),
            new UsageReportBuilder(),
            new ReportExporter())
    {
    }

    internal MainViewModel(
        ITrackerRepository repository,
        UsageReportBuilder reportBuilder,
        ReportExporter reportExporter)
    {
        _repository = repository;
        _reportBuilder = reportBuilder;
        _reportExporter = reportExporter;

        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Monday", WorkingDays.Monday, true));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Tuesday", WorkingDays.Tuesday, true));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Wednesday", WorkingDays.Wednesday, true));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Thursday", WorkingDays.Thursday, true));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Friday", WorkingDays.Friday, true));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Saturday", WorkingDays.Saturday, false));
        WorkingDayOptions.Add(new WorkingDayOptionViewModel("Sunday", WorkingDays.Sunday, false));

        var today = DateTime.Today;
        FromDate = today.Month >= 7
            ? new DateTime(today.Year, 7, 1)
            : new DateTime(today.Year - 1, 7, 1);
        ToDate = today;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NetworkRowViewModel> Networks { get; } = new();

    public ObservableCollection<MonthlyUsageRowViewModel> MonthlyRows { get; } = new();

    public ObservableCollection<WorkSessionRowViewModel> WorkSessions { get; } = new();

    public ObservableCollection<WorkingDayOptionViewModel> WorkingDayOptions { get; } = new();

    public ObservableCollection<PublicHolidayRowViewModel> PublicHolidays { get; } = new();

    public IReadOnlyList<HolidayTypeOptionViewModel> HolidayTypeOptions { get; } =
        Enum.GetValues<HolidayType>()
            .Select(type => new HolidayTypeOptionViewModel(type.ToDisplayName(), type))
            .ToArray();

    public IReadOnlyList<LocationType> WorkSessionLocationOptions { get; } =
        new[] { LocationType.Home, LocationType.Office, LocationType.Other, LocationType.Unknown };

    public IReadOnlyList<string> MonthNames { get; } =
        CultureInfo.GetCultureInfo("en-AU").DateTimeFormat.MonthNames.Take(12).ToArray();

    public IReadOnlyList<int> YearOptions { get; } =
        Enumerable.Range(DateTime.Today.Year - 10, 12).Reverse().ToArray();

    public string ServiceStatus
    {
        get => _serviceStatus;
        private set => SetField(ref _serviceStatus, value);
    }

    public string CurrentNetworkName
    {
        get => _currentNetworkName;
        private set => SetField(ref _currentNetworkName, value);
    }

    public string CurrentNetworkClassification
    {
        get => _currentNetworkClassification;
        private set => SetField(ref _currentNetworkClassification, value);
    }

    public string TodayHeading
    {
        get => _todayHeading;
        private set => SetField(ref _todayHeading, value);
    }

    public string TodayTotalWorkHours
    {
        get => _todayTotalWorkHours;
        private set => SetField(ref _todayTotalWorkHours, value);
    }

    public string TodayTargetStatus
    {
        get => _todayTargetStatus;
        private set => SetField(ref _todayTargetStatus, value);
    }

    public string TodayTargetCompletionText
    {
        get => _todayTargetCompletionText;
        private set => SetField(ref _todayTargetCompletionText, value);
    }

    public bool IsTodayTargetMet
    {
        get => _isTodayTargetMet;
        private set => SetField(ref _isTodayTargetMet, value);
    }

    public string TodayAdditionalHours
    {
        get => _todayAdditionalHours;
        private set => SetField(ref _todayAdditionalHours, value);
    }

    public string TodayBreakTotal
    {
        get => _todayBreakTotal;
        private set => SetField(ref _todayBreakTotal, value);
    }

    public string WeekHeading
    {
        get => _weekHeading;
        private set => SetField(ref _weekHeading, value);
    }

    public string WeekTargetDays
    {
        get => _weekTargetDays;
        private set => SetField(ref _weekTargetDays, value);
    }

    public string WeekTargetLocations
    {
        get => _weekTargetLocations;
        private set => SetField(ref _weekTargetLocations, value);
    }

    public string WeekTotalHours
    {
        get => _weekTotalHours;
        private set => SetField(ref _weekTotalHours, value);
    }

    public string WeekAdditionalHours
    {
        get => _weekAdditionalHours;
        private set => SetField(ref _weekAdditionalHours, value);
    }

    public string WeekEvents
    {
        get => _weekEvents;
        private set => SetField(ref _weekEvents, value);
    }

    public string MonthHeading
    {
        get => _monthHeading;
        private set => SetField(ref _monthHeading, value);
    }

    public string MonthTargetDays
    {
        get => _monthTargetDays;
        private set => SetField(ref _monthTargetDays, value);
    }

    public string MonthTargetLocations
    {
        get => _monthTargetLocations;
        private set => SetField(ref _monthTargetLocations, value);
    }

    public string MonthTotalHours
    {
        get => _monthTotalHours;
        private set => SetField(ref _monthTotalHours, value);
    }

    public string MonthAdditionalHours
    {
        get => _monthAdditionalHours;
        private set => SetField(ref _monthAdditionalHours, value);
    }

    public string MonthEvents
    {
        get => _monthEvents;
        private set => SetField(ref _monthEvents, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public NetworkRowViewModel? SelectedNetwork
    {
        get => _selectedNetwork;
        set => SetField(ref _selectedNetwork, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set => SetField(ref _fromDate, value);
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set => SetField(ref _toDate, value);
    }

    public string HourlyRateText
    {
        get => _hourlyRateText;
        set => SetField(ref _hourlyRateText, value);
    }

    public int SelectedMonthIndex
    {
        get => _selectedMonthIndex;
        set
        {
            if (SetField(ref _selectedMonthIndex, value))
            {
                NotifyMonthNavigationChanged();
            }
        }
    }

    public int SelectedYear
    {
        get => _selectedYear;
        set
        {
            if (SetField(ref _selectedYear, value))
            {
                NotifyMonthNavigationChanged();
            }
        }
    }

    public bool CanMoveToPreviousMonth =>
        TryGetSelectedMonth(out var selectedMonth) && selectedMonth > EarliestSelectableMonth;

    public bool CanMoveToNextMonth =>
        TryGetSelectedMonth(out var selectedMonth) && selectedMonth < CurrentMonth;

    public string MonthlyHomeHours
    {
        get => _monthlyHomeHours;
        private set => SetField(ref _monthlyHomeHours, value);
    }

    public string MonthlyOfficeHours
    {
        get => _monthlyOfficeHours;
        private set => SetField(ref _monthlyOfficeHours, value);
    }

    public string MonthlyAdditionalHours
    {
        get => _monthlyAdditionalHours;
        private set => SetField(ref _monthlyAdditionalHours, value);
    }

    public string MonthlyTotalHours
    {
        get => _monthlyTotalHours;
        private set => SetField(ref _monthlyTotalHours, value);
    }

    public DateTime? DailyDetailDate
    {
        get => _dailyDetailDate;
        set
        {
            if (SetField(ref _dailyDetailDate, value?.Date))
            {
                NotifyDailyDateNavigationChanged();
            }
        }
    }

    public string DailyWorkSessionsHeading =>
        $"Daily work sessions - {(DailyDetailDate ?? DateTime.Today).ToString("d MMMM yyyy, dddd", CultureInfo.GetCultureInfo("en-AU"))}";

    public DateTime LatestSelectableDate => DateTime.Today;

    public bool CanMoveToPreviousDailyDate =>
        DailyDetailDate is { } date && date.Date > DateTime.MinValue.Date;

    public bool CanMoveToNextDailyDate =>
        DailyDetailDate is { } date && date.Date < DateTime.Today;

    public WorkSessionRowViewModel? SelectedWorkSession
    {
        get => _selectedWorkSession;
        set => SetField(ref _selectedWorkSession, value);
    }

    public string DailySessionTotalHours
    {
        get => _dailySessionTotalHours;
        private set => SetField(ref _dailySessionTotalHours, value);
    }

    public string DailyTargetStatus
    {
        get => _dailyTargetStatus;
        private set => SetField(ref _dailyTargetStatus, value);
    }

    public string DailyTargetExplanation
    {
        get => _dailyTargetExplanation;
        private set => SetField(ref _dailyTargetExplanation, value);
    }

    public string DailyTargetCompletionText
    {
        get => _dailyTargetCompletionText;
        private set => SetField(ref _dailyTargetCompletionText, value);
    }

    public bool IsDailyTargetMet
    {
        get => _isDailyTargetMet;
        private set => SetField(ref _isDailyTargetMet, value);
    }

    public string DailyAdditionalHours
    {
        get => _dailyAdditionalHours;
        private set => SetField(ref _dailyAdditionalHours, value);
    }

    public string DailyBreakTotal
    {
        get => _dailyBreakTotal;
        private set => SetField(ref _dailyBreakTotal, value);
    }

    public string DailyReviewStatus
    {
        get => _dailyReviewStatus;
        private set => SetField(ref _dailyReviewStatus, value);
    }

    public string DailyTargetHoursText
    {
        get => _dailyTargetHoursText;
        set => SetField(ref _dailyTargetHoursText, value);
    }

    public string DailyTargetMinutesText
    {
        get => _dailyTargetMinutesText;
        set => SetField(ref _dailyTargetMinutesText, value);
    }

    public DateTime? NewPublicHolidayDate
    {
        get => _newPublicHolidayDate;
        set => SetField(ref _newPublicHolidayDate, value?.Date);
    }

    public string NewPublicHolidayName
    {
        get => _newPublicHolidayName;
        set => SetField(ref _newPublicHolidayName, value);
    }

    public HolidayType NewPublicHolidayType
    {
        get => _newPublicHolidayType;
        set => SetField(ref _newPublicHolidayType, value);
    }

    public string NewHolidayLeaveHoursText
    {
        get => _newHolidayLeaveHoursText;
        set => SetField(ref _newHolidayLeaveHoursText, value);
    }

    public string NewHolidayLeaveMinutesText
    {
        get => _newHolidayLeaveMinutesText;
        set => SetField(ref _newHolidayLeaveMinutesText, value);
    }

    public PublicHolidayRowViewModel? SelectedPublicHoliday
    {
        get => _selectedPublicHoliday;
        set => SetField(ref _selectedPublicHoliday, value);
    }

    public bool IsEditingPublicHoliday => _editingPublicHolidayDate is not null;

    public string HolidaySaveButtonText => IsEditingPublicHoliday ? "Save changes" : "Add date";

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        await _repository.InitialiseAsync(cancellationToken);
        await LoadSettingsAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ServiceStatus = ReadServiceStatus();

        var networkRows = await _repository.GetNetworkLocationsAsync(cancellationToken);
        Networks.Clear();
        foreach (var network in networkRows)
        {
            Networks.Add(new NetworkRowViewModel(
                network.NetworkId,
                network.NetworkName,
                network.LocationType,
                network.LastSeenUtc.ToLocalTime().ToString("g", CultureInfo.GetCultureInfo("en-AU"))));
        }

        var latestNetwork = networkRows.FirstOrDefault();
        if (latestNetwork is not null)
        {
            CurrentNetworkName = latestNetwork.NetworkName;
            CurrentNetworkClassification = latestNetwork.LocationType.ToString();
            SelectedNetwork ??= Networks.FirstOrDefault();
        }
        else
        {
            CurrentNetworkName = "Waiting for the first service sample…";
            CurrentNetworkClassification = "Unknown";
            SelectedNetwork = null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekEnd = weekStart.AddDays(6);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var dashboardFrom = weekStart < monthStart ? weekStart : monthStart;
        var report = await BuildReportAsync(dashboardFrom, today, 0, cancellationToken);
        RefreshDashboard(today, weekStart, weekEnd, monthStart, monthEnd, report);
        await RefreshMonthlyTableAsync(cancellationToken);
        await RefreshDailyDetailAsync(cancellationToken);
        StatusMessage = $"Last refreshed {DateTime.Now:h:mm:ss tt}";
    }

    public async Task RefreshSelectedMonthAsync(CancellationToken cancellationToken = default)
    {
        await RefreshMonthlyTableAsync(cancellationToken);
        var monthName = MonthNames[SelectedMonthIndex];
        StatusMessage = $"Showing {monthName} {SelectedYear}.";
    }

    public async Task MoveSelectedMonthAsync(
        int monthOffset,
        CancellationToken cancellationToken = default)
    {
        if (monthOffset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(monthOffset), "Month navigation must move by one month.");
        }

        if (!TryGetSelectedMonth(out var selectedMonth))
        {
            throw new InvalidOperationException("Choose a valid month and year.");
        }

        var targetMonth = selectedMonth.AddMonths(monthOffset);
        targetMonth = targetMonth < EarliestSelectableMonth
            ? EarliestSelectableMonth
            : targetMonth > CurrentMonth
                ? CurrentMonth
                : targetMonth;
        SelectedYear = targetMonth.Year;
        SelectedMonthIndex = targetMonth.Month - 1;
        await RefreshSelectedMonthAsync(cancellationToken);
    }

    public async Task GoToCurrentMonthAsync(CancellationToken cancellationToken = default)
    {
        SelectedYear = CurrentMonth.Year;
        SelectedMonthIndex = CurrentMonth.Month - 1;
        await RefreshSelectedMonthAsync(cancellationToken);
    }

    public async Task MoveDailyDetailDateAsync(
        int dayOffset,
        CancellationToken cancellationToken = default)
    {
        if (dayOffset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(dayOffset), "Date navigation must move by one day.");
        }

        var selectedDate = DailyDetailDate?.Date ?? DateTime.Today;
        var targetDate = selectedDate.AddDays(dayOffset);
        DailyDetailDate = targetDate > DateTime.Today ? DateTime.Today : targetDate;
        await RefreshDailyDetailAsync(cancellationToken);
    }

    public async Task GoToTodayAsync(CancellationToken cancellationToken = default)
    {
        DailyDetailDate = DateTime.Today;
        await RefreshDailyDetailAsync(cancellationToken);
    }

    public async Task RefreshDailyDetailAsync(CancellationToken cancellationToken = default)
    {
        if (DailyDetailDate is null)
        {
            throw new InvalidOperationException("Choose a date for the daily detail.");
        }

        var date = DateOnly.FromDateTime(DailyDetailDate.Value);
        var dailyUsageTask = _repository.GetDailyUsageAsync(date, date, cancellationToken);
        var networksTask = _repository.GetNetworkLocationsAsync(cancellationToken);
        await Task.WhenAll(dailyUsageTask, networksTask);
        var dailyUsage = await dailyUsageTask;
        var report = _reportBuilder.Build(
            date,
            date,
            0,
            dailyUsage,
            await networksTask,
            Array.Empty<ManualAdjustment>());
        var sessionReports = report.DetailedSessions.ToDictionary(session => session.Id, StringComparer.Ordinal);
        var selectedId = SelectedWorkSession?.Record.Id;

        WorkSessions.Clear();
        foreach (var session in dailyUsage.SingleOrDefault()?.WorkSessions ?? Array.Empty<WorkSessionRecord>())
        {
            if (sessionReports.TryGetValue(session.Id, out var sessionReport))
            {
                WorkSessions.Add(new WorkSessionRowViewModel(session, sessionReport));
            }
        }

        SelectedWorkSession = WorkSessions.FirstOrDefault(item => item.Record.Id == selectedId)
            ?? WorkSessions.FirstOrDefault();
        var totalWorkHours = WorkSessions.Sum(session => session.WorkHours);
        DailySessionTotalHours = DurationFormatter.FormatHours(totalWorkHours);
        UpdateDailyTargetProgress(date, totalWorkHours, report.DetailedSessions);
        DailyBreakTotal = DurationFormatter.FormatMinutes(WorkSessions.Sum(session => session.BreakMinutes));
        var pendingCount = WorkSessions.Count(session => session.Record.Status == WorkSessionStatus.Suggested);
        DailyReviewStatus = WorkSessions.Count == 0
            ? "No sessions"
            : pendingCount == 0
                ? "Confirmed"
                : $"{pendingCount} to review";
        StatusMessage = $"Showing daily detail for {date:d MMM yyyy}.";
    }

    internal void UpdateDailyTargetProgress(
        DateOnly date,
        double totalWorkHours,
        IReadOnlyCollection<WorkSessionReport>? sessions = null,
        DateTimeOffset? now = null)
    {
        var display = BuildDailyTargetDisplay(
            date,
            totalWorkHours,
            sessions ?? Array.Empty<WorkSessionReport>(),
            now ?? DateTimeOffset.Now);
        DailyTargetStatus = display.Status;
        DailyTargetExplanation = display.Explanation;
        DailyTargetCompletionText = display.CompletionText;
        IsDailyTargetMet = display.IsMet;
        DailyAdditionalHours = display.AdditionalTime;
    }

    private void RefreshDashboard(
        DateOnly today,
        DateOnly weekStart,
        DateOnly weekEnd,
        DateOnly monthStart,
        DateOnly monthEnd,
        UsageReport report)
    {
        var sessions = report.DetailedSessions
            .Where(session => session.Date == today)
            .OrderBy(session => session.StartTime)
            .ToArray();

        var totalWorkHours = sessions.Sum(session => session.WorkHours);
        var targetDisplay = BuildDailyTargetDisplay(today, totalWorkHours, sessions, DateTimeOffset.Now);
        TodayHeading = today.ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU"));
        TodayTotalWorkHours = DurationFormatter.FormatHours(totalWorkHours);
        TodayTargetStatus = targetDisplay.Status;
        TodayTargetCompletionText = targetDisplay.CompletionText;
        IsTodayTargetMet = targetDisplay.IsMet;
        TodayAdditionalHours = targetDisplay.AdditionalTime;
        TodayBreakTotal = DurationFormatter.FormatMinutes(sessions.Sum(session => session.BreakMinutes));

        var holidays = PublicHolidays.Select(row => row.Record).ToArray();
        var week = PeriodProgressCalculator.Calculate(
            weekStart,
            weekEnd,
            today,
            _workSchedule,
            holidays,
            report.Days,
            report.DetailedSessions);
        var month = PeriodProgressCalculator.Calculate(
            monthStart,
            monthEnd,
            today,
            _workSchedule,
            holidays,
            report.Days,
            report.DetailedSessions);

        var australianCulture = CultureInfo.GetCultureInfo("en-AU");
        WeekHeading = $"{weekStart.ToString("d MMM", australianCulture)} – {weekEnd.ToString("d MMM yyyy", australianCulture)}";
        WeekTargetDays = $"{week.CompletedTargetDays} / {week.ScheduledWorkDays}";
        WeekTargetLocations = $"Home: {week.HomeCompletedTargetDays}  |  Office: {week.OfficeCompletedTargetDays}";
        WeekTotalHours = DurationFormatter.FormatHours(week.TotalWorkHours);
        WeekAdditionalHours = DurationFormatter.FormatMinutes(week.AdditionalMinutes);
        WeekEvents = FormatEvents(week.Events);

        MonthHeading = monthStart.ToString("MMMM yyyy", australianCulture);
        MonthTargetDays = $"{month.CompletedTargetDays} / {month.ScheduledWorkDays}";
        MonthTargetLocations = $"Home: {month.HomeCompletedTargetDays}  |  Office: {month.OfficeCompletedTargetDays}";
        MonthTotalHours = DurationFormatter.FormatHours(month.TotalWorkHours);
        MonthAdditionalHours = DurationFormatter.FormatMinutes(month.AdditionalMinutes);
        MonthEvents = FormatEvents(month.Events);
    }

    private DailyTargetDisplay BuildDailyTargetDisplay(
        DateOnly date,
        double totalWorkHours,
        IReadOnlyCollection<WorkSessionReport> sessions,
        DateTimeOffset now)
    {
        var effectiveTarget = WorkScheduleEvaluator.Evaluate(
            date,
            _workSchedule,
            PublicHolidays.Select(row => row.Record));
        var targetProgress = WorkdayTargetCalculator.Calculate(totalWorkHours, effectiveTarget.TargetMinutes);

        string status;
        string explanation;
        if (effectiveTarget.DayKind == ScheduleDayKind.PublicHoliday)
        {
            status = "Public Holiday";
            var holidayName = effectiveTarget.PublicHoliday is { Name.Length: > 0 } holiday
                ? $" ({holiday.Name})"
                : string.Empty;
            explanation = $"{date:d MMM yyyy} is a Public Holiday{holidayName}. The target is 0 h 00 min, so all work is additional time.";
        }
        else if (effectiveTarget.DayKind == ScheduleDayKind.NonWorkingDay)
        {
            status = "Non-working day";
            explanation = $"{date:dddd} is not a configured workday. The target is 0 h 00 min, so all work is additional time.";
        }
        else if (effectiveTarget.DayKind == ScheduleDayKind.LeaveDay &&
                 effectiveTarget.PublicHoliday is { } leave)
        {
            status = effectiveTarget.TargetMinutes == 0
                ? leave.Type.ToDisplayName()
                : targetProgress.IsMet
                    ? "Target met"
                    : $"{DurationFormatter.FormatMinutes(targetProgress.RemainingMinutes)} remaining";
            explanation = $"{leave.Type.ToDisplayName()}: {DurationFormatter.FormatMinutes(leave.LeaveMinutes)}. " +
                          $"Adjusted daily target: {DurationFormatter.FormatMinutes(effectiveTarget.TargetMinutes)}.";
        }
        else
        {
            status = targetProgress.IsMet
                ? "Target met"
                : $"{DurationFormatter.FormatMinutes(targetProgress.RemainingMinutes)} remaining";
            explanation = $"Configured daily target: {DurationFormatter.FormatMinutes(targetProgress.TargetMinutes)}.";
        }

        var completion = WorkdayCompletionCalculator.Calculate(
            date,
            effectiveTarget.TargetMinutes,
            sessions,
            now);
        var completionText = completion.Status switch
        {
            TargetCompletionStatus.NotApplicable => "No target for this date",
            TargetCompletionStatus.AwaitingFirstSession when targetProgress.IsMet => "Target completed",
            TargetCompletionStatus.AwaitingFirstSession => "Start work to estimate",
            TargetCompletionStatus.Completed => $"Completed by {FormatCompletionTime(date, completion.Time!.Value)}",
            _ => $"Estimated finish: {FormatCompletionTime(date, completion.Time!.Value)}"
        };

        return new DailyTargetDisplay(
            status,
            explanation,
            completionText,
            targetProgress.IsMet,
            DurationFormatter.FormatMinutes(targetProgress.AdditionalMinutes));
    }

    public async Task SaveWorkScheduleAsync(CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(DailyTargetHoursText, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(DailyTargetMinutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
            hours < 0 || minutes is < 0 or > 59)
        {
            throw new InvalidOperationException("Enter a valid daily target using whole hours and minutes from 0 to 59.");
        }

        var targetMinutes = checked(hours * 60 + minutes);
        if (targetMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException("The daily target must be between 1 minute and 24 hours.");
        }

        var scheduledDays = WorkingDayOptions
            .Where(day => day.IsSelected)
            .Aggregate(WorkingDays.None, (current, day) => current | day.Value);
        if (scheduledDays == WorkingDays.None)
        {
            throw new InvalidOperationException("Choose at least one standard workday.");
        }

        var settings = new WorkScheduleSettings(targetMinutes, scheduledDays);
        await _repository.SaveWorkScheduleSettingsAsync(settings, cancellationToken);
        ApplyWorkSchedule(settings);
        await RefreshAsync(cancellationToken);
        StatusMessage = "Work schedule saved.";
    }

    public async Task SavePublicHolidayAsync(CancellationToken cancellationToken = default)
    {
        if (NewPublicHolidayDate is null)
        {
            throw new InvalidOperationException("Choose a Holiday date.");
        }

        var name = (NewPublicHolidayName ?? string.Empty).Trim();
        if (name.Length > 120)
        {
            throw new InvalidOperationException("The Holiday description cannot exceed 120 characters.");
        }

        var leaveMinutes = 0;
        if (NewPublicHolidayType != HolidayType.PublicHoliday)
        {
            if (!int.TryParse(NewHolidayLeaveHoursText, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
                !int.TryParse(NewHolidayLeaveMinutesText, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) ||
                hours < 0 || minutes is < 0 or > 59)
            {
                throw new InvalidOperationException("Enter valid leave hours and whole minutes from 0 to 59.");
            }

            leaveMinutes = checked(hours * 60 + minutes);
            if (leaveMinutes is < 1 || leaveMinutes > _workSchedule.DailyTargetMinutes)
            {
                throw new InvalidOperationException(
                    $"Leave must be between 1 minute and the daily target of {DurationFormatter.FormatMinutes(_workSchedule.DailyTargetMinutes)}.");
            }
        }

        var publicHoliday = new PublicHoliday(
            DateOnly.FromDateTime(NewPublicHolidayDate.Value),
            name,
            NewPublicHolidayType,
            leaveMinutes);
        var originalDate = _editingPublicHolidayDate;
        var wasEditing = originalDate is not null;
        if (wasEditing)
        {
            await _repository.UpdatePublicHolidayAsync(originalDate!.Value, publicHoliday, cancellationToken);
        }
        else
        {
            await _repository.SavePublicHolidayAsync(publicHoliday, cancellationToken);
        }

        ResetHolidayEditor();
        await LoadPublicHolidaysAsync(cancellationToken);
        SelectedPublicHoliday = PublicHolidays.FirstOrDefault(row => row.Record.Date == publicHoliday.Date);
        await RefreshAsync(cancellationToken);
        StatusMessage = wasEditing ? "Holiday changes saved." : "Holiday saved.";
    }

    public void BeginEditSelectedPublicHoliday()
    {
        if (SelectedPublicHoliday is null)
        {
            throw new InvalidOperationException("Select a Holiday to edit.");
        }

        var record = SelectedPublicHoliday.Record;
        SetEditingPublicHolidayDate(record.Date);
        NewPublicHolidayDate = record.Date.ToDateTime(TimeOnly.MinValue);
        NewPublicHolidayName = record.Name;
        NewPublicHolidayType = record.Type;
        var leaveMinutes = record.Type == HolidayType.PublicHoliday
            ? _workSchedule.DailyTargetMinutes
            : record.LeaveMinutes;
        NewHolidayLeaveHoursText = (leaveMinutes / 60).ToString(CultureInfo.InvariantCulture);
        NewHolidayLeaveMinutesText = (leaveMinutes % 60).ToString(CultureInfo.InvariantCulture);
        StatusMessage = $"Editing {record.Date:d MMM yyyy}.";
    }

    public void CancelPublicHolidayEdit()
    {
        ResetHolidayEditor();
        StatusMessage = "Holiday editing cancelled.";
    }

    public async Task DeleteSelectedPublicHolidayAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedPublicHoliday is null)
        {
            throw new InvalidOperationException("Select a Holiday to delete.");
        }

        var date = SelectedPublicHoliday.Record.Date;
        await _repository.DeletePublicHolidayAsync(date, cancellationToken);
        if (_editingPublicHolidayDate == date)
        {
            ResetHolidayEditor();
        }
        SelectedPublicHoliday = null;
        await LoadPublicHolidaysAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
        StatusMessage = "Holiday deleted.";
    }

    public async Task SaveWorkSessionAsync(
        WorkSessionRecord? original,
        TimeOnly startTime,
        TimeOnly endTime,
        int breakMinutes,
        LocationType location,
        string note,
        CancellationToken cancellationToken = default)
    {
        if (DailyDetailDate is null)
        {
            throw new InvalidOperationException("Choose a date for the daily detail.");
        }

        var workDate = DateOnly.FromDateTime(DailyDetailDate.Value);
        var startLocal = DateTime.SpecifyKind(workDate.ToDateTime(startTime), DateTimeKind.Unspecified);
        var endDate = endTime == TimeOnly.MinValue && startTime > TimeOnly.MinValue
            ? workDate.AddDays(1)
            : workDate;
        var endLocal = DateTime.SpecifyKind(endDate.ToDateTime(endTime), DateTimeKind.Unspecified);
        if (endLocal <= startLocal)
        {
            throw new InvalidOperationException("End time must be later than start time. Midnight is allowed as the end of the selected date.");
        }

        var durationMinutes = (endLocal - startLocal).TotalMinutes;
        if (breakMinutes < 0 || breakMinutes >= durationMinutes)
        {
            throw new InvalidOperationException("Break minutes must be zero or more and shorter than the session.");
        }

        if (!Enum.IsDefined(location))
        {
            throw new InvalidOperationException("Choose a valid location.");
        }

        var trimmedNote = (note ?? string.Empty).Trim();
        if (trimmedNote.Length > 200)
        {
            throw new InvalidOperationException("The note cannot exceed 200 characters.");
        }

        var start = new DateTimeOffset(startLocal, TimeZoneInfo.Local.GetUtcOffset(startLocal)).ToUniversalTime();
        var end = new DateTimeOffset(endLocal, TimeZoneInfo.Local.GetUtcOffset(endLocal)).ToUniversalTime();
        var session = new WorkSessionRecord(
            original?.Id ?? Guid.NewGuid().ToString("N"),
            start,
            end,
            breakMinutes,
            original?.ActiveSecondsByNetwork ?? new Dictionary<string, int>(StringComparer.Ordinal),
            location,
            WorkSessionStatus.Confirmed,
            original?.Source ?? WorkSessionSource.Manual,
            trimmedNote);
        await _repository.UpsertWorkSessionAsync(workDate, session, cancellationToken);
        await RefreshAsync(cancellationToken);
        StatusMessage = original is null ? "Manual work session added and confirmed." : "Work session updated and confirmed.";
    }

    public async Task DeleteSelectedWorkSessionAsync(CancellationToken cancellationToken = default)
    {
        if (DailyDetailDate is null || SelectedWorkSession is null)
        {
            throw new InvalidOperationException("Select a work session to delete.");
        }

        var date = DateOnly.FromDateTime(DailyDetailDate.Value);
        await _repository.DeleteWorkSessionAsync(date, SelectedWorkSession.Record.Id, cancellationToken);
        SelectedWorkSession = null;
        await RefreshAsync(cancellationToken);
        StatusMessage = "Work session deleted.";
    }

    public async Task SplitSelectedWorkSessionAsync(
        TimeOnly firstEnd,
        TimeOnly secondStart,
        int firstBreakMinutes,
        int secondBreakMinutes,
        CancellationToken cancellationToken = default)
    {
        if (DailyDetailDate is null || SelectedWorkSession is null)
        {
            throw new InvalidOperationException("Select a work session to split.");
        }

        var date = DateOnly.FromDateTime(DailyDetailDate.Value);
        var original = SelectedWorkSession.Record;
        var firstEndUtc = ToUtc(date, firstEnd);
        var secondStartUtc = ToUtc(date, secondStart);
        if (firstEndUtc <= original.StartTimeUtc || secondStartUtc < firstEndUtc || secondStartUtc >= original.EndTimeUtc)
        {
            throw new InvalidOperationException("The split times must be ordered within the selected work session.");
        }

        var location = ParseSessionLocation(SelectedWorkSession.Location);
        var first = new WorkSessionRecord(
            original.Id,
            original.StartTimeUtc,
            firstEndUtc,
            firstBreakMinutes,
            new Dictionary<string, int>(StringComparer.Ordinal),
            location,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            original.Note ?? string.Empty);
        var second = new WorkSessionRecord(
            Guid.NewGuid().ToString("N"),
            secondStartUtc,
            original.EndTimeUtc,
            secondBreakMinutes,
            new Dictionary<string, int>(StringComparer.Ordinal),
            location,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            original.Note ?? string.Empty);
        await _repository.ReplaceWorkSessionsAsync(
            date,
            new[] { original.Id },
            new[] { first, second },
            cancellationToken);
        await RefreshAsync(cancellationToken);
        StatusMessage = "Work session split into two confirmed sessions.";
    }

    public async Task MergeWorkSessionsAsync(
        IReadOnlyCollection<WorkSessionRowViewModel> selectedRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selectedRows);

        if (DailyDetailDate is null || selectedRows.Count < 2)
        {
            throw new InvalidOperationException("Select two or more consecutive work sessions to merge.");
        }

        var selected = selectedRows
            .Distinct()
            .OrderBy(row => row.Record.StartTimeUtc)
            .ToArray();
        if (selected.Length < 2)
        {
            throw new InvalidOperationException("Select two or more consecutive work sessions to merge.");
        }

        var selectedIndices = selected
            .Select(WorkSessions.IndexOf)
            .Order()
            .ToArray();
        if (selectedIndices[0] < 0 ||
            selectedIndices.Zip(selectedIndices.Skip(1), (first, second) => second - first).Any(gap => gap != 1))
        {
            throw new InvalidOperationException("Only consecutive work sessions can be merged. Include every row between the first and last selection.");
        }

        var locationName = selected[0].Location;
        if (selected.Any(row => !string.Equals(row.Location, locationName, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Only sessions with the same location can be merged. Edit their locations first.");
        }

        var location = ParseSessionLocation(locationName);
        var sessions = selected.Select(row => row.Record).ToArray();
        var gapMinutes = sessions
            .Zip(sessions.Skip(1), (first, second) =>
                Math.Max(0, (int)Math.Round((second.StartTimeUtc - first.EndTimeUtc).TotalMinutes)))
            .Sum();
        var noteParts = sessions
            .Select(session => session.Note)
            .Where(note => !string.IsNullOrWhiteSpace(note))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var merged = new WorkSessionRecord(
            sessions[0].Id,
            sessions[0].StartTimeUtc,
            sessions[^1].EndTimeUtc,
            checked(sessions.Sum(session => session.BreakMinutes) + gapMinutes),
            new Dictionary<string, int>(StringComparer.Ordinal),
            location,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            string.Join(" | ", noteParts));
        var date = DateOnly.FromDateTime(DailyDetailDate.Value);
        await _repository.ReplaceWorkSessionsAsync(
            date,
            sessions.Select(session => session.Id).ToArray(),
            new[] { merged },
            cancellationToken);
        await RefreshAsync(cancellationToken);
        StatusMessage = $"Merged {sessions.Length} selected work sessions and confirmed the result.";
    }

    public async Task ConfirmDailyWorkSessionsAsync(CancellationToken cancellationToken = default)
    {
        if (DailyDetailDate is null)
        {
            throw new InvalidOperationException("Choose a date for the daily detail.");
        }

        var date = DateOnly.FromDateTime(DailyDetailDate.Value);
        await _repository.ConfirmWorkSessionsAsync(date, cancellationToken);
        await RefreshAsync(cancellationToken);
        StatusMessage = "All work sessions for the selected date are confirmed.";
    }

    public async Task ClassifySelectedNetworkAsync(
        LocationType locationType,
        CancellationToken cancellationToken = default)
    {
        var selected = SelectedNetwork ?? Networks.FirstOrDefault();
        if (selected is null)
        {
            throw new InvalidOperationException("No network has been observed yet. Wait for the service to record a sample.");
        }

        await _repository.SetNetworkLocationAsync(selected.NetworkId, locationType, cancellationToken);
        SelectedNetwork = null;
        await RefreshAsync(cancellationToken);
        StatusMessage = $"Marked {selected.NetworkName} as {locationType}.";
    }

    public Task<int> GetNetworkUsageDayCountAsync(
        NetworkRowViewModel network,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(network);
        return _repository.GetNetworkUsageDayCountAsync(network.NetworkId, cancellationToken);
    }

    public async Task DeleteNetworkAsync(
        NetworkRowViewModel network,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(network);

        await _repository.DeleteNetworkAsync(network.NetworkId, cancellationToken);
        SelectedNetwork = null;
        await RefreshAsync(cancellationToken);
        StatusMessage = $"Deleted {network.NetworkName}. Linked usage remains recorded as Unclassified.";
    }

    public async Task<IReadOnlyList<string>> ExportCsvAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildSelectedReportAsync(cancellationToken);
        var paths = await _reportExporter.ExportCsvAsync(report, outputPath, cancellationToken);
        StatusMessage = $"Exported {paths.Count} CSV files.";
        return paths;
    }

    public async Task ExportHtmlAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var report = await BuildSelectedReportAsync(cancellationToken);
        await _reportExporter.ExportHtmlAsync(report, outputPath, cancellationToken);
        StatusMessage = "Exported the printable HTML report.";
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var workScheduleTask = _repository.GetWorkScheduleSettingsAsync(cancellationToken);
        var publicHolidaysTask = _repository.GetPublicHolidaysAsync(cancellationToken);
        await Task.WhenAll(workScheduleTask, publicHolidaysTask);

        ApplyWorkSchedule(await workScheduleTask);
        ApplyPublicHolidays(await publicHolidaysTask);
    }

    private async Task LoadPublicHolidaysAsync(CancellationToken cancellationToken) =>
        ApplyPublicHolidays(await _repository.GetPublicHolidaysAsync(cancellationToken));

    private void ApplyWorkSchedule(WorkScheduleSettings settings)
    {
        _workSchedule = settings;
        DailyTargetHoursText = (settings.DailyTargetMinutes / 60).ToString(CultureInfo.InvariantCulture);
        DailyTargetMinutesText = (settings.DailyTargetMinutes % 60).ToString(CultureInfo.InvariantCulture);
        if (!IsEditingPublicHoliday)
        {
            NewHolidayLeaveHoursText = (settings.DailyTargetMinutes / 60).ToString(CultureInfo.InvariantCulture);
            NewHolidayLeaveMinutesText = (settings.DailyTargetMinutes % 60).ToString(CultureInfo.InvariantCulture);
        }
        foreach (var day in WorkingDayOptions)
        {
            day.IsSelected = (settings.ScheduledDays & day.Value) != 0;
        }
    }

    private void ApplyPublicHolidays(IEnumerable<PublicHoliday> publicHolidays)
    {
        var selectedDate = SelectedPublicHoliday?.Record.Date;
        PublicHolidays.Clear();
        foreach (var publicHoliday in publicHolidays.OrderBy(holiday => holiday.Date))
        {
            PublicHolidays.Add(new PublicHolidayRowViewModel(publicHoliday));
        }

        SelectedPublicHoliday = PublicHolidays.FirstOrDefault(row => row.Record.Date == selectedDate);
    }

    private void ResetHolidayEditor()
    {
        SetEditingPublicHolidayDate(null);
        NewPublicHolidayDate = DateTime.Today;
        NewPublicHolidayName = string.Empty;
        NewPublicHolidayType = HolidayType.PublicHoliday;
        NewHolidayLeaveHoursText = (_workSchedule.DailyTargetMinutes / 60).ToString(CultureInfo.InvariantCulture);
        NewHolidayLeaveMinutesText = (_workSchedule.DailyTargetMinutes % 60).ToString(CultureInfo.InvariantCulture);
    }

    private void SetEditingPublicHolidayDate(DateOnly? date)
    {
        if (_editingPublicHolidayDate == date)
        {
            return;
        }

        _editingPublicHolidayDate = date;
        OnPropertyChanged(nameof(IsEditingPublicHoliday));
        OnPropertyChanged(nameof(HolidaySaveButtonText));
    }

    private async Task<UsageReport> BuildSelectedReportAsync(CancellationToken cancellationToken)
    {
        if (FromDate is null || ToDate is null)
        {
            throw new InvalidOperationException("Choose a report start and end date.");
        }

        if (!decimal.TryParse(
                HourlyRateText,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var hourlyRate))
        {
            throw new InvalidOperationException("Enter the hourly rate as a number, for example 0.70.");
        }

        return await BuildReportAsync(
            DateOnly.FromDateTime(FromDate.Value),
            DateOnly.FromDateTime(ToDate.Value),
            hourlyRate,
            cancellationToken);
    }

    private async Task RefreshMonthlyTableAsync(CancellationToken cancellationToken)
    {
        if (SelectedMonthIndex is < 0 or > 11)
        {
            throw new InvalidOperationException("Choose a valid month.");
        }

        if (!YearOptions.Contains(SelectedYear))
        {
            throw new InvalidOperationException("Choose a valid year.");
        }

        var firstDay = new DateOnly(SelectedYear, SelectedMonthIndex + 1, 1);
        var lastDay = firstDay.AddMonths(1).AddDays(-1);
        var today = DateOnly.FromDateTime(DateTime.Today);

        MonthlyRows.Clear();
        if (firstDay > today)
        {
            SetMonthlyTotals(null);
            return;
        }

        var report = await BuildReportAsync(
            firstDay,
            lastDay > today ? today : lastDay,
            0,
            cancellationToken);

        var breakMinutesByDate = report.DetailedSessions
            .GroupBy(session => session.Date)
            .ToDictionary(group => group.Key, group => group.Sum(session => session.BreakMinutes));
        var workHoursByDate = report.DetailedSessions
            .GroupBy(session => session.Date)
            .ToDictionary(group => group.Key, group => group.Sum(session => session.WorkHours));
        var holidays = PublicHolidays.Select(row => row.Record).ToArray();

        foreach (var day in report.Days)
        {
            var effectiveTarget = WorkScheduleEvaluator.Evaluate(day.Date, _workSchedule, holidays);
            var additionalMinutes = WorkdayTargetCalculator
                .Calculate(workHoursByDate.GetValueOrDefault(day.Date), effectiveTarget.TargetMinutes)
                .AdditionalMinutes;
            MonthlyRows.Add(new MonthlyUsageRowViewModel(
                day,
                breakMinutesByDate.GetValueOrDefault(day.Date),
                additionalMinutes));
        }

        SetMonthlyTotals(report, MonthlyRows.Sum(row => row.AdditionalMinutes));
    }

    private void SetMonthlyTotals(UsageReport? report, int additionalMinutes = 0)
    {
        var homeHours = report?.TotalHomeHours ?? 0;
        var officeHours = report?.TotalOfficeHours ?? 0;
        var otherHours = report?.TotalOtherHours ?? 0;
        var unknownHours = report?.TotalUnknownHours ?? 0;

        MonthlyHomeHours = DurationFormatter.FormatHours(homeHours);
        MonthlyOfficeHours = DurationFormatter.FormatHours(officeHours);
        MonthlyAdditionalHours = DurationFormatter.FormatMinutes(additionalMinutes);
        MonthlyTotalHours = DurationFormatter.FormatHours(homeHours + officeHours + otherHours + unknownHours);
    }

    private async Task<UsageReport> BuildReportAsync(
        DateOnly from,
        DateOnly to,
        decimal hourlyRate,
        CancellationToken cancellationToken)
    {
        var dailyUsageTask = _repository.GetDailyUsageAsync(from, to, cancellationToken);
        var networksTask = _repository.GetNetworkLocationsAsync(cancellationToken);
        var adjustmentsTask = _repository.GetManualAdjustmentsAsync(from, to, cancellationToken);
        await Task.WhenAll(dailyUsageTask, networksTask, adjustmentsTask);

        return _reportBuilder.Build(
            from,
            to,
            hourlyRate,
            await dailyUsageTask,
            await networksTask,
            await adjustmentsTask);
    }

    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();
    }

    private static string FormatCompletionTime(DateOnly selectedDate, DateTimeOffset completionTime)
    {
        var time = completionTime.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-AU"));
        return DateOnly.FromDateTime(completionTime.DateTime) > selectedDate
            ? $"{time} next day"
            : time;
    }

    private static string FormatEvents(IReadOnlyCollection<PublicHoliday> events) =>
        string.Join(
            Environment.NewLine,
            events.Select(holiday =>
            {
                var duration = holiday.Type == HolidayType.PublicHoliday
                    ? string.Empty
                    : $" ({DurationFormatter.FormatMinutes(holiday.LeaveMinutes)})";
                var description = string.IsNullOrWhiteSpace(holiday.Name)
                    ? string.Empty
                    : $": {holiday.Name}";
                return $"{holiday.Date.ToString("d MMM", CultureInfo.GetCultureInfo("en-AU"))} — " +
                       $"{holiday.Type.ToDisplayName()}{duration}{description}";
            }));

    private DateTime EarliestSelectableMonth => new(YearOptions.Min(), 1, 1);

    private static DateTime CurrentMonth => new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private bool TryGetSelectedMonth(out DateTime selectedMonth)
    {
        if (SelectedMonthIndex is < 0 or > 11 || !YearOptions.Contains(SelectedYear))
        {
            selectedMonth = default;
            return false;
        }

        selectedMonth = new DateTime(SelectedYear, SelectedMonthIndex + 1, 1);
        return true;
    }

    private void NotifyDailyDateNavigationChanged()
    {
        OnPropertyChanged(nameof(DailyWorkSessionsHeading));
        OnPropertyChanged(nameof(CanMoveToPreviousDailyDate));
        OnPropertyChanged(nameof(CanMoveToNextDailyDate));
    }

    private void NotifyMonthNavigationChanged()
    {
        OnPropertyChanged(nameof(CanMoveToPreviousMonth));
        OnPropertyChanged(nameof(CanMoveToNextMonth));
    }

    private static LocationType ParseSessionLocation(string location) => location switch
    {
        nameof(LocationType.Home) => LocationType.Home,
        nameof(LocationType.Office) => LocationType.Office,
        nameof(LocationType.Other) => LocationType.Other,
        "Unclassified" => LocationType.Unknown,
        _ => throw new InvalidOperationException("A mixed-location session must be separated or assigned one location before this action.")
    };

    private static string ReadServiceStatus()
    {
        try
        {
            using var service = new ServiceController(AppConstants.ServiceName);
            return service.Status switch
            {
                ServiceControllerStatus.Running => "Running",
                ServiceControllerStatus.StartPending => "Starting",
                ServiceControllerStatus.StopPending => "Stopping",
                _ => service.Status.ToString()
            };
        }
        catch (InvalidOperationException)
        {
            return "Not installed";
        }
        catch (Win32Exception)
        {
            return "Status unavailable";
        }
    }

    private sealed record DailyTargetDisplay(
        string Status,
        string Explanation,
        string CompletionText,
        bool IsMet,
        string AdditionalTime);

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

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
