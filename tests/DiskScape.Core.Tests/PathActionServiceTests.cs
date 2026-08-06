using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class PathActionServiceTests
{
    [Fact]
    public void Delete_DefaultsToRecycleBin_WhenModeNotProvided()
    {
        var root = CreateTempRoot();
        var filePath = Path.Combine(root, "sample.txt");
        File.WriteAllText(filePath, "hello");

        var executor = new RecordingExecutor();
        var service = new PathActionService(executor);

        service.Delete(filePath, isDirectory: false);

        var action = Assert.Single(executor.Actions);
        Assert.Equal("delete-file", action.Kind);
        Assert.Equal(DeleteMode.RecycleBin, action.Mode);
        Assert.Equal(Path.GetFullPath(filePath), action.Path);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Delete_UsesPermanentMode_WhenRequested()
    {
        var root = CreateTempRoot();
        var dirPath = Path.Combine(root, "to-remove");
        Directory.CreateDirectory(dirPath);

        var executor = new RecordingExecutor();
        var service = new PathActionService(executor);

        service.Delete(dirPath, isDirectory: true, DeleteMode.Permanent);

        var action = Assert.Single(executor.Actions);
        Assert.Equal("delete-directory", action.Kind);
        Assert.Equal(DeleteMode.Permanent, action.Mode);
        Assert.Equal(Path.GetFullPath(dirPath), action.Path);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Delete_ThrowsForMissingFile_AndDoesNotCallExecutor()
    {
        var missingFile = Path.Combine(Path.GetTempPath(), "diskscape-tests", Guid.NewGuid().ToString("N"), "missing.bin");
        var executor = new RecordingExecutor();
        var service = new PathActionService(executor);

        Assert.Throws<FileNotFoundException>(() => service.Delete(missingFile, isDirectory: false));
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public void Delete_ThrowsForMissingDirectory_AndDoesNotCallExecutor()
    {
        var missingDirectory = Path.Combine(Path.GetTempPath(), "diskscape-tests", Guid.NewGuid().ToString("N"), "missing-dir");
        var executor = new RecordingExecutor();
        var service = new PathActionService(executor);

        Assert.Throws<DirectoryNotFoundException>(() => service.Delete(missingDirectory, isDirectory: true));
        Assert.Empty(executor.Actions);
    }

    [Fact]
    public void OpenInExplorer_NormalizesPath_AndDelegates()
    {
        var root = CreateTempRoot();
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        var executor = new RecordingExecutor();
        var service = new PathActionService(executor);

        service.OpenInExplorer(Path.Combine(nested, "."));

        var action = Assert.Single(executor.Actions);
        Assert.Equal("open-explorer", action.Kind);
        Assert.Equal(DeleteMode.RecycleBin, action.Mode);
        Assert.Equal(Path.GetFullPath(nested), action.Path);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void CopyPath_ReturnsNormalizedAbsolutePath()
    {
        var root = CreateTempRoot();
        var nested = Path.Combine(root, "nested");
        Directory.CreateDirectory(nested);

        var service = new PathActionService(new RecordingExecutor());
        var copied = service.CopyPath(Path.Combine(nested, "."));

        Assert.Equal(Path.GetFullPath(nested), copied);

        Directory.Delete(root, recursive: true);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "diskscape-action-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class RecordingExecutor : IPathActionExecutor
    {
        public List<RecordedAction> Actions { get; } = new();

        public void DeleteFile(string fullPath, DeleteMode mode) =>
            Actions.Add(new RecordedAction("delete-file", fullPath, mode));

        public void DeleteDirectory(string fullPath, DeleteMode mode) =>
            Actions.Add(new RecordedAction("delete-directory", fullPath, mode));

        public void OpenInExplorer(string fullPath) =>
            Actions.Add(new RecordedAction("open-explorer", fullPath, DeleteMode.RecycleBin));
    }

    private sealed record RecordedAction(string Kind, string Path, DeleteMode Mode);
}
