using crypto.Services;
using System.Collections.ObjectModel;

namespace crypto;

public partial class MainPage : ContentPage
{
    private readonly BybitWebSocketService _bybitWebSocketService;
    // Dictionary to store cryptocurrency price labels
    private readonly Dictionary<string, Label> _priceLabels = new Dictionary<string, Label>();
    // Track the currently active cryptocurrencies
    private readonly List<CryptoInfo> _activeCryptos = new List<CryptoInfo>();
    
    // Keep only the 5 most popular cryptocurrencies
    private readonly List<CryptoInfo> _availableCryptos = new List<CryptoInfo>
    {
        new CryptoInfo { Symbol = "BTCUSDT", DisplayName = "Bitcoin (BTC)" },
        new CryptoInfo { Symbol = "ETHUSDT", DisplayName = "Ethereum (ETH)" },
        new CryptoInfo { Symbol = "SOLUSDT", DisplayName = "Solana (SOL)" },
        new CryptoInfo { Symbol = "BNBUSDT", DisplayName = "Binance Coin (BNB)" },
        new CryptoInfo { Symbol = "XRPUSDT", DisplayName = "Ripple (XRP)" }
    };

    public MainPage()
    {
        InitializeComponent();
        _bybitWebSocketService = new BybitWebSocketService();
        _bybitWebSocketService.OnPriceUpdate += OnCryptoPriceUpdate;

        // Initialize the UI and data structures
        InitializeCryptoUI();

        // Start the WebSocket connection when the page appears
        Appearing += async (s, e) => await StartWebSocketConnection();
        Disappearing += async (s, e) => await _bybitWebSocketService.StopAsync();
    }

    private void InitializeCryptoUI()
    {
        // Add default cryptocurrencies
        _activeCryptos.Add(_availableCryptos.First(c => c.Symbol == "BTCUSDT"));
        _activeCryptos.Add(_availableCryptos.First(c => c.Symbol == "ETHUSDT"));
        
        // Clear the existing content and recreate it
        var container = CryptoCardsContainer;
        
        // Remove all children except the Add Crypto button (which is the last child)
        if (container.Children.Count > 0)
        {
            var addButton = container.Children.LastOrDefault();
            container.Children.Clear();
            if (addButton != null)
            {
                container.Children.Add(addButton);
            }
        }
        
        // Add crypto cards for active cryptocurrencies
        foreach (var crypto in _activeCryptos)
        {
            AddCryptoCard(crypto);
        }
    }

    private void FindCryptoPriceLabels()
    {
        // Clear existing price labels
        _priceLabels.Clear();

        // Search through the visual tree for all crypto frames
        foreach (var frame in FindVisualChildren<Frame>(this))
        {
            if (frame.Content is Grid grid)
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

                        // Find the matching crypto info
                        var cryptoInfo = _availableCryptos.FirstOrDefault(c => c.DisplayName == cryptoLabel.Text);
                        if (cryptoInfo != null && priceLabel != null)
                        {
                            _priceLabels[cryptoInfo.Symbol] = priceLabel;
                        }
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
        // Subscribe to all active cryptocurrencies
        await _bybitWebSocketService.StartAsync(_activeCryptos.Select(c => c.Symbol).ToArray());
    }

    private void OnCryptoPriceUpdate(object sender, PriceUpdateEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Update the correct price label based on the symbol using the dictionary
            if (_priceLabels.TryGetValue(e.Symbol, out var priceLabel))
            {
                priceLabel.Text = $"${e.Price:N2}";
                // Update text color based on price change (green for up, red for down)
                if (decimal.TryParse(priceLabel.Text.Replace("$", ""), out decimal currentPrice))
                {
                    // Store the original price before updating
                    if (priceLabel.BindingContext == null)
                    {
                        priceLabel.BindingContext = currentPrice.ToString();
                    }
                    else if (decimal.TryParse(priceLabel.BindingContext.ToString(), out decimal previousPrice))
                    {
                        priceLabel.TextColor = currentPrice > previousPrice 
                            ? Color.Parse("#4CAF50")  // Green
                            : currentPrice < previousPrice 
                                ? Color.Parse("#E74C3C")  // Red
                                : priceLabel.TextColor;

                        priceLabel.BindingContext = currentPrice.ToString();
                    }
                }
            }
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
    
    private async void OnAddCryptoClicked(object sender, EventArgs e)
    {
        var availableCryptos = _availableCryptos
            .Where(c => !_activeCryptos.Any(ac => ac.Symbol == c.Symbol))
            .ToList();
            
        if (availableCryptos.Count == 0)
        {
            await DisplayAlert("No More Cryptocurrencies", 
                "All available cryptocurrencies are already displayed.", "OK");
            return;
        }

        // Show selection dialog
        string result = await DisplayActionSheet("Select Cryptocurrency", 
            "Cancel", null, availableCryptos.Select(c => c.DisplayName).ToArray());
            
        if (result != "Cancel" && !string.IsNullOrEmpty(result))
        {
            var selectedCrypto = _availableCryptos.FirstOrDefault(c => c.DisplayName == result);
            if (selectedCrypto != null)
            {
                // Add to active cryptos
                _activeCryptos.Add(selectedCrypto);
                
                // Add crypto card to UI
                AddCryptoCard(selectedCrypto);
                
                // Subscribe to the new cryptocurrency
                await _bybitWebSocketService.StartAsync(selectedCrypto.Symbol);
            }
        }
    }
    
    private void AddCryptoCard(CryptoInfo cryptoInfo)
    {
        // Create price label
        var priceLabel = new Label
        {
            Text = "$0.00",
            TextColor = Color.Parse("#4CAF50"),
            FontSize = 20
        };
        
        // Create the card
        var frame = new Frame
        {
            BackgroundColor = Color.Parse("#2e2e2e"),
            CornerRadius = 10,
            Margin = new Thickness(0, 5),
            Padding = new Thickness(10, 5)
        };
        
        // Create layout for the card
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        
        // Create and add crypto name label and price label
        var stackLayout = new StackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center
        };
        
        stackLayout.Add(new Label
        {
            Text = cryptoInfo.DisplayName,
            TextColor = Colors.White,
            FontSize = 20
        });
        
        stackLayout.Add(priceLabel);
        
        // Create view history button
        var historyButton = new Button
        {
            Text = "View History",
            Style = (Style)Resources["HistoryButton"],
            VerticalOptions = LayoutOptions.Center
        };
        historyButton.Clicked += OnViewHistoryClicked;
        
        // Add everything to the grid
        grid.Add(stackLayout, 0, 0);
        grid.Add(historyButton, 1, 0);
        
        // Add the grid to the frame
        frame.Content = grid;
        
        // Find the container and add the new card before the "Add Crypto" button
        var container = CryptoCardsContainer;
        int addButtonIndex = container.Children.Count - 1; // Assuming add button is the last child
        container.Children.Insert(addButtonIndex, frame);
        
        // Register the price label
        _priceLabels[cryptoInfo.Symbol] = priceLabel;
    }
    
    // Model for crypto information
    public class CryptoInfo
    {
        public string Symbol { get; set; }
        public string DisplayName { get; set; }
    }
}
