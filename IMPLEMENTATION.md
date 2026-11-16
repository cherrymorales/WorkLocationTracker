# Implementation record

## Purpose and boundaries

This implementation creates a Windows-only, local-first work-location tracker. Its boundary is deliberately narrow: it records coarse computer activity and network identity, provides user classification and correction, and exports supporting records. It does not inspect work content or decide whether activity was work-related.

## Architecture

`WorkLocationTracker.Service` is a .NET 10 Worker Service registered with Windows. Every minute it reads the active console session's lock state and last-input time through the Windows Terminal Services API and obtains connected profile identities through Windows Network List Manager. It hashes profile GUIDs with SHA-256 before storage. A network-interface-and-gateway fingerprint is used if Network List Manager is unavailable.

`WorkLocationTracker.Core` owns validation, SQLite persistence, location mapping, report aggregation and safe CSV/HTML generation. SQLite keeps one `DailyUsage` row per calendar day and updates that row transactionally as observations arrive. Per-network active seconds and a small, coalesced work-session list are held as JSON inside the row, so the database remains compact and later Home/Office/Other classification still corrects unedited historical records. SQLite runs in write-ahead logging mode with a 4 MB journal-size limit, automatic and startup checkpoints, short-lived pooled connections and a busy timeout so the service and monitor can use the database concurrently.

`WorkLocationTracker.Monitor` is a .NET 10 WPF application. Its Dashboard is a read-only summary with three stacked rows: Today, This week and This month. Today shows total work, target progress and projected finish, Additional time and breaks; the period rows show target days completed, recorded hours, Additional time and Holiday or leave events. A shortcut opens today's editable Daily detail. Network observation, classification and deletion are isolated in a dedicated Network tab after Monthly records; deletion removes the profile and classification only, after showing the count of linked daily records, while retained linked usage is reported as Unclassified. Daily detail reviews suggested sessions, compares work time with the effective target for that date, keeps add and confirm actions visible, and provides edit, split, multi-select merge and delete actions from each row's context menu. Daily and Monthly headers use compact grouped selectors with vector chevrons, bounded previous/next navigation, current-period shortcuts and automatically refreshed picker controls. Monthly records displays each day's first start, last end, total session break, Home, Office, Other, Unclassified, Additional and overall duration. Its footer summarises Home, Office, Additional time and total work; individual sessions remain in Daily detail. Export produces daily, detailed-session and historical adjustment-audit reports. Settings persists the daily target, standard workdays and typed Holiday or leave dates in two small relational tables separate from activity records. Holiday and leave rows support button or double-click editing, including an atomic date change that cannot leave the old record behind.

The monitor is published as a normal Windows GUI app-host executable and is launched directly from its Start Menu shortcut. It does not use a hidden command shell, a `Run` registry value or another user auto-start mechanism. The Windows service starts automatically and records activity independently, so the monitor only needs to run while the user is reviewing data. The installer removes the obsolete hidden PowerShell sign-in entry and does not create an antivirus exclusion or weaken endpoint protection.

`WorkLocationTracker.Installer` is a WiX Toolset 5 per-machine MSI for x64 Windows. The reproducible build publishes self-contained .NET 10 service and WPF payloads, embeds them in a compressed MSI, registers the service through the Windows Installer service tables, configures LocalService and recovery actions, creates the Event Log source and Start menu shortcut, and applies the shared data-directory access rules. Runtime configuration and dependency manifests are MSI companion files so an upgrade from the legacy script installation cannot retain framework-dependent launch metadata. Uninstall removes registered components while leaving the untracked SQLite database and backups in ProgramData. The build optionally Authenticode-signs application binaries and the MSI only when an approved certificate thumbprint is supplied.

## Important calculation decisions

