namespace Avln.ToolBox.Services;

public sealed class FileSystemService
{
    public void CopyFile(string sourcePath, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourcePath, destinationPath, true);
    }

    public void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteEmptyDirectories(string startDirectory, string stopDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        var stop = Path.GetFullPath(stopDirectory).TrimEnd(Path.DirectorySeparatorChar);

        while (current.Exists &&
               !string.Equals(current.FullName.TrimEnd(Path.DirectorySeparatorChar), stop, StringComparison.OrdinalIgnoreCase))
        {
            if (current.EnumerateFileSystemInfos().Any())
            {
                break;
            }

            var parent = current.Parent;
            current.Delete();
            if (parent is null)
            {
                break;
            }

            current = parent;
        }
    }
}
