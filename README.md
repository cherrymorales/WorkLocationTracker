# Work Location Tracker

Work Location Tracker is a private Windows application that records when this computer appears active and which network it is connected to. You classify observed networks as **Home**, **Office** or **Other**, then export the activity records for your chosen period.

The solution has two installed components:

- **Work Location Tracker Service** starts with Windows and updates one compact daily usage record every minute.
- **Work Location Tracker Monitor** starts when you sign in and provides the dashboard, network classification, daily session review and report export.

The service and monitor are separate because Windows services are intentionally unable to display interactive desktop windows.

On some Windows systems, a hibernate/resume reconnect can create a new console session while Terminal Services retains the previous session's last-input timestamp. The service detects this invalid state and falls back to the current session's lock state, preventing an unlocked working session from being recorded as idle indefinitely. Normal five-minute idle detection remains in effect whenever Windows supplies a valid timestamp; locking or signing out always stops active time.

## What it records

The local SQLite database keeps one usage-summary row per calendar day. That row records:

- the first and last active sample times;
- accumulated active seconds for each observed network;
- accumulated idle seconds, based on the Windows session's lock state and a configurable five-minute idle threshold when Windows supplies a last-input timestamp; and
- a compact list of daily work sessions, including start, finish, inferred or reviewed break minutes, location, review status and an optional note; and
- the computer name.

Network classifications remain in a separate compact table so historical usage can be reclassified. Existing manual-adjustment records are retained for backwards-compatible reporting, although new corrections are made through Daily detail sessions. Existing minute-sample rows are automatically combined into daily records and removed during the upgrade. Before that migration, the application creates one recoverable `work-location-tracker.pre-v1.3.0-*.db` backup beside the active database.

It does **not** record application names, window titles, websites, keystrokes, clipboard contents, files, packet contents or browsing history. No information is uploaded by this application.

## Install

### MSI installer — recommended

The self-contained x64 MSI includes the required .NET 10 runtime. On a Windows 10 or Windows 11 x64 computer:

1. Copy `WorkLocationTracker-1.24.0-win-x64.msi` to the computer.
2. Double-click the MSI and approve the Windows administrator prompt.
3. Complete the setup wizard.
4. Open **Work Location Tracker** from the Start menu.

The MSI registers and starts the service under `LocalService`, installs the desktop monitor, creates the Event Log source and Start menu shortcut, and adds the application to **Settings > Apps > Installed apps**. Upgrades preserve the existing database. Uninstalling removes the service and application files but deliberately preserves usage data under `C:\ProgramData\WorkLocationTracker`.

The distributed MSI should be Authenticode-signed with an approved code-signing certificate. An unsigned development build can display an Unknown publisher warning; do not work around that warning with antivirus exclusions.

### Development installation

This workstation has the required .NET 10 SDK and Windows Desktop Runtime. The PowerShell installation remains available for local development:

1. Open PowerShell using **Run as administrator**.
2. Change to this folder.
3. Run:

   ```powershell
   Set-ExecutionPolicy -Scope Process Bypass
   .\scripts\Install.ps1 -LaunchMonitor
   ```

The installer builds Release binaries, registers an automatically starting Windows service under the lower-privilege `LocalService` account and creates a Start Menu shortcut for the monitor. Application data is stored in:

```text
C:\ProgramData\WorkLocationTracker\work-location-tracker.db
```

Only the installing Windows user, local administrators, `SYSTEM` and `LocalService` receive access to that directory.

The service starts with Windows and records activity without the monitor being open. The monitor is a conventional GUI executable that opens on demand from the Start menu without a console window. The installer removes the previous hidden PowerShell sign-in command and does not create user auto-start persistence or antivirus exclusions.

The monitor's **Daily detail** tab groups nearby computer activity into work sessions. Its heading displays the selected date and weekday. Use the chevron buttons to move one day at a time, **Today** to return to the current date, or the calendar to jump directly to another date. Date changes refresh automatically. Choose a date to add sessions or confirm all suggestions; right-click a session to edit, split, merge or delete it. Use Ctrl-click or Shift-click to select two or more consecutive sessions and merge them in one operation. Merged sessions must have the same location; their intervening gaps become break time. You can also double-click a single session to edit it. Activity separated by more than 30 minutes becomes a new suggested session, and activity crossing midnight is split between the two local calendar dates. Confirmed sessions drive the daily and exported totals.

