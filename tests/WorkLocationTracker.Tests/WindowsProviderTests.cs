using WorkLocationTracker.Service.Tracking;

namespace WorkLocationTracker.Tests;

public sealed class WindowsProviderTests
{
    [Fact]
    public void ActivityProvider_ReturnsAValidSnapshot()
    {
        var snapshot = new WtsActivityProvider().GetSnapshot();

        var maximumPlausibleIdleTime =
            TimeSpan.FromMilliseconds(Environment.TickCount64) + TimeSpan.FromMinutes(1);

        Assert.InRange(snapshot.IdleTime, TimeSpan.Zero, maximumPlausibleIdleTime);
    }

    [Fact]
    public void ActivityProvider_UsesUnlockedFallbackWhenLastInputPredatesLogon()
    {
        var logonTime = DateTimeOffset.UtcNow.ToFileTime();

        var snapshot = WtsActivityProvider.CreateSnapshot(
            hasInteractiveUser: true,
            logonTime,
            lastInputTime: logonTime - TimeSpan.FromMinutes(1).Ticks,
            currentTime: logonTime + TimeSpan.FromMinutes(10).Ticks,
            maximumPlausibleIdleTime: TimeSpan.FromDays(1));

        Assert.True(snapshot.HasInteractiveUser);
        Assert.Equal(TimeSpan.Zero, snapshot.IdleTime);
    }

    [Fact]
    public void ActivityProvider_KeepsValidIdleTimeForCurrentSession()
    {
        var logonTime = DateTimeOffset.UtcNow.ToFileTime();

        var snapshot = WtsActivityProvider.CreateSnapshot(
            hasInteractiveUser: true,
            logonTime,
            lastInputTime: logonTime + TimeSpan.FromMinutes(10).Ticks,
            currentTime: logonTime + TimeSpan.FromMinutes(12).Ticks,
            maximumPlausibleIdleTime: TimeSpan.FromDays(1));

        Assert.True(snapshot.HasInteractiveUser);
        Assert.Equal(TimeSpan.FromMinutes(2), snapshot.IdleTime);
    }

    [Fact]
    public void NetworkProvider_ReturnsAStableNonEmptyIdentity()
    {
        var provider = new NetworkSnapshotProvider();

        var first = provider.GetSnapshot();
        var second = provider.GetSnapshot();

        Assert.NotEmpty(first.Id);
        Assert.NotEmpty(first.DisplayName);
        Assert.Equal(first.Id, second.Id);
    }
}
