using System.Diagnostics;
using Microsoft.VisualBasic.FileIO;

namespace DiskScape.Core;

public sealed class WindowsPathActionExecutor : IPathActionExecutor
{
    public void DeleteFile(string fullPath, DeleteMode mode)
    {
        EnsureWindows();

        if (mode == DeleteMode.RecycleBin)
        {
            FileSystem.DeleteFile(
                fullPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
            return;
        }

        File.Delete(fullPath);
    }

    public void DeleteDirectory(string fullPath, DeleteMode mode)
    {
        EnsureWindows();

        if (mode == DeleteMode.RecycleBin)
        {
            FileSystem.DeleteDirectory(
                fullPath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin,
                UICancelOption.ThrowException);
            return;
        }

        Directory.Delete(fullPath, recursive: true);
    }

    public void OpenInExplorer(string fullPath)
    {
        EnsureWindows();

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{fullPath}\"",
            UseShellExecute = true
        });
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("WindowsPathActionExecutor is only supported on Windows.");
        }
    }
}
