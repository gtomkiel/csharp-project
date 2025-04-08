using LiveChartsCore;
using LiveChartsCore.Kernel;

namespace crypto;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.MinimumWidth = 1200;
        window.MinimumHeight = 800;

        return window;
    }

    protected override void OnStart()
    {
        LiveCharts.Configure(config =>
            config
                .HasMap<City>((city, index) => new Coordinate(index, city.Population))
        );
    }

    public record City(string Name, double Population);
}
