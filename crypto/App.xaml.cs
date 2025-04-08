using LiveChartsCore;

namespace crypto;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        LiveCharts.Configure(config => 
            config 
                .HasMap<City>((city, index) => new(index, city.Population)) 
        ); 
    }
    
    public record City(string Name, double Population);
}