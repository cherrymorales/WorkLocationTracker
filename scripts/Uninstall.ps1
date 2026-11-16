[CmdletBinding()]
param(
    [switch]$RemoveData
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'WorkLocationTrackerService'
$installDirectory = Join-Path $env:ProgramFiles 'Work Location Tracker'
$monitorAssembly = Join-Path $installDirectory 'Monitor\WorkLocationTracker.Monitor.dll'
$dataDirectory = Join-Path $env:ProgramData 'WorkLocationTracker'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Work Location Tracker.lnk'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from a PowerShell window opened with Run as administrator.'
}

$legacyMonitorProcesses = @(Get-Process -Name 'WorkLocationTracker.Monitor' -ErrorAction SilentlyContinue)
$hostedMonitorProcessIds = @(
    Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine.IndexOf($monitorAssembly, [StringComparison]::OrdinalIgnoreCase) -ge 0
        } |
        Select-Object -ExpandProperty ProcessId
)

if ($legacyMonitorProcesses.Count -gt 0) {
    $legacyMonitorProcesses | Stop-Process -Force
}

if ($hostedMonitorProcessIds.Count -gt 0) {
    Stop-Process -Id $hostedMonitorProcessIds -Force
}

$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $serviceName -Force
        (Get-Service -Name $serviceName).WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Stopped,
            [TimeSpan]::FromSeconds(30))
    }

    & sc.exe delete $serviceName | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Windows could not remove the service registration.'
    }
}

Remove-ItemProperty -Path $runKeyPath -Name 'WorkLocationTrackerMonitor' -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $shortcutPath -Force -ErrorAction SilentlyContinue

$expectedInstallDirectory = [IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'Work Location Tracker'))
$resolvedInstallDirectory = [IO.Path]::GetFullPath($installDirectory)
if ($resolvedInstallDirectory -ne $expectedInstallDirectory) {
    throw 'Refusing to remove an unexpected installation directory.'
}

if (Test-Path -LiteralPath $resolvedInstallDirectory) {
    Remove-Item -LiteralPath $resolvedInstallDirectory -Recurse -Force
}

if ([System.Diagnostics.EventLog]::SourceExists('Work Location Tracker')) {
    Remove-EventLog -Source 'Work Location Tracker'
}

if ($RemoveData) {
    $expectedDataDirectory = [IO.Path]::GetFullPath((Join-Path $env:ProgramData 'WorkLocationTracker'))
    $resolvedDataDirectory = [IO.Path]::GetFullPath($dataDirectory)
    if ($resolvedDataDirectory -ne $expectedDataDirectory) {
        throw 'Refusing to remove an unexpected data directory.'
    }

    if (Test-Path -LiteralPath $resolvedDataDirectory) {
        Remove-Item -LiteralPath $resolvedDataDirectory -Recurse -Force
        Write-Host 'The local usage database was permanently removed.' -ForegroundColor Yellow
    }
}
else {
    Write-Host "Usage data was preserved at $dataDirectory"
}

Write-Host 'Work Location Tracker was uninstalled.' -ForegroundColor Green
