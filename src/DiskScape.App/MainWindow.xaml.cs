using System.Reflection;
using System.Windows;
using DiskScape.Core;

namespace DiskScape.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var coreVersion = typeof(Scanner).Assembly.GetName().Version?.ToString() ?? "unknown";
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        CoreVersionText.Text = $"App version: {appVersion}  |  DiskScape.Core: {coreVersion}";
    }
}
