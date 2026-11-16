using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkLocationTracker.Core;
using WorkLocationTracker.Core.Data;
using WorkLocationTracker.Service;
using WorkLocationTracker.Service.Tracking;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = AppConstants.DisplayName;
});

builder.Services
    .AddOptions<TrackingOptions>()
    .Bind(builder.Configuration.GetSection(TrackingOptions.SectionName))
    .Validate(
        options => options.SampleIntervalSeconds is >= 15 and <= 3600,
        "Tracking:SampleIntervalSeconds must be between 15 and 3600.")
    .Validate(
        options => options.IdleThresholdMinutes is >= 1 and <= 120,
        "Tracking:IdleThresholdMinutes must be between 1 and 120.")
    .Validate(
        options => options.MaximumGapSeconds is >= 30 and <= 7200,
        "Tracking:MaximumGapSeconds must be between 30 and 7200.")
    .ValidateOnStart();

builder.Services.AddSingleton<AppPaths>();
builder.Services.AddSingleton<ITrackerRepository, TrackerRepository>();
builder.Services.AddSingleton<IActivityProvider, WtsActivityProvider>();
builder.Services.AddSingleton<INetworkSnapshotProvider, NetworkSnapshotProvider>();
builder.Services.AddHostedService<TrackingWorker>();

await builder.Build().RunAsync();
