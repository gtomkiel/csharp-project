using crypto.Services;

namespace crypto;

public partial class MainPage : ContentPage
{
    private readonly BybitWebSocketService _bybitWebSocketService;
    private Label _bitcoinPriceLabel;
    private Label _ethereumPriceLabel;

    public MainPage()
    {
        InitializeComponent();
        _bybitWebSocketService = new BybitWebSocketService();
        _bybitWebSocketService.OnPriceUpdate += OnCryptoPriceUpdate;

        // Find the Bitcoin and Ethereum price labels in the UI
        FindCryptoPriceLabels();

        // Start the WebSocket connection when the page appears
        Appearing += async (s, e) => await StartWebSocketConnection();
        Disappearing += async (s, e) => await _bybitWebSocketService.StopAsync();
    }

    private void FindCryptoPriceLabels()
    {
        // Search through the visual tree for both Bitcoin and Ethereum frames
        foreach (var frame in FindVisualChildren<Frame>(this))
        {
            var grid = frame.Content as Grid;
            if (grid != null)
            {
                var stackLayout = grid.Children.FirstOrDefault(c => c is StackLayout) as StackLayout;
                if (stackLayout != null)
                {
                    var cryptoLabel = stackLayout.Children.FirstOrDefault(c => c is Label) as Label;
                    if (cryptoLabel != null && cryptoLabel.Text != null)
                    {
                        // Get the second Label which should be the price
                        var priceLabel = stackLayout.Children
                            .Where(c => c is Label)
                            .Cast<Label>()
                            .Skip(1)
                            .FirstOrDefault();

                        // Assign to the appropriate reference based on the crypto name
                        if (cryptoLabel.Text.Contains("Bitcoin"))
                            _bitcoinPriceLabel = priceLabel;
                        else if (cryptoLabel.Text.Contains("Ethereum"))
                            _ethereumPriceLabel = priceLabel;
                    }
                }
            }
        }
    }

    public static IEnumerable<T> FindVisualChildren<T>(Element element) where T : Element
    {
        if (element == null)
            yield break;

        if (element is Layout layout)
        {
            foreach (Element child in layout.Children)
            {
                if (child is T t)
                    yield return t;

                foreach (var childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }
        else if (element is ContentView contentView && contentView.Content != null)
        {
            if (contentView.Content is T t)
                yield return t;

            foreach (var childOfChild in FindVisualChildren<T>(contentView.Content))
                yield return childOfChild;
        }
        else if (element is ContentPage contentPage && contentPage.Content != null)
        {
            if (contentPage.Content is T t)
                yield return t;

            foreach (var childOfChild in FindVisualChildren<T>(contentPage.Content))
                yield return childOfChild;
        }
    }

    private async Task StartWebSocketConnection()
    {
        // Subscribe to both Bitcoin and Ethereum
        await _bybitWebSocketService.StartAsync("BTCUSDT", "ETHUSDT");
    }

    private void OnCryptoPriceUpdate(object sender, PriceUpdateEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Update the correct price label based on the symbol using the stored label references
            if (e.Symbol == "BTCUSDT" && _bitcoinPriceLabel != null)
                _bitcoinPriceLabel.Text = $"${e.Price:N2}";
            else if (e.Symbol == "ETHUSDT" && _ethereumPriceLabel != null)
                _ethereumPriceLabel.Text = $"${e.Price:N2}";
        });
    }

    private async void OnViewHistoryClicked(object sender, EventArgs e)
    {
        // Get the cryptocurrency name from the button's parent Grid
        if (sender is Button button && button.Parent is Grid grid)
        {
            // Find the StackLayout in the same Grid
            var stackLayout = grid.Children.FirstOrDefault(c => c is StackLayout) as StackLayout;
            if (stackLayout != null)
            {
                // The first Label in the StackLayout contains the cryptocurrency name
                var cryptoLabel = stackLayout.Children.FirstOrDefault(c => c is Label) as Label;
                if (cryptoLabel != null)
                {
                    var cryptoName = cryptoLabel.Text;
                    // Navigate to history page with the selected cryptocurrency
                    await Shell.Current.GoToAsync($"HistoryPage?Crypto={cryptoName}");
                }
            }
        }
    }
}
