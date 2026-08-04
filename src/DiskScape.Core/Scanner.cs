using System.Collections.Concurrent;
using System.IO.Enumeration;

namespace DiskScape.Core;

public sealed class Scanner
{
    private readonly ScannerOptions _options;

    public Scanner(ScannerOptions? options = null)
    {
        _options = options ?? new ScannerOptions();
    }

    public async Task<ScanNode> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var fullRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRootPath) && !File.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException($"Path not found: {fullRootPath}");
        }

        var warnings = new ConcurrentBag<ScanWarning>();
        var progressState = new ProgressState(progress);

        if (File.Exists(fullRootPath))
        {
            var fileInfo = new FileInfo(fullRootPath);
            var fileNode = ScanNode.CreateFile(fileInfo.FullName, fileInfo.Length);
            progressState.Report(fileInfo.FullName, fileInfo.Length);
            fileNode.SetWarnings(Array.Empty<ScanWarning>());
            return fileNode;
        }

        var root = ScanNode.CreateDirectory(fullRootPath);
        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxDegreeOfParallelism));

        await ScanDirectoryAsync(root, warnings, progressState, gate, cancellationToken).ConfigureAwait(false);

        Aggregate(root);
        root.SetWarnings(warnings.OrderBy(w => w.Path, StringComparer.OrdinalIgnoreCase).ToArray());
        return root;
    }

    private async Task ScanDirectoryAsync(
        ScanNode directoryNode,
        ConcurrentBag<ScanWarning> warnings,
        ProgressState progress,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var subdirectories = new List<ScanNode>();

        try
        {
            foreach (var entry in EnumerateEntries(directoryNode.Path))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    if (!_options.RecurseReparsePoints && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        warnings.Add(new ScanWarning(
                            ScanWarningKind.ReparsePointSkipped,
                            entry.FullPath,
                            "Reparse point skipped by default to avoid traversal cycles."));
                        progress.Report(entry.FullPath, bytesDelta: 0);
                        continue;
                    }

                    var childDirectory = ScanNode.CreateDirectory(entry.FullPath);
                    directoryNode.AddChild(childDirectory);
                    subdirectories.Add(childDirectory);
                    progress.Report(entry.FullPath, bytesDelta: 0);
                    continue;
                }

                var fileSize = Math.Max(0, entry.Length);
                var fileNode = ScanNode.CreateFile(entry.FullPath, fileSize);
                directoryNode.AddChild(fileNode);
                progress.Report(entry.FullPath, bytesDelta: fileSize);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add(new ScanWarning(ScanWarningKind.AccessDenied, directoryNode.Path, ex.Message));
            return;
        }
        catch (PathTooLongException ex)
        {
            warnings.Add(new ScanWarning(ScanWarningKind.PathTooLong, directoryNode.Path, ex.Message));
            return;
        }
        catch (DirectoryNotFoundException ex)
        {
            warnings.Add(new ScanWarning(ScanWarningKind.NotFound, directoryNode.Path, ex.Message));
            return;
        }
        catch (IOException ex)
        {
            warnings.Add(new ScanWarning(ScanWarningKind.IoError, directoryNode.Path, ex.Message));
            return;
        }

        if (subdirectories.Count == 0)
        {
            return;
        }

        var tasks = new List<Task>(subdirectories.Count);
        foreach (var childDirectory in subdirectories)
        {
            tasks.Add(Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    await ScanDirectoryAsync(childDirectory, warnings, progress, gate, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static IEnumerable<EntryInfo> EnumerateEntries(string directoryPath)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = 0,
            IgnoreInaccessible = false,
            MatchCasing = MatchCasing.PlatformDefault,
            MatchType = MatchType.Simple
        };

        var enumerable = new FileSystemEnumerable<EntryInfo>(
            directoryPath,
            transform: (ref FileSystemEntry entry) => new EntryInfo(
                entry.ToFullPath(),
                entry.IsDirectory,
                entry.Attributes,
                entry.IsDirectory ? 0 : entry.Length),
            options)
        {
            ShouldIncludePredicate = static (ref FileSystemEntry _) => true
        };

        foreach (var entry in enumerable)
        {
            yield return entry;
        }
    }

    private static void Aggregate(ScanNode node)
    {
        if (!node.IsDirectory)
        {
            node.TotalSize = node.OwnSize;
            node.FileCount = 1;
            return;
        }

        long total = node.OwnSize;
        long fileCount = 0;

        foreach (var child in node.Children)
        {
            Aggregate(child);
            total += child.TotalSize;
            fileCount += child.FileCount;
        }

        node.TotalSize = total;
        node.FileCount = fileCount;
    }

    private readonly record struct EntryInfo(string FullPath, bool IsDirectory, FileAttributes Attributes, long Length);

    private sealed class ProgressState
    {
        private readonly IProgress<ScanProgress>? _progress;
        private long _itemsScanned;
        private long _bytesScanned;

        public ProgressState(IProgress<ScanProgress>? progress)
        {
            _progress = progress;
        }

        public void Report(string currentPath, long bytesDelta)
        {
            var items = Interlocked.Increment(ref _itemsScanned);
            var bytes = Interlocked.Add(ref _bytesScanned, bytesDelta);
            _progress?.Report(new ScanProgress(items, bytes, currentPath));
        }
    }
}
