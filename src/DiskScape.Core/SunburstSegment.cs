namespace DiskScape.Core;

/// <summary>
/// Represents one sunburst arc segment in polar coordinates.
/// </summary>
public readonly record struct SunburstSegment(
    ScanNode Node,
    int Depth,
    double StartAngle,
    double SweepAngle,
    double InnerRadius,
    double OuterRadius)
{
    public double EndAngle => StartAngle + SweepAngle;

    public double Thickness => OuterRadius - InnerRadius;
}
