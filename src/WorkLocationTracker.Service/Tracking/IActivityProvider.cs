namespace WorkLocationTracker.Service.Tracking;

public interface IActivityProvider
{
    ActivitySnapshot GetSnapshot();
}

public sealed record ActivitySnapshot(bool HasInteractiveUser, TimeSpan IdleTime);

