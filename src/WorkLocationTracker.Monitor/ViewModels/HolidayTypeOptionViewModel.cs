using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed record HolidayTypeOptionViewModel(
    string Name,
    HolidayType Value);
