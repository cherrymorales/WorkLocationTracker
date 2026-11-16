using System.Xml.Linq;

namespace WorkLocationTracker.Tests;

public sealed class InstallerSecurityTests
{
    [Fact]
    public void Installer_UsesDirectGuiShortcutWithoutHiddenShellOrRunKeyPersistence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "Install.ps1"));
        var monitorProject = XDocument.Load(Path.Combine(
            repositoryRoot,
            "src",
            "WorkLocationTracker.Monitor",
            "WorkLocationTracker.Monitor.csproj"));

        Assert.DoesNotContain("Set-ItemProperty", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("powershell.exe", installer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$shortcut.TargetPath = $monitorExecutable", installer, StringComparison.Ordinal);
        Assert.Contains("Remove-ItemProperty", installer, StringComparison.Ordinal);
        Assert.Equal(
            "true",
            monitorProject.Descendants("UseAppHost").Single().Value,
            ignoreCase: true);
        Assert.Equal(
            "WinExe",
            monitorProject.Descendants("OutputType").Single().Value,
            ignoreCase: true);
    }

    [Fact]
    public void MsiInstaller_UsesStandardServiceAndShortcutTablesWithSelfContainedPayloads()
    {
        var repositoryRoot = FindRepositoryRoot();
        var package = XDocument.Load(Path.Combine(repositoryRoot, "installer", "Package.wxs"));
        var buildScript = File.ReadAllText(Path.Combine(repositoryRoot, "scripts", "Build-Installer.ps1"));
        XNamespace wix = "http://wixtoolset.org/schemas/v4/wxs";

        var serviceInstall = Assert.Single(package.Descendants(wix + "ServiceInstall"));
        Assert.Equal("WorkLocationTrackerService", serviceInstall.Attribute("Name")?.Value);
        Assert.Equal("NT AUTHORITY\\LocalService", serviceInstall.Attribute("Account")?.Value);
        Assert.Equal("auto", serviceInstall.Attribute("Start")?.Value);

        var serviceControl = Assert.Single(package.Descendants(wix + "ServiceControl"));
        Assert.Equal("install", serviceControl.Attribute("Start")?.Value);
        Assert.Equal("both", serviceControl.Attribute("Stop")?.Value);
        Assert.Equal("uninstall", serviceControl.Attribute("Remove")?.Value);

        Assert.Single(package.Descendants(wix + "Shortcut"));
        Assert.DoesNotContain("CurrentVersion\\Run", package.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--self-contained", buildScript, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash", buildScript, StringComparison.Ordinal);
        Assert.Contains("CertificateThumbprint", buildScript, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WorkLocationTracker.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Work Location Tracker repository root.");
    }
}
