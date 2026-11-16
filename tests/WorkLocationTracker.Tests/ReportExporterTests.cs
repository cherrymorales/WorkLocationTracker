using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class ReportExporterTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "WorkLocationTracker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Export_WritesPrintableHtmlAndSeparateAdjustmentAudit()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var date = new DateOnly(2026, 8, 17);
        var report = new UsageReport(
            date,
            date,
            0.70m,
            DateTimeOffset.UtcNow,
            new[]
            {
                new DailyUsageReport(
                    date,
                    3600,
                    0,
                    0,
                    0,
                    0,
                    0,
                    0,
                    new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.FromHours(10)),
                    new DateTimeOffset(2026, 8, 17, 17, 0, 0, TimeSpan.FromHours(10)))
            },
            new[] { new NetworkLocation("id", "A&B <Wi-Fi>", LocationType.Home, DateTimeOffset.UtcNow, null) },
            new[] { new ManualAdjustment(1, date, LocationType.Home, 30, "=unsafe formula", DateTimeOffset.UtcNow) },
            new[]
            {
                new WorkSessionReport(
                    date,
                    "session-one",
                    new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.FromHours(10)),
                    new DateTimeOffset(2026, 8, 17, 17, 0, 0, TimeSpan.FromHours(10)),
                    30,
                    8.5,
                    "Home",
                    WorkSessionStatus.Confirmed,
                    WorkSessionSource.Manual,
                    "Reviewed & confirmed")
            });
        var exporter = new ReportExporter();
        var htmlPath = Path.Combine(_temporaryDirectory, "report.html");
        var csvPath = Path.Combine(_temporaryDirectory, "report.csv");

        await exporter.ExportHtmlAsync(report, htmlPath);
        var csvPaths = await exporter.ExportCsvAsync(report, csvPath);

        var html = await File.ReadAllTextAsync(htmlPath);
        var dailyCsv = await File.ReadAllTextAsync(csvPaths[0]);
        var sessionsCsv = await File.ReadAllTextAsync(csvPaths[1]);
        var adjustmentCsv = await File.ReadAllTextAsync(csvPaths[2]);
        Assert.Contains("A&amp;B &lt;Wi-Fi&gt;", html, StringComparison.Ordinal);
        Assert.Contains("<th>Start</th><th>End</th>", html, StringComparison.Ordinal);
        Assert.Contains("Detailed work sessions", html, StringComparison.Ordinal);
        Assert.Contains("Work location activity report", html, StringComparison.Ordinal);
        Assert.Contains("Estimated home value", html, StringComparison.Ordinal);
        Assert.Contains("1 h 00 min", html, StringComparison.Ordinal);
        Assert.Contains("8 h 30 min", html, StringComparison.Ordinal);
        Assert.Contains("Reviewed &amp; confirmed", html, StringComparison.Ordinal);
        Assert.Contains("2026-08-17,08:00,17:00", dailyCsv, StringComparison.Ordinal);
        Assert.Contains("Estimated home value", dailyCsv, StringComparison.Ordinal);
        Assert.Contains("2026-08-17,08:00,17:00,30,8.50", sessionsCsv, StringComparison.Ordinal);
        Assert.Contains("\"'=unsafe formula\"", adjustmentCsv, StringComparison.Ordinal);
        Assert.DoesNotContain("tax", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deduction", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tax", dailyCsv, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deduction", dailyCsv, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, csvPaths.Count);
    }

    public void Dispose()
    {
        if (!_temporaryDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a test directory outside the system temporary path.");
        }

        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
