namespace WorkLocationTracker.Service.Tracking;

public interface INetworkSnapshotProvider
{
    NetworkSnapshot GetSnapshot();
}

public sealed record NetworkSnapshot(string Id, string DisplayName);

