namespace DiskScape.Core;

public interface IPathActionExecutor
{
    void DeleteFile(string fullPath, DeleteMode mode);

    void DeleteDirectory(string fullPath, DeleteMode mode);

    void OpenInExplorer(string fullPath);
}
