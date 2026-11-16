namespace WorkLocationTracker.Core;

public sealed class AppPaths
{
    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? ResolveDefaultDataDirectory();
    }

    public string DataDirectory { get; }

    public string DatabasePath => Path.Combine(DataDirectory, AppConstants.DatabaseFileName);

    private static string ResolveDefaultDataDirectory()
    {
        var overridePath = Environment.GetEnvironmentVariable(
            AppConstants.DataDirectoryEnvironmentVariable);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WorkLocationTracker");
    }
}