- A sample is active when an interactive local user exists and the current idle time is no greater than the configured threshold.
- When Windows does not supply a valid last-input timestamp for the local console, the service uses unlocked-session time as the activity proxy. Locking or logging out stops active time in this fallback mode.
- A last-input timestamp earlier than the current session logon is also invalid. This can occur when hibernate/resume disconnects one console session and creates another while retaining the old timestamp. The service uses the same unlocked-session fallback rather than treating the new session as permanently idle.
- The complete sampling interval is marked active or idle. At a one-minute sampling interval this creates at most one sampling interval of boundary imprecision.
- An interval longer than `MaximumGapSeconds` is skipped, preventing suspended time from being counted.
- A day's start and end are the first and last active sample boundaries. They are not sign-in/sign-out times, and idle or locked samples do not extend them.
- Samples that cross local midnight are divided between the two calendar dates before aggregation.
- Active samples separated by no more than 30 minutes are coalesced into one suggested work session. Longer inactive gaps create separate sessions, while inactivity inside a session becomes suggested break time.
- Suggested session totals use tracked active seconds. Once confirmed, a session uses its reviewed `finish - start - break` duration and its reviewed location for daily and exported totals.
- Confirmed sessions are not mutated by later overlapping automatic observations. Activity after a confirmed session becomes a new suggestion, so days should normally be confirmed after work has finished.
- Multi-select merge accepts two or more consecutive sessions with the same location, preserves distinct notes, and converts the gaps between them into break minutes in the resulting confirmed session.
- Daily records retain accumulated seconds by network identity, while Home/Office/Other classification is joined at report time. This permits a later label to correct historical classification without retaining minute-level rows.
- The upgrade migration first creates a consistent, uniquely named pre-v1.3.0 SQLite backup, then combines legacy minute samples into one daily row, drops the legacy sample table, checkpoints the write-ahead log and vacuums the active database to recover unused pages.
- The v1.4 migration creates a separate pre-v1.4.0 backup and converts every existing daily summary into one imported session marked for review. No historical daily total is discarded.
- Historical manual adjustments remain append-only audit entries and continue to affect reports; the monitor no longer creates them because corrections are now represented by Daily detail sessions. Negative historical totals are clamped to zero per location and day.
- The estimated Home value is Home hours multiplied by an optional user-entered hourly rate. The application defaults the rate to zero so time records can be exported without implying a monetary value.
- Human-readable durations in the monitor and printable HTML are rounded to the nearest minute and displayed as hours and minutes. CSV exports retain decimal hours for calculations and compatibility.
- The default Daily target is 7.6 hours, stored as exactly 456 minutes, with Monday to Friday selected as standard workdays. Both the duration and workday set are user-configurable.
- A Public Holiday, full-day leave or weekday not selected in Settings has an effective target of zero. Annual and Sick Leave reduce the configured target by their recorded minutes, allowing partial-day leave. All work beyond the resulting effective target is Additional time.
- On a configured workday with a positive effective target, the summary displays the remaining duration in red below target and Target met in green at or above target. Additional time is `max(0, rounded work minutes - effective target minutes)`, is displayed in orange and does not classify payroll overtime or entitlement.
- Weekly periods run Monday to Sunday. Weekly and monthly target-day denominators count configured workdays with a positive effective target, excluding Public Holidays and full-day leave while retaining partial-leave days. Completed target days use each date's reduced target and recorded hours only through today. Each completed target day is assigned to Home or Office only when that location has strictly more recorded time than every other location. Tied, Other-dominant and Unclassified-dominant days remain in the overall completed count but not the Home/Office split. Holiday and leave events are listed for the full period so upcoming dates remain visible.
- While a target remains, Daily detail estimates its completion from the later of the latest recorded session end or the current time for today, plus the remaining target minutes. This assumes work continues without another break. Once met, it locates the completion point inside the target-reaching session from the remaining required work and reviewed break duration instead of reporting that session's end. Because only total break duration is stored, the calculation treats that break as occurring before completion. Zero-target dates do not show a completion time.

## Security and privacy

- The service uses the built-in `LocalService` account rather than `LocalSystem`.
- The MSI restricts the data directory to `SYSTEM`, administrators, `LocalService` and interactive Windows users so the service and desktop monitor can share the local database.
- Network IDs are one-way hashes; profile display names remain visible so a user can classify them.
- Deleting a network never cascades into daily usage or work-session data; the conditional warning explains the resulting Unclassified time before confirmation.
- Report HTML encodes user-controlled values. CSV adjustment reasons are quoted and formula-like prefixes are neutralised to reduce spreadsheet formula-injection risk.
- SQL values are parameterised and manually entered reasons and durations are bounded.
- No server, telemetry endpoint or automatic update channel is present.

## Verification scope

Automated tests cover daily-row upserts, duplicate-sample protection, legacy and daily-summary migrations, session coalescing, midnight splitting, confirmed-session preservation, overlap rejection, classification, safe network deletion with linked-usage preservation, work-schedule and Holiday persistence, weekday/weekend/Holiday target evaluation, target-completion estimates, weekly and monthly Dashboard progress, manual-adjustment validation, session-aware report aggregation, start/end and break presentation, hour-minute duration formatting, daily-target calculations, negative-adjustment clamping, HTML encoding, CSV formula neutralisation and the real Windows activity/network providers. WPF regression coverage verifies all six navigation tabs in order, the dedicated stretching Network layout, the three stacked Dashboard rows, bounded Daily and Monthly date navigation controls, the schedule-aware Daily summary and completion line, the Daily context menu, the detailed-session table, Monthly record columns, Settings controls, application icons and readable total colours. Installer-authoring tests prevent hidden-shell or Run-key persistence and verify the MSI service, shortcut, self-contained payload and signing/checksum build configuration. Windows Installer integration is additionally tested with an elevated install, legacy migration, service start, monitor launch, uninstall and reinstall because registering a service and changing access-control lists are operating-system changes.

The installed monitor is also launch-tested for long enough to complete WPF's deferred layout and binding pass. This is important because a short process-only check can finish before deferred binding errors are raised.

## Recommended manual acceptance test

1. Run the installer as administrator and confirm `WorkLocationTrackerService` is Running.
2. Wait at least one minute and open the monitor.
3. Confirm the displayed network matches the connected network profile.
4. Mark it Home, wait another minute and confirm the Daily detail or Monthly records Home total increases after refresh.
5. Select a linked network, choose Delete network and verify the warning shows the affected daily-record count; choose No unless the network is genuinely obsolete.
6. Lock Windows and confirm that new active time stops increasing. If Windows supplies last-input timestamps on the test computer, also leave it untouched beyond the idle threshold and confirm that active time stops increasing.
7. Sleep and resume the computer; confirm the sleeping interval is not included.
8. Open Daily detail, verify that a long inactive gap creates two suggestions, edit the break and location, then confirm the day.
9. Right-click a session and verify Edit, Split, Merge and Delete target that row; also verify double-click opens Edit.
10. Open Export, export HTML and CSV, check the daily totals, detailed sessions and any historical adjustment audit, and print-preview the HTML report.
11. Restart Windows and confirm the service and monitor start automatically.