The **Monthly records** tab keeps a compact reporting-month selector aligned in its header. Use the chevron buttons to move one month at a time, **Current** to return to the current month, or select a month and year directly. Selection changes refresh automatically. The table shows each day's first start, last end, total break, Home, Office, Other, Unclassified, Additional and total durations. Its footer summarises Home, Office, Additional time and total work. Additional time uses the effective target for each date, so all work on weekends, configured non-working days and Holidays is included. Use Daily detail when you need to review the individual sessions behind those daily figures.

The monitor and printable HTML report display durations as hours and minutes, rounded to the nearest minute. CSV exports retain decimal hours so their numeric columns remain suitable for spreadsheet calculations.

The **Export** tab lets you choose the reporting period and an optional hourly rate, then export printable HTML or CSV records. When supplied, the rate is multiplied by Home hours to provide an estimated value; leave it at `$0.00` when you only need time records.

The **Network** tab contains the observed-network table and all classification controls. It can also delete an obsolete observed network. If daily records reference it, the confirmation states how many days are affected. Deletion removes only the saved network identity and classification: recorded hours and sessions remain, and linked time becomes Unclassified. A network observed again by the service is automatically added back as Unclassified.

The **Dashboard** has three full-width rows: Today, This week and This month. Today retains total work, target and estimated finish, Additional time and breaks. The weekly and monthly rows show completed target days against all scheduled workdays in the full period, total recorded hours through today, Additional time and configured Holiday or leave events. Beneath each target-day total, Home and Office counts assign a completed day to whichever location has the greatest recorded duration. A tie, or a day dominated by Other or Unclassified time, remains in the overall completed total but is not included in the Home/Office split. Scheduled-day totals exclude non-working days, Public Holidays and full-day leave; partial leave remains a scheduled day with its reduced target. Future workdays remain in the denominator but are not counted as missed targets. **Open Daily detail** takes you directly to today's editable sessions.

In Daily detail, **Total work time** is the complete session total for the selected day. On a standard workday, **Daily target** shows the remaining duration in red while work is below the configured target, then changes to a green **Target met** status. A smaller line estimates the finish time from the latest recorded session and remaining target time, assuming work continues without another break. If today's latest session has already ended, the estimate starts from the current time instead of showing a misleading time in the past. Once the target is met, **Completed by** identifies the point inside the target-reaching session when cumulative work reached the effective target rather than displaying the session's eventual end. Because sessions store total break duration rather than break timestamps, a reviewed break is treated as occurring before that completion point. On a non-working day or full-day leave, the target is zero and no completion time applies. **Additional time** is orange and shows work beyond the effective target, which means all work on those zero-target dates. This comparison is informational and does not determine overtime, pay or approval.

The **Settings** tab stores the daily target, standard workdays, Public Holidays, Annual Leave and Sick Leave. Public Holidays are always full-day zero-target dates. Annual and Sick Leave accept hours and minutes and reduce the configured target for that date, so a partial day retains only the remaining required work time; entering the complete daily target records full-day leave. Select a Holiday or leave row and choose **Edit selected**, or double-click it, to update its date, type, duration or description. **Cancel edit** returns the form to Add mode without saving. The default schedule is `7 h 36 min`, Monday to Friday. Changing these settings never deletes or rewrites recorded work sessions.

## First use

