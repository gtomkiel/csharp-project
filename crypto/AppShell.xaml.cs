namespace crypto;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register routes for navigation
        Routing.RegisterRoute("HistoryPage", typeof(Views.HistoryPage));
    }
}
