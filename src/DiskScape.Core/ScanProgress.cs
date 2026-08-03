namespace DiskScape.Core;

public sealed record ScanProgress(long ItemsScanned, long BytesScanned, string CurrentPath);