1. Leave the service running for at least one minute so it can observe the current network.
2. Open **Work Location Tracker** from the Start Menu.
3. Open **Network**, select the current network and choose **Mark as Home** or **Mark as Office**.
4. Label every network shown as unclassified. A new classification is also applied to earlier samples from that network.
5. Open **Settings** and confirm the daily target and standard workdays. Add Public Holidays and record Annual or Sick Leave with the applicable duration.
6. Open **Daily detail**, choose a date and review the suggested sessions against your calendar or timesheet. Correct the start, finish, break and location, then save or use **Confirm all**. Review a day after its work has finished so later activity is not left as a new suggestion.
7. Add or edit a Daily detail session when activity detection is not representative—for example, a client call while you were not touching the computer, or personal time that should be excluded. Record a useful note explaining the correction.
8. Open **Export**, choose the report period and optionally enter an hourly rate. The default is `$0.00`, so no value is implied when you only need time records.
9. Export printable HTML or CSV. CSV export creates daily, detailed-session and historical adjustment-audit files.

Only delete an obsolete network after reviewing the warning. Deleting the current network is temporary because the running service will observe and add it again.

## Reviewing exported records

Computer activity cannot determine whether every minute was work-related. Review each export against your calendar, timesheets, leave and breaks. Keep copies of the exported reports and the underlying database when you need a longer-term record. The application does not collect application content, expense information or evidence of the work performed.

## Configuration

The installed service reads `Service\appsettings.json`. Defaults are:

```json
{
  "Tracking": {
    "SampleIntervalSeconds": 60,
    "IdleThresholdMinutes": 5,
    "MaximumGapSeconds": 150
  }
}
```

- `SampleIntervalSeconds` controls how frequently the current day's single usage record is updated.
- `IdleThresholdMinutes` controls how long Windows can receive no input before a sample is considered idle. If Windows does not supply a last-input timestamp for the local console, unlocked-session time is used instead. These are activity proxies, not work rules.
- `MaximumGapSeconds` prevents a long sleep, hibernation or stalled interval from being counted as active time.

Restart the service after changing these values:

```powershell
Restart-Service WorkLocationTrackerService
```

## Build and test

```powershell
dotnet restore .\WorkLocationTracker.sln
dotnet build .\WorkLocationTracker.sln -c Release --no-restore
dotnet test .\WorkLocationTracker.sln -c Release --no-build
```

Build the self-contained MSI and its SHA-256 checksum with:

```powershell
.\scripts\Build-Installer.ps1
```

The shareable files are written to `artifacts\installer`. To sign the application executables and MSI, supply the thumbprint of an explicitly approved code-signing certificate:

```powershell
.\scripts\Build-Installer.ps1 -CertificateThumbprint 'YOUR_CERTIFICATE_THUMBPRINT'
```

The build script runs the automated tests, publishes self-contained x64 service and monitor payloads, creates the compressed MSI, optionally signs it and writes a `.sha256` checksum file.

## Back up or uninstall

Stop the service before copying the database for a consistent manual backup:

```powershell
Stop-Service WorkLocationTrackerService
Copy-Item 'C:\ProgramData\WorkLocationTracker\work-location-tracker.db*' 'D:\YourSecureBackupFolder'
Start-Service WorkLocationTrackerService
```

For an MSI installation, uninstall **Work Location Tracker** from **Settings > Apps > Installed apps**. The database is preserved. Delete `C:\ProgramData\WorkLocationTracker` manually only when its records and backups are no longer required.

For a legacy development installation, uninstall the binaries and preserve the database with:

```powershell
.\scripts\Uninstall.ps1
```

To permanently remove the usage database as well:

```powershell
.\scripts\Uninstall.ps1 -RemoveData
```

The uninstall script reports explicitly whether it preserved or removed the data.

## Current limitations

- Activity detection uses the active local Windows console session. It measures keyboard/mouse inactivity when Windows supplies that timestamp; otherwise it counts signed-in, unlocked time. Reading, calls, meetings and unlocked personal time may require adding or editing a Daily detail session.
- The tracker cannot establish whether detected activity was work or private activity.
- Suggested break time is inferred from computer inactivity within a session. It can include legitimate calls, meetings or reading, so it must be reviewed rather than treated automatically as a personal break.
- Windows network profiles occasionally change identity after a router reset or a Windows network reset; label the new observed profile when this happens.
- The service records the active local console session; Remote Desktop-only work should be reconciled manually.
- Report values are only as complete as the period for which the service was installed and running.
