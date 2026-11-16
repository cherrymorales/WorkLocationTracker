using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed record NetworkRowViewModel(
    string NetworkId,
    string NetworkName,
    LocationType LocationType,
    string LastSeen);

