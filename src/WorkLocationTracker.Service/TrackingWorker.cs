using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WorkLocationTracker.Core.Data;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Service.Tracking;

namespace WorkLocationTracker.Service;

public sealed class TrackingWorker : BackgroundService
{
    private readonly ITrackerRepository _repository;
    private readonly IActivityProvider _activityProvider;
    private readonly INetworkSnapshotProvider _networkProvider;
    private readonly TrackingOptions _options;
    private readonly ILogger<TrackingWorker> _logger;

    public TrackingWorker(
        ITrackerRepository repository,
        IActivityProvider activityProvider,
        INetworkSnapshotProvider networkProvider,
        IOptions<TrackingOptions> options,
        ILogger<TrackingWorker> logger)
    {
        _repository = repository;
        _activityProvider = activityProvider;
        _networkProvider = networkProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _repository.InitialiseAsync(stoppingToken);
        _logger.LogInformation(
            "Usage tracking started with a {Interval}-second interval and {IdleThreshold}-minute idle threshold.",
            _options.SampleIntervalSeconds,
            _options.IdleThresholdMinutes);

        var previousObservation = DateTimeOffset.UtcNow;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.SampleIntervalSeconds));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var observedAt = DateTimeOffset.UtcNow;
            var elapsed = observedAt - previousObservation;
            previousObservation = observedAt;

            if (elapsed.TotalSeconds > _options.MaximumGapSeconds)
            {
                _logger.LogInformation(
                    "Skipped a {GapSeconds}-second gap, probably caused by sleep or hibernation.",
                    (int)elapsed.TotalSeconds);
                continue;
            }

            try
            {
                await RecordSampleAsync(observedAt, elapsed, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not record this usage sample; collection will retry.");
            }
        }
    }

    private async Task RecordSampleAsync(
        DateTimeOffset observedAt,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        var durationSeconds = Math.Max(1, (int)Math.Round(elapsed.TotalSeconds));
        var activity = _activityProvider.GetSnapshot();
        var network = _networkProvider.GetSnapshot();
        var isActive = activity.HasInteractiveUser &&
                       activity.IdleTime <= TimeSpan.FromMinutes(_options.IdleThresholdMinutes);

        var sample = new UsageSample(
            0,
            observedAt - elapsed,
            observedAt,
            isActive ? durationSeconds : 0,
            isActive ? 0 : durationSeconds,
            network.Id,
            network.DisplayName,
            Environment.MachineName);

        await _repository.AddUsageSampleAsync(sample, cancellationToken);
    }
}

