[CmdletBinding()]
param(
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$monitorProject = Join-Path $repositoryRoot 'src\WorkLocationTracker.Monitor\WorkLocationTracker.Monitor.csproj'
$serviceProject = Join-Path $repositoryRoot 'src\WorkLocationTracker.Service\WorkLocationTracker.Service.csproj'
$installerProject = Join-Path $repositoryRoot 'installer\WorkLocationTracker.Installer.wixproj'
$solution = Join-Path $repositoryRoot 'WorkLocationTracker.sln'
$publishRoot = Join-Path $repositoryRoot 'artifacts\installer-publish'
$servicePublish = Join-Path $publishRoot 'Service'
$monitorPublish = Join-Path $publishRoot 'Monitor'
$installerOutput = Join-Path $repositoryRoot 'artifacts\installer'

[xml]$monitorProjectXml = Get-Content -LiteralPath $monitorProject
$productVersion = [string]$monitorProjectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($productVersion)) {
    throw 'Could not read the monitor version from the project file.'
}

function Assert-ChildPath {
    param([Parameter(Mandatory)] [string]$Path)

    $resolvedRoot = [IO.Path]::GetFullPath($repositoryRoot)
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $resolvedPath"
    }
}

function Invoke-DotNet {
    param([Parameter(Mandatory)] [string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-SignTool {
    param([Parameter(Mandatory)] [string]$Path)

    $signTool = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\x64\signtool.exe' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signTool) {
        throw 'signtool.exe was not found in the Windows SDK.'
    }

    & $signTool.FullName sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $Path
    if ($LASTEXITCODE -ne 0) {
        throw "Signing failed for $Path."
    }
}

Assert-ChildPath -Path $publishRoot
Assert-ChildPath -Path $installerOutput

if (-not $SkipTests) {
    Invoke-DotNet -Arguments @('test', $solution, '-c', 'Release')
}

foreach ($directory in @($publishRoot, $installerOutput)) {
    if (Test-Path -LiteralPath $directory) {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$publishArguments = @(
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

Invoke-DotNet -Arguments (@('publish', $serviceProject) + $publishArguments + @('--output', $servicePublish))
Invoke-DotNet -Arguments (@('publish', $monitorProject) + $publishArguments + @('--output', $monitorPublish))

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $CertificateThumbprint -and $_.HasPrivateKey } |
        Select-Object -First 1
    if (-not $certificate) {
        throw "No code-signing certificate with private key was found for thumbprint $CertificateThumbprint."
    }

    Invoke-SignTool -Path (Join-Path $servicePublish 'WorkLocationTracker.Service.exe')
    Invoke-SignTool -Path (Join-Path $servicePublish 'WorkLocationTracker.Service.dll')
    Invoke-SignTool -Path (Join-Path $monitorPublish 'WorkLocationTracker.Monitor.exe')
    Invoke-SignTool -Path (Join-Path $monitorPublish 'WorkLocationTracker.Monitor.dll')
    Invoke-SignTool -Path (Join-Path $monitorPublish 'WorkLocationTracker.Core.dll')
}

Invoke-DotNet -Arguments @(
    'build', $installerProject,
    '-c', 'Release',
    "-p:ProductVersion=$productVersion",
    "-p:ServicePublishDir=$servicePublish",
    "-p:MonitorPublishDir=$monitorPublish"
)

$msiPath = Join-Path $installerOutput "WorkLocationTracker-$productVersion-win-x64.msi"
if (-not (Test-Path -LiteralPath $msiPath)) {
    throw "The expected MSI was not created: $msiPath"
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Invoke-SignTool -Path $msiPath
}

$hash = Get-FileHash -LiteralPath $msiPath -Algorithm SHA256
$checksumPath = "$msiPath.sha256"
Set-Content -LiteralPath $checksumPath -Value "$($hash.Hash)  $(Split-Path -Leaf $msiPath)" -Encoding Ascii

Write-Host ''
Write-Host 'Installer created successfully.' -ForegroundColor Green
Write-Host "MSI: $msiPath"
Write-Host "SHA-256: $($hash.Hash)"
if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    Write-Warning 'The MSI is unsigned. Use -CertificateThumbprint with an approved code-signing certificate before public distribution.'
}
