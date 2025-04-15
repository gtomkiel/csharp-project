using crypto.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.Generic;

namespace crypto.Views;

[QueryProperty(nameof(Crypto), "Crypto")]
public partial class HistoryPage : ContentPage
{
    // Enum to represent timeframe options
    private enum TimeframeOption
    {
        LastDay,
        LastWeek,
        LastMonth,
        LastYear
    }

    private string _crypto = string.Empty;
    private readonly BybitApiService _apiService;
    private TimeframeOption _selectedTimeframe = TimeframeOption.LastDay; // Default to last day

    // Dictionary mapping crypto display names to symbols
    private static readonly Dictionary<string, string> CryptoSymbols = new Dictionary<string, string>
    {
        { "Bitcoin (BTC)", "BTCUSDT" },
        { "Ethereum (ETH)", "ETHUSDT" },
        { "Solana (SOL)", "SOLUSDT" },
        { "Binance Coin (BNB)", "BNBUSDT" },
        { "Ripple (XRP)", "XRPUSDT" }
    };

    // Properties for the Picker binding
    public List<string> TimeframeOptions { get; } = new() { "Last Day", "Last Week", "Last Month", "Last Year" };

    public int SelectedTimeframeIndex
    {
        get => (int)_selectedTimeframe;
        set
        {
            if ((int)_selectedTimeframe != value)
            {
                _selectedTimeframe = (TimeframeOption)value;
                OnPropertyChanged();
            }
        }
    }

    public ISeries[] Series { get; set; } = Array.Empty<ISeries>();
    public Axis[] XAxes { get; set; }

