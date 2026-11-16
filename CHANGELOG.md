# Change log

## 1.24.0 — 16 November 2026

- Replaced the hidden PowerShell and `HKCU` Run-key monitor launch chain with a conventional Windows GUI executable.
- Changed the monitor to open on demand from a direct Start Menu shortcut while the service continues logging automatically.
- Removes the obsolete `WorkLocationTrackerMonitor` sign-in value during upgrades and does not create another user persistence mechanism.
- Added regression coverage preventing hidden-shell and Run-key startup behaviour from returning.
- Added a self-contained x64 MSI that installs, starts, upgrades and removes the Windows service and desktop monitor through standard Windows Installer tables.
- Added Start menu, Programs and Features, Event Log source, data-directory permission and service recovery configuration.
- Added a reproducible installer build with tests, SHA-256 checksum output and explicit optional Authenticode signing.

## 1.22.1 — 13 November 2026

- Added the selected date and weekday to the Daily detail heading.
- Kept the heading synchronised when navigating with the date picker, day buttons or Today shortcut.
- Added view-model and WPF binding regression coverage for the date-aware heading.

## 1.22.0 — 31 October 2026

- Added Edit selected and double-click editing for Holiday and leave records.
- Loads the selected date, type, leave duration and description into the existing form, with Save changes and Cancel edit states.
- Allows an edit to move the record to another unused date through one atomic database update, preventing the original date from being left behind.
- Waits for the stopped service process to exit before replacing installation files, avoiding transient upgrade file locks.
- Added repository, view-model and WPF workflow regression coverage.

## 1.21.0 — 31 October 2026

- Added Public Holiday, Annual Leave and Sick Leave types to Settings, with hours-and-minutes duration for partial-day leave.
- Kept Public Holidays as full-day zero-target dates while Annual and Sick Leave reduce the configured target by their recorded duration.
- Migrated existing Holiday records safely as full-day Public Holidays and expanded Dashboard event details.
- Corrected Completed by to report the point within the target-reaching session when cumulative work met the effective target instead of that session's end.
- Added database migration, partial-leave, period-progress, exact-completion and WPF control regression coverage.

## 1.20.0 — 31 October 2026

- Added a schedule-aware Additional column to the Monthly records table.
- Replaced the Other and Unclassified cards in the monthly footer with a single Additional time total while retaining those location columns in the detailed table.
- Calculated daily and monthly Additional time against each date's effective target, including zero-target weekends, non-working days and Holidays.
- Made the Dashboard integration test independent of the current day of the week and updated Monthly layout regression coverage.

## 1.19.0 — 7 October 2026

- Fixed activity tracking after a hibernate/resume reconnect creates a new Windows session but Terminal Services retains the previous session's last-input timestamp.
- Detects a last-input timestamp earlier than the current session logon and uses the existing unlocked-session fallback instead of recording the user as idle indefinitely.
- Preserves normal five-minute idle detection when Windows supplies a valid current-session timestamp, while locking or signing out continues to stop active time.
- Added regression coverage for stale reconnect timestamps and valid current-session idle calculations.

## 1.18.0 — 2 October 2026

- Added a Home and Office breakdown beneath weekly and monthly target days completed.
- Assigned each completed target day once to its predominant recorded location, so a short secondary-location session does not change the day classification.
- Excluded tied, Other-dominant and Unclassified-dominant days from the Home/Office split while retaining them in the overall completed-day total.
- Added calculation and WPF binding regression coverage for the new breakdown.

## 1.17.0 — 2 October 2026

- Rebuilt Dashboard as three full-width stacked rows for Today, This week and This month.
- Added target days completed, total hours, Additional time and Holiday events to the weekly and monthly summaries.
- Counted configured workdays across each full period while excluding Holidays, with progress and recorded hours calculated only through today.
- Moved Network after Monthly records in the navigation order.
- Renamed user-facing Public holiday wording to Holiday so personal leave and other zero-target dates can be recorded clearly.
- Added period-calculation and WPF layout regression coverage.

## 1.16.0 — 1 October 2026

- Moved the observed-network table and classification controls from Dashboard into a dedicated Network tab.
- Rebuilt Dashboard as a daily overview of total work, target progress and estimated finish, Additional time, breaks and review status.
- Added today's Home, Office, Other and Unclassified totals alongside a compact today-session table.
- Added an Open Daily detail shortcut that opens today's editable sessions.
- Added Dashboard aggregation and six-tab WPF regression coverage.

