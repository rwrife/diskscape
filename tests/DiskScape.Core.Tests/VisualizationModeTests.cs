using DiskScape.Core;

namespace DiskScape.Core.Tests;

public sealed class VisualizationModeTests
{
    [Fact]
    public void Toggle_SwitchesBetweenTreemapAndSunburst()
    {
        Assert.Equal(VisualizationMode.Sunburst, VisualizationMode.Treemap.Toggle());
        Assert.Equal(VisualizationMode.Treemap, VisualizationMode.Sunburst.Toggle());
    }
}
