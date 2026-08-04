namespace DiskScape.Core;

public sealed record ScanWarning(ScanWarningKind Kind, string Path, string Message);
