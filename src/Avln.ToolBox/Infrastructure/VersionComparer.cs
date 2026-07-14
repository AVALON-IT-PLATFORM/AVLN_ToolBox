namespace Avln.ToolBox.Infrastructure;

public static class VersionComparer
{
    public static bool IsNewer(string candidate, string current)
    {
        return Compare(candidate, current) > 0;
    }

    public static int Compare(string left, string right)
    {
        var leftVersion = Parse(left);
        var rightVersion = Parse(right);
        return leftVersion.CompareTo(rightVersion);
    }

    private static Version Parse(string value)
    {
        var normalized = (value ?? string.Empty).Trim().TrimStart('v', 'V');
        var prereleaseSeparator = normalized.IndexOf('-');
        if (prereleaseSeparator >= 0)
        {
            normalized = normalized[..prereleaseSeparator];
        }

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count < 3)
        {
            parts.Add("0");
        }

        if (parts.Count > 4)
        {
            parts = parts.Take(4).ToList();
        }

        return Version.TryParse(string.Join('.', parts), out var version)
            ? version
            : new Version(0, 0, 0);
    }
}
