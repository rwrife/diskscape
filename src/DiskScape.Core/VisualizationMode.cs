namespace DiskScape.Core;

public enum VisualizationMode
{
    Treemap,
    Sunburst
}

public static class VisualizationModeExtensions
{
    public static VisualizationMode Toggle(this VisualizationMode mode) =>
        mode == VisualizationMode.Treemap
            ? VisualizationMode.Sunburst
            : VisualizationMode.Treemap;
}
