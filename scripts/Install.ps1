[CmdletBinding()]
param(
    [switch]$LaunchMonitor
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$serviceName = 'WorkLocationTrackerService'
$displayName = 'Work Location Tracker'
$sourceRoot = Split-Path -Parent $PSScriptRoot
$installDirectory = Join-Path $env:ProgramFiles 'Work Location Tracker'
$serviceDirectory = Join-Path $installDirectory 'Service'
$monitorDirectory = Join-Path $installDirectory 'Monitor'
$dataDirectory = Join-Path $env:ProgramData 'WorkLocationTracker'
$monitorAssembly = Join-Path $monitorDirectory 'WorkLocationTracker.Monitor.dll'
$monitorExecutable = Join-Path $monitorDirectory 'WorkLocationTracker.Monitor.exe'
$serviceExecutable = Join-Path $serviceDirectory 'WorkLocationTracker.Service.exe'
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Work Location Tracker.lnk'
$runKeyPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$publishRoot = Join-Path ([IO.Path]::GetTempPath()) ("WorkLocationTracker.Publish.{0}" -f [Guid]::NewGuid().ToString('N'))

function Assert-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from a PowerShell window opened with Run as administrator.'
    }
}

function Invoke-DotNetPublish {
    param(
        [Parameter(Mandatory)] [string]$Project,
        [Parameter(Mandatory)] [string]$Output
    )

    & dotnet publish $Project -c Release -r win-x64 --self-contained false --output $Output
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project."
    }
}

function Invoke-Sc {
    param([Parameter(ValueFromRemainingArguments)] [string[]]$Arguments)

    & sc.exe @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Service configuration failed: sc.exe $($Arguments -join ' ')"
    }
}

function Get-RunningMonitor {
    $legacyProcesses = @(Get-Process -Name 'WorkLocationTracker.Monitor' -ErrorAction SilentlyContinue)
    $hostedProcesses = @(
        Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                $_.CommandLine -and
                $_.CommandLine.IndexOf($monitorAssembly, [StringComparison]::OrdinalIgnoreCase) -ge 0
            }
    )

    return @($legacyProcesses) + @($hostedProcesses)
}

Assert-Administrator