## 1.15.0 — 30 September 2026

- Added a small target-completion line beneath Daily target.
- Added an Estimated finish time based on the latest recorded session and outstanding target time.
- Prevented an inactive gap today from producing a completion estimate in the past by using the current time as the projection point.
- Added Completed by feedback once cumulative sessions meet the target, and suppressed completion times on zero-target dates.
- Added calculation and WPF binding regression coverage for the new completion information.

## 1.14.0 — 18 September 2026

- Added a Settings tab for the daily target and standard workdays, defaulting to 7 h 36 min on Monday to Friday.
- Added a persistent public-holiday list with optional names and safe deletion.
- Changed non-working days and public holidays to use a zero target, making all recorded work Additional Time on those dates.
- Added clear Daily target explanations for standard workdays, non-working days and public holidays.
- Added persistence, schedule-evaluation and WPF regression coverage for the new settings.

## 1.13.0 — 18 September 2026

- Renamed Work time to Total work time to clarify that it is the complete session total.
- Changed Daily target to show red remaining time below `7 h 36 min` and green Target met status at or above the target.
- Changed Additional time to orange while retaining its time-above-target calculation.
- Expanded target-calculation and WPF colour regression coverage.

## 1.12.0 — 18 September 2026

- Changed the Dashboard to let the Network location card and table fill the available page space.
- Added a `7 h 36 min` Daily target to the Daily detail summary.
- Added Additional time for displayed work beyond the target, without implying payroll overtime or entitlement.
- Added calculation and WPF layout regression coverage for the new summary and stretching network table.

## 1.11.0 — 6 September 2026

- Replaced text-arrow navigation with centred vector chevron icons that scale cleanly.
- Restyled Daily and Monthly date controls as compact grouped selector surfaces.
- Returned the Monthly selector to the same header row as the page title and description.
- Removed redundant Apply buttons and made direct date, month and year selections refresh automatically.
- Shortened the Monthly current-period shortcut while retaining its accessible Current month label.

## 1.10.0 — 6 September 2026

- Added previous-day and next-day arrow buttons to Daily detail with immediate refresh.
- Added previous-month and next-month arrow buttons to Monthly records with immediate refresh.
- Added Today and Current month shortcuts and prevented forward navigation beyond the current period.
- Retained direct calendar and month/year selection with Apply for larger date jumps.
- Added WPF regression coverage for the navigation controls and their forward-navigation state.

## 1.9.0 — 6 September 2026

- Restored Start and End columns to Monthly records and added each day's total session Break.
- Changed monitor duration values and totals from decimal hours to an hours-and-minutes display rounded to the nearest minute.
- Applied the same human-readable duration format to printable HTML reports while retaining numeric decimal hours in CSV exports.
- Added unit and WPF regression coverage for duration formatting and the expanded Monthly records table.

## 1.8.0 — 4 September 2026

- Renamed the reporting tab to Export so the application remains a generic work-location tool.
- Replaced purpose-specific interface and report wording with neutral activity-record guidance.
- Renamed exported files to `work-location-report-*` and the printable report to Work location activity report.
- Kept the optional hourly calculation while describing it as an estimated Home value.
- Added export regression checks that keep generated reports generic.

## 1.7.0 — 4 September 2026

- Added a Delete network action to the Dashboard network-classification controls.
- Added a warning confirmation that reports how many daily records reference the selected network.
- Made network deletion preserve usage records while removing the saved classification, causing linked time to become Unclassified.
- Added a reminder that a deleted network will return as Unclassified if the service observes it again.
- Added an original multi-resolution application icon to the monitor windows and Start Menu shortcut.
- Added persistence and WPF coverage for safe network deletion and icon loading.

## 1.6.0 — 4 September 2026

- Added Ctrl-click and Shift-click multi-selection to Daily detail work sessions.
- Changed Merge to combine two or more selected consecutive sessions in one operation.
- Preserved distinct notes and counted gaps between merged sessions as break time.
- Prevented merging non-consecutive sessions or sessions with different locations.
- Removed Start and End columns from Monthly records because Daily detail contains the reviewed session times.

## 1.5.0 — 4 September 2026