    public Axis[] YAxes { get; set; } = new[]
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(SKColors.White),
            Position = LiveChartsCore.Measure.AxisPosition.End
        }
    };

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
        BindingContext = this;

        // Initialize with default timeframe
        _selectedTimeframe = TimeframeOption.LastDay;
        UpdateXAxes(_selectedTimeframe);
    }

    private async void LoadCryptoData()
    {
        if (string.IsNullOrEmpty(Crypto))
            return;

        CryptoNameLabel.Text = Crypto;

        var symbol = GetSymbolFromCryptoName(Crypto);
        if (string.IsNullOrEmpty(symbol))
        {
            ShowErrorMessage($"Unknown cryptocurrency: {Crypto}");
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var nowMillis = now.ToUnixTimeMilliseconds();
            long startTimeMillis;
            object interval;

            // Set interval and start time based on selected timeframe
            switch (_selectedTimeframe)
            {
                case TimeframeOption.LastDay:
                    interval = 60; // 60 minutes
                    startTimeMillis = now.AddDays(-1).ToUnixTimeMilliseconds();
                    break;
                case TimeframeOption.LastWeek:
                    interval = 360; // 6 hours (360 minutes)
                    startTimeMillis = now.AddDays(-7).ToUnixTimeMilliseconds();
                    break;
                case TimeframeOption.LastMonth:
                    interval = "D"; // Daily
                    startTimeMillis = now.AddMonths(-1).ToUnixTimeMilliseconds();
                    break;
                case TimeframeOption.LastYear:
                    interval = "M"; // Monthly
                    startTimeMillis = now.AddYears(-1).ToUnixTimeMilliseconds();
                    break;
                default:
                    interval = 60;
                    startTimeMillis = now.AddDays(-1).ToUnixTimeMilliseconds();
                    break;
            }

            var response = await _apiService.GetTokenDataAsync(symbol, interval, startTimeMillis, nowMillis);

            if (response != null && response.RetCode == 0 && response.Result?.List?.Count > 0)
            {
                var candlesticks = response.Result.List.Select(item =>
                {
                    // Convert UTC time to local time zone
                    var utcDateTime = item.GetStartDateTime();
                    var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, TimeZoneInfo.Local);

                    return new FinancialPoint(
                        localDateTime,
                        (double)item.HighPrice,
                        (double)item.OpenPrice,
                        (double)item.ClosePrice,
                        (double)item.LowPrice
                    );
                }).ToArray();

                Series = new ISeries[]
                {
                    new CandlesticksSeries<FinancialPoint>
                    {
                        Values = candlesticks,
                        UpStroke = new SolidColorPaint(SKColors.LightGreen) { StrokeThickness = 5 },
                        DownStroke = new SolidColorPaint(SKColors.IndianRed) { StrokeThickness = 5 },
                        UpFill = new SolidColorPaint(SKColors.LightGreen.WithAlpha(90)),
                        DownFill = new SolidColorPaint(SKColors.IndianRed.WithAlpha(90)),
                        MaxBarWidth = 20 // Increase the maximum width of the candlesticks
                    }
                };

                OnPropertyChanged(nameof(Series));
            }
            else
            {
                ShowErrorMessage($"Failed to load {Crypto} data.");
            }
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Error loading data: {ex.Message}");
        }
    }

    private void UpdateXAxes(TimeframeOption timeframeOption)
    {
        string dateFormat;
        TimeSpan unitWidth;

        // Set appropriate date format and unit width based on timeframe
        switch (timeframeOption)
        {
            case TimeframeOption.LastDay:
                dateFormat = "HH:mm";
                unitWidth = TimeSpan.FromMinutes(60);
                break;
            case TimeframeOption.LastWeek:
                dateFormat = "dd/MM HH:mm";
                unitWidth = TimeSpan.FromMinutes(360);
                break;
            case TimeframeOption.LastMonth:
                dateFormat = "dd/MM";
                unitWidth = TimeSpan.FromDays(1);
                break;
            case TimeframeOption.LastYear:
                dateFormat = "yyyy/MM";
                unitWidth = TimeSpan.FromDays(30);
                break;
            default:
                dateFormat = "dd/MM HH:mm";
                unitWidth = TimeSpan.FromMinutes(60);
                break;
        }

        XAxes = new[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(SKColors.White),
                LabelsRotation = 45,
                Labeler = value => new DateTime((long)value).ToString(dateFormat),
                UnitWidth = unitWidth.Ticks
            }
        };

        OnPropertyChanged(nameof(XAxes));
    }

    private void OnTimeframeSelected(object sender, EventArgs e)
    {
        if (TimeframePicker.SelectedIndex < 0)
            return;

        _selectedTimeframe = (TimeframeOption)TimeframePicker.SelectedIndex;
        UpdateXAxes(_selectedTimeframe);
        LoadCryptoData();
    }

    private void ShowErrorMessage(string message)
    {
        var border = new Border
        {
            BackgroundColor = Color.FromArgb("#2e2e2e"),
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(8)
            },
            Padding = new Thickness(15)
        };

        border.Content = new Label
        {
            Text = message,
            TextColor = Colors.IndianRed
        };

        // Update the chart area to show the error instead
        Series = Array.Empty<ISeries>();
        OnPropertyChanged(nameof(Series));
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private string GetSymbolFromCryptoName(string cryptoName)
    {
        // First, try an exact match in our dictionary
        if (CryptoSymbols.TryGetValue(cryptoName, out string symbol))
        {
            return symbol;
        }
        
        // If not found, try a partial match (for backward compatibility)
        foreach (var kvp in CryptoSymbols)
        {
            if (cryptoName.Contains(kvp.Key) || kvp.Key.Contains(cryptoName))
            {
                return kvp.Value;
            }
        }
        
        // If still not found, try to extract the symbol from parentheses
        // Example: "Bitcoin (BTC)" -> extract "BTC" and append "USDT"
        var match = System.Text.RegularExpressions.Regex.Match(cryptoName, @"\(([^)]*)\)");
        if (match.Success && match.Groups.Count > 1)
        {
            string extractedSymbol = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(extractedSymbol))
            {
                return $"{extractedSymbol}USDT";
            }
        }
        
        return string.Empty;
    }
}
