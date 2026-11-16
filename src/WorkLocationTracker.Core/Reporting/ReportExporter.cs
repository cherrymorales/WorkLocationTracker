using System.Globalization;
using System.Net;
using System.Text;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public sealed class ReportExporter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public async Task<IReadOnlyList<string>> ExportCsvAsync(
        UsageReport report,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var dailyCsv = BuildDailyCsv(report);
        await File.WriteAllTextAsync(outputPath, dailyCsv, Utf8WithoutBom, cancellationToken);

        var adjustmentPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(outputPath)}-adjustments.csv");
        var adjustmentCsv = BuildAdjustmentsCsv(report.Adjustments);
        await File.WriteAllTextAsync(adjustmentPath, adjustmentCsv, Utf8WithoutBom, cancellationToken);

        var sessionPath = Path.Combine(
            Path.GetDirectoryName(outputPath) ?? string.Empty,
            $"{Path.GetFileNameWithoutExtension(outputPath)}-sessions.csv");
        var sessionCsv = BuildSessionsCsv(report.DetailedSessions);
        await File.WriteAllTextAsync(sessionPath, sessionCsv, Utf8WithoutBom, cancellationToken);

        return new[] { outputPath, sessionPath, adjustmentPath };
    }

    public async Task ExportHtmlAsync(
        UsageReport report,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        await File.WriteAllTextAsync(
            outputPath,
            BuildHtml(report),
            Utf8WithoutBom,
            cancellationToken);
    }

    internal static string BuildDailyCsv(UsageReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Start time,End time,Home hours,Office hours,Other hours,Unclassified hours,Home adjustment minutes,Hourly rate,Estimated home value");

        foreach (var day in report.Days)
        {
            var amount = decimal.Round((decimal)day.HomeHours * report.HourlyRate, 2, MidpointRounding.AwayFromZero);
            builder
                .Append(day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(FormatTime(day.StartTime)).Append(',')
                .Append(FormatTime(day.EndTime)).Append(',')
                .Append(FormatDecimalHours(day.HomeHours)).Append(',')
                .Append(FormatDecimalHours(day.OfficeHours)).Append(',')
                .Append(FormatDecimalHours(day.OtherHours)).Append(',')
                .Append(FormatDecimalHours(day.UnknownHours)).Append(',')
                .Append(day.HomeAdjustmentMinutes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(report.HourlyRate.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .AppendLine(amount.ToString("0.00", CultureInfo.InvariantCulture));
        }

        builder.AppendLine();
        builder.Append("TOTAL,,,")
            .Append(FormatDecimalHours(report.TotalHomeHours)).Append(',')
            .Append(FormatDecimalHours(report.TotalOfficeHours)).Append(',')
            .Append(FormatDecimalHours(report.TotalOtherHours)).Append(',')
            .Append(FormatDecimalHours(report.TotalUnknownHours)).Append(",,,")
            .AppendLine(report.EstimatedHomeValue.ToString("0.00", CultureInfo.InvariantCulture));
        builder.AppendLine();
        builder.AppendLine("Note: This report records computer activity and network location. Review it against your calendar, timesheets, leave and breaks before relying on it.");
        builder.AppendLine("Note: The hourly rate is optional and user supplied.");

        return builder.ToString();
    }

    internal static string BuildAdjustmentsCsv(IReadOnlyList<ManualAdjustment> adjustments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Location,Minutes,Reason,Recorded at (UTC)");
        foreach (var adjustment in adjustments)
        {
            builder
                .Append(adjustment.WorkDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(adjustment.LocationType).Append(',')
                .Append(adjustment.Minutes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(EscapeCsv(adjustment.Reason)).Append(',')
                .AppendLine(adjustment.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    internal static string BuildSessionsCsv(IReadOnlyList<WorkSessionReport> sessions)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Date,Start time,End time,Break minutes,Work hours,Location,Status,Source,Note");
        foreach (var session in sessions)
        {
            builder
                .Append(session.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(FormatTime(session.StartTime)).Append(',')
                .Append(FormatTime(session.EndTime)).Append(',')
                .Append(session.BreakMinutes.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(FormatDecimalHours(session.WorkHours)).Append(',')
                .Append(EscapeCsv(session.Location)).Append(',')
                .Append(session.Status).Append(',')
                .Append(session.Source).Append(',')
                .AppendLine(EscapeCsv(session.Note));
        }

        return builder.ToString();
    }

    internal static string BuildHtml(UsageReport report)
    {
        var builder = new StringBuilder();
        builder.Append("""
            <!doctype html>
            <html lang="en-AU">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Work location activity report</title>
              <style>
                body { font: 15px/1.45 "Segoe UI", Arial, sans-serif; color: #17202a; margin: 40px auto; max-width: 1050px; padding: 0 24px; }
                h1, h2 { color: #173d36; }
                .summary { display: flex; flex-wrap: wrap; gap: 14px; margin: 24px 0; }
                .card { border: 1px solid #d7e2df; border-radius: 10px; padding: 14px 18px; min-width: 180px; background: #f7faf9; }
                .card strong { display: block; font-size: 25px; color: #126b58; }
                table { border-collapse: collapse; width: 100%; margin: 12px 0 28px; }
                th, td { border-bottom: 1px solid #d7e2df; padding: 9px; text-align: right; }
                th:first-child, td:first-child, .text { text-align: left; }
                th { background: #eef5f3; color: #173d36; }
                .notice { border-left: 4px solid #d99000; background: #fff8e8; padding: 12px 16px; margin: 20px 0; }
                .muted { color: #5d6d69; }
                @media print { body { margin: 12mm; max-width: none; } .notice { break-inside: avoid; } }
              </style>
            </head>
            <body>
            <h1>Work location activity report</h1>
            """);

        builder.Append("<p class=\"muted\">Period: ")
            .Append(report.From.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU")))
            .Append(" to ")
            .Append(report.To.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-AU")))
            .Append(" · Generated ")
            .Append(report.GeneratedAt.ToString("d MMMM yyyy h:mm tt zzz", CultureInfo.GetCultureInfo("en-AU")))
            .AppendLine("</p>");

        builder.Append("<div class=\"summary\">")
            .Append(SummaryCard("Home", DurationFormatter.FormatHours(report.TotalHomeHours)))
            .Append(SummaryCard("Office", DurationFormatter.FormatHours(report.TotalOfficeHours)))
            .Append(SummaryCard("Unclassified", DurationFormatter.FormatHours(report.TotalUnknownHours)))
            .Append(SummaryCard("Estimated home value", report.EstimatedHomeValue.ToString("C", CultureInfo.GetCultureInfo("en-AU"))))
            .AppendLine("</div>");

        builder.Append("<p>Estimated calculation: ")
            .Append(DurationFormatter.FormatHours(report.TotalHomeHours))
            .Append(" of Home time × ")
            .Append(report.HourlyRate.ToString("C", CultureInfo.GetCultureInfo("en-AU")))
            .AppendLine(" per hour.</p>");

        builder.AppendLine("""
            <div class="notice"><strong>Review before relying on this report.</strong> This application measures computer activity and network location; it cannot determine whether every activity was work-related. Reconcile the figures with your calendar, timesheets, leave and breaks. The hourly rate is optional and user supplied.</div>
            <h2>Daily record</h2>
            <table><thead><tr><th>Date</th><th>Start</th><th>End</th><th>Home</th><th>Office</th><th>Other</th><th>Unclassified</th><th>Home adjustment</th></tr></thead><tbody>
            """);

        foreach (var day in report.Days)
        {
            builder.Append("<tr><td>").Append(day.Date.ToString("ddd d MMM yyyy", CultureInfo.GetCultureInfo("en-AU"))).Append("</td><td>")
                .Append(FormatTime(day.StartTime)).Append("</td><td>")
                .Append(FormatTime(day.EndTime)).Append("</td><td>")
                .Append(DurationFormatter.FormatHours(day.HomeHours)).Append("</td><td>")
                .Append(DurationFormatter.FormatHours(day.OfficeHours)).Append("</td><td>")
                .Append(DurationFormatter.FormatHours(day.OtherHours)).Append("</td><td>")
                .Append(DurationFormatter.FormatHours(day.UnknownHours)).Append("</td><td>")
                .Append(day.HomeAdjustmentMinutes.ToString(CultureInfo.InvariantCulture)).AppendLine(" min</td></tr>");
        }

        builder.AppendLine("</tbody></table><h2>Detailed work sessions</h2><table><thead><tr><th>Date</th><th>Start</th><th>End</th><th>Break</th><th>Work hours</th><th>Location</th><th>Status</th><th>Note</th></tr></thead><tbody>");
        foreach (var session in report.DetailedSessions)
        {
            builder.Append("<tr><td>").Append(session.Date.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-AU"))).Append("</td><td>")
                .Append(FormatTime(session.StartTime)).Append("</td><td>")
                .Append(FormatTime(session.EndTime)).Append("</td><td>")
                .Append(DurationFormatter.FormatMinutes(session.BreakMinutes)).Append("</td><td>")
                .Append(DurationFormatter.FormatHours(session.WorkHours)).Append("</td><td>")
                .Append(Html(session.Location)).Append("</td><td>")
                .Append(session.Status).Append("</td><td class=\"text\">")
                .Append(Html(session.Note)).AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table><h2>Network classifications</h2><table><thead><tr><th>Network</th><th>Classification</th><th>Last seen</th></tr></thead><tbody>");
        foreach (var network in report.Networks)
        {
            builder.Append("<tr><td class=\"text\">").Append(Html(network.NetworkName)).Append("</td><td>")
                .Append(network.LocationType).Append("</td><td>")
                .Append(network.LastSeenUtc.ToLocalTime().ToString("d MMM yyyy h:mm tt", CultureInfo.GetCultureInfo("en-AU")))
                .AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table><h2>Manual adjustment audit</h2><table><thead><tr><th>Date</th><th>Location</th><th>Minutes</th><th>Reason</th></tr></thead><tbody>");
        foreach (var adjustment in report.Adjustments)
        {
            builder.Append("<tr><td>").Append(adjustment.WorkDate.ToString("d MMM yyyy", CultureInfo.GetCultureInfo("en-AU"))).Append("</td><td>")
                .Append(adjustment.LocationType).Append("</td><td>")
                .Append(adjustment.Minutes.ToString(CultureInfo.InvariantCulture)).Append("</td><td class=\"text\">")
                .Append(Html(adjustment.Reason)).AppendLine("</td></tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }

    private static string SummaryCard(string label, string value) =>
        $"<div class=\"card\"><span>{Html(label)}</span><strong>{Html(value)}</strong></div>";

    private static string FormatDecimalHours(double hours) =>
        hours.ToString("0.00", CultureInfo.InvariantCulture);

    private static string FormatTime(DateTimeOffset? time) =>
        time?.ToString("HH:mm", CultureInfo.GetCultureInfo("en-AU")) ?? string.Empty;

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private static string EscapeCsv(string value)
    {
        var safeValue = value.Length > 0 && "=+-@\t\r".Contains(value[0], StringComparison.Ordinal)
            ? $"'{value}"
            : value;
        return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
