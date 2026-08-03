namespace DiskScape.Core;

public enum ScanWarningKind
{
    AccessDenied,
    ReparsePointSkipped,
    PathTooLong,
    IoError,
    NotFound
}