- Moved Edit, Split, Merge and Delete from the Daily detail button row into a session context menu.
- Made right-click select the session under the pointer before opening its actions.
- Kept Add session and Confirm all as the two visible Daily detail actions.
- Moved report settings and exports from Dashboard into a dedicated Export tab.
- Removed the obsolete Manual adjustment entry interface; Daily detail sessions now provide the correction workflow.
- Retained existing manual-adjustment data and its export audit for backwards compatibility.

## 1.4.1 — 4 September 2026

- Fixed the work-session editor action row being clipped at the original fixed window height.
- Made the session and split-session dialogs resizable, with scrollable forms and action buttons fixed at the bottom.
- Added WPF regression coverage that verifies the save and split-confirm buttons remain inside a constrained window.

## 1.4.0 — 4 September 2026

- Added a Daily detail tab for reviewing, adding, editing, splitting, merging, deleting and confirming work sessions.
- Added compact per-day session storage while retaining the one-row-per-calendar-day database design.
- Split activity at local midnight and create separate suggestions after inactive gaps longer than 30 minutes.
- Use confirmed start, finish, break and location values for daily and exported totals.
- Added detailed-session tables to printable HTML and a separate session CSV export.
- Added a backed-up migration that converts existing daily summaries into imported sessions marked for review.
- Added tests for the session migration, coalescing, midnight splitting, overlap protection, reporting, exports and WPF layout.

## 1.3.1 — 4 September 2026

- Fixed the selected navigation tab's white foreground leaking into the page content and making dashboard and monthly totals invisible.
- Added UI regression coverage to ensure the total values retain the normal dark page foreground.

## 1.3.0 — 4 September 2026

- Replaced minute-level usage storage with one transactionally updated `DailyUsage` record per calendar day.
- Retained per-network active seconds inside each daily row so historical network reclassification continues to work.
- Added first-active and last-active time columns to the database, monthly monitor table, CSV report and printable HTML report.
- Added an automatic, data-preserving migration that aggregates existing samples, removes the legacy table and compacts the database.
- Added a one-time pre-v1.3.0 database backup before removing the legacy minute-level table.
- Limited the SQLite write-ahead journal to 4 MB with automatic checkpoints.

## 1.2.1 — 4 September 2026

- Restored the original Monthly records content layout.
- Replaced the default Dashboard and Monthly records selectors with a compact segmented navigation control and a clearer selected state.

## 1.2.0 — 4 September 2026

- Upgraded the service, monitor, shared library and automated tests from .NET 8 to .NET 10.
- Aligned Microsoft.Data.Sqlite, Windows service hosting and service-controller packages to version 10.0.11.
- Updated installation prerequisites and implementation documentation for the .NET 10 Windows Desktop Runtime.

## 1.1.1 — 4 September 2026

- Refined the Monthly records tab with a clearer reporting-period filter, summary cards and a report-style daily table.
- Added alternating table rows, right-aligned numeric values, improved spacing and a helpful empty state.
- Kept the existing monthly calculations and reporting behaviour unchanged.
- Fixed the installer's launch option to open the monitor through its tested hidden shortcut.

## 1.1.0 — 4 September 2026

- Added a Monthly records tab with month and year filters.
- Added a daily table for Home, Office, Other, Unclassified and total hours.
- Added monthly totals and test coverage for daily-row calculations.
- Fixed Windows PowerShell 5.1 compatibility in the upgrade and uninstall process checks.

## 1.0.4 — 3 September 2026

- Hid the signed .NET host's console window while keeping the WPF monitor visible.
- Applied the hidden launch mode to the Start menu shortcut, sign-in startup and installer launch option.

## 1.0.3 — 3 September 2026

- Changed the monitor launcher to use Microsoft's signed .NET host after Trend Micro repeatedly blocked the unsigned generated app-host executable on reputation grounds.
- Updated installation, automatic-start and uninstallation handling for the hosted monitor process.

## 1.0.2 — 3 September 2026

- Fixed local console sessions being classified as permanently idle when Windows returns no last-input timestamp.
- Added a locked-session check and a sanity bound for native idle-time values.

## 1.0.1 — 3 September 2026

- Fixed the monitor startup crash caused by WPF attempting a two-way binding to read-only dashboard properties.

## 1.0.0 — 3 September 2026

- Added the Windows activity-collection service.
- Added local SQLite persistence and network-location classification.
- Added the WPF monitoring dashboard.
- Added reasoned manual adjustments.
- Added printable HTML and CSV activity reports.
- Added secure installation, automatic start and data-preserving uninstallation scripts.
- Added automated persistence, reporting and export-safety tests.
