namespace DiskScape.Core;

public sealed class ScannerOptions
{
    public int MaxDegreeOfParallelism { get; init; } = Math.Max(1, Environment.ProcessorCount);
    public bool RecurseReparsePoints { get; init; }
}
