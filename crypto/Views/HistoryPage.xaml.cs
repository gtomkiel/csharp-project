using System.Web;
using crypto.Services;

namespace crypto.Views;

[QueryProperty(nameof(Crypto), "Crypto")]
public partial class HistoryPage : ContentPage
{
    private string _crypto;
    private readonly BybitApiService _apiService;

    public string Crypto
    {
        get => _crypto;
        set
        {
            _crypto = value;
            OnPropertyChanged();
            LoadCryptoData();
        }
    }

    public HistoryPage(BybitApiService apiService)
    {
        InitializeComponent();
        _apiService = apiService;
    }

    private async void LoadCryptoData()
    {
        if (string.IsNullOrEmpty(Crypto))
            return;

        CryptoNameLabel.Text = Crypto;
        
        try
        {
            // Clear previous data
            HistoryContainer.Children.Clear();

            if (Crypto.Contains("Bitcoin"))
            {
                var response = await _apiService.GetBitcoinDataAsync();
                
                if (response != null && response.RetCode == 0 && response.Result?.List?.Count > 0)
                {
                    foreach (var item in response.Result.List)
                    {
                        var dateTime = item.GetStartDateTime().ToLocalTime();
                        var frame = new Frame
                        {
                            BackgroundColor = Color.FromArgb("#2e2e2e"),
                            CornerRadius = 8,
                            Padding = new Thickness(15)
                        };

                        var layout = new StackLayout();
                        layout.Children.Add(new Label
                        {
                            Text = $"{dateTime:g}",
                            TextColor = Colors.White,
                            FontAttributes = FontAttributes.Bold
                        });
                        
                        layout.Children.Add(new Label
                        {
                            Text = $"Open: ${item.OpenPrice}",
                            TextColor = Colors.LightGray
                        });
                        
                        layout.Children.Add(new Label
                        {
                            Text = $"Close: ${item.ClosePrice}",
                            TextColor = Colors.LightGray
                        });
                        
                        layout.Children.Add(new Label
                        {
                            Text = $"High: ${item.HighPrice}",
                            TextColor = Colors.LightGreen
                        });
                        
                        layout.Children.Add(new Label
                        {
                            Text = $"Low: ${item.LowPrice}",
                            TextColor = Colors.IndianRed
                        });

                        frame.Content = layout;
                        HistoryContainer.Children.Add(frame);
                    }
                }
                else
                {
                    ShowErrorMessage("Failed to load Bitcoin data.");
                }
            }
            else
            {
                // For other cryptocurrencies we would implement similar code
                // For now, just show a message
                ShowErrorMessage($"History data for {Crypto} is not available yet.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Error loading data: {ex.Message}");
        }
    }

    private void ShowErrorMessage(string message)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2e2e2e"),
            CornerRadius = 8,
            Padding = new Thickness(15)
        };

        frame.Content = new Label
        {
            Text = message,
            TextColor = Colors.IndianRed
        };

        HistoryContainer.Children.Clear();
        HistoryContainer.Children.Add(frame);
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
