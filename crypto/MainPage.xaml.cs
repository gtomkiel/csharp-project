namespace crypto;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Refresh", "Prices are being refreshed...", "OK");
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        // Get the cryptocurrency name from the button's parent StackLayout
        if (sender is Button button && button.Parent is StackLayout stackLayout)
        {
            // The first Label in the StackLayout contains the cryptocurrency name
            if (stackLayout.Children.FirstOrDefault(c => c is Label) is Label cryptoLabel)
            {
                string cryptoName = cryptoLabel.Text;
                // Navigate to history page with the selected cryptocurrency
                await Shell.Current.GoToAsync($"HistoryPage?Crypto={cryptoName}");
            }
        }
    }
}