try {
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
    $servicePublish = Join-Path $publishRoot 'Service'
    $monitorPublish = Join-Path $publishRoot 'Monitor'

    Write-Host 'Publishing the service and monitor...'
    Invoke-DotNetPublish -Project (Join-Path $sourceRoot 'src\WorkLocationTracker.Service\WorkLocationTracker.Service.csproj') -Output $servicePublish
    Invoke-DotNetPublish -Project (Join-Path $sourceRoot 'src\WorkLocationTracker.Monitor\WorkLocationTracker.Monitor.csproj') -Output $monitorPublish

    $runningMonitor = @(Get-RunningMonitor)
    if ($runningMonitor.Count -gt 0) {
        Write-Host 'Closing the running monitor...'
        foreach ($monitor in $runningMonitor) {
            $processId = if ($monitor.PSObject.Properties.Name -contains 'ProcessId') { $monitor.ProcessId } else { $monitor.Id }
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if (-not $process) {
                continue
            }

            [void]$process.CloseMainWindow()
            try {
                $process.WaitForExit(10000)
            }
            catch {
                # The process may have exited between discovery and the wait call.
            }

            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force
                $process.WaitForExit(5000)
            }
        }

        $runningMonitor = @(Get-RunningMonitor)
    }

    if ($runningMonitor.Count -gt 0) {
        $runningMonitorDescriptions = $runningMonitor | ForEach-Object {
            $processId = if ($_.PSObject.Properties.Name -contains 'ProcessId') { $_.ProcessId } else { $_.Id }
            $processName = if ($_.PSObject.Properties.Name -contains 'Name') { $_.Name } else { $_.ProcessName }
            "{0} (process {1})" -f $processName, $processId
        }
        throw "Close Work Location Tracker before installing or upgrading it. Detected: $($runningMonitorDescriptions -join ', ')."
    }

    $existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($existingService -and $existingService.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Write-Host 'Stopping the existing service...'
        Stop-Service -Name $serviceName -Force
        (Get-Service -Name $serviceName).WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Stopped,
            [TimeSpan]::FromSeconds(30))

        $serviceExitDeadline = [DateTime]::UtcNow.AddSeconds(10)
        do {
            $serviceProcesses = @(Get-Process -Name 'WorkLocationTracker.Service' -ErrorAction SilentlyContinue)
            if ($serviceProcesses.Count -eq 0) {
                break
            }

            Start-Sleep -Milliseconds 250
        } while ([DateTime]::UtcNow -lt $serviceExitDeadline)

        if ($serviceProcesses.Count -gt 0) {
            throw 'The stopped service process did not exit before the installation timeout.'
        }
    }

    New-Item -ItemType Directory -Path $serviceDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $monitorDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null

    Copy-Item -Path (Join-Path $servicePublish '*') -Destination $serviceDirectory -Recurse -Force
    Copy-Item -Path (Join-Path $monitorPublish '*') -Destination $monitorDirectory -Recurse -Force

    if (-not (Test-Path -LiteralPath $monitorExecutable)) {
        throw 'The published monitor executable is missing.'
    }

    if (-not $existingService) {
        New-Service `
            -Name $serviceName `
            -BinaryPathName ('"{0}"' -f $serviceExecutable) `
            -DisplayName $displayName `
            -Description 'Records local computer activity and network identity for work-location reports.' `
            -StartupType Automatic | Out-Null
    }
    else {
        Invoke-Sc config $serviceName binPath= ('"{0}"' -f $serviceExecutable) start= auto
    }

    # LocalService has fewer privileges than LocalSystem and only needs write access to this app's data folder.
    Invoke-Sc config $serviceName obj= 'NT AUTHORITY\LocalService'
    Invoke-Sc description $serviceName 'Records local computer activity and network identity for work-location reports.'
    Invoke-Sc failure $serviceName reset= 86400 actions= restart/5000/restart/15000/none/0

    if (-not [System.Diagnostics.EventLog]::SourceExists($displayName)) {
        New-EventLog -LogName Application -Source $displayName
    }

    $currentUserSid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls.exe $dataDirectory /inheritance:r /grant:r `
        '*S-1-5-18:(OI)(CI)F' `
        '*S-1-5-19:(OI)(CI)M' `
        '*S-1-5-32-544:(OI)(CI)F' `
        "*$($currentUserSid):(OI)(CI)M" | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not secure the application data directory.'
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $monitorExecutable
    $shortcut.Arguments = ''
    $shortcut.WorkingDirectory = $monitorDirectory
    $shortcut.Description = 'Monitor work location and export working-from-home reports'
    $shortcut.IconLocation = "$monitorExecutable,0"
    $shortcut.WindowStyle = 1
    $shortcut.Save()

    # Remove the previous hidden PowerShell auto-start entry. The service records
    # activity independently; the monitor is now an on-demand desktop interface.
    Remove-ItemProperty -Path $runKeyPath -Name 'WorkLocationTrackerMonitor' -ErrorAction SilentlyContinue

    Start-Service -Name $serviceName
    (Get-Service -Name $serviceName).WaitForStatus(
        [System.ServiceProcess.ServiceControllerStatus]::Running,
        [TimeSpan]::FromSeconds(30))

    Write-Host ''
    Write-Host 'Work Location Tracker was installed successfully.' -ForegroundColor Green
    Write-Host "Application: $monitorExecutable"
    Write-Host "Local database: $(Join-Path $dataDirectory 'work-location-tracker.db')"
    Write-Host 'Open the monitor from the Start menu when you want to review activity.'

    if ($LaunchMonitor) {
        Start-Process -FilePath $shortcutPath
    }
}
finally {
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedPublishRoot = [IO.Path]::GetFullPath($publishRoot)
    if ($resolvedPublishRoot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedPublishRoot).StartsWith('WorkLocationTracker.Publish.', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedPublishRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
