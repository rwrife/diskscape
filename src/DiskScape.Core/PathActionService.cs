namespace DiskScape.Core;

public sealed class PathActionService
{
    private readonly IPathActionExecutor _executor;

    public PathActionService(IPathActionExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public void Delete(string path, bool isDirectory, DeleteMode mode = DeleteMode.RecycleBin)
    {
        var fullPath = NormalizePath(path);

        if (isDirectory)
        {
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {fullPath}");
            }

            _executor.DeleteDirectory(fullPath, mode);
            return;
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {fullPath}", fullPath);
        }

        _executor.DeleteFile(fullPath, mode);
    }

    public void OpenInExplorer(string path)
    {
        var fullPath = NormalizePath(path);

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException($"Path not found: {fullPath}", fullPath);
        }

        _executor.OpenInExplorer(fullPath);
    }

    public string CopyPath(string path) => NormalizePath(path);

    private static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }
}
