using crypto.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using Microsoft.Maui.Controls.Shapes;

namespace crypto.Views;

[QueryProperty(nameof(Crypto), "Crypto")]
public partial class HistoryPage : ContentPage
{
    private string _crypto = string.Empty;
    private readonly BybitApiService _apiService;
    public ISeries[] Series { get; set; } = Array.Empty<ISeries>();

    public Axis[] XAxes { get; set; } = new[]
    {
        new Axis
        {
            LabelsPaint = new SolidColorPaint(SKColors.White),
            LabelsRotation = 45,
            Labeler = value => new DateTime((long)value).ToString("MM/dd HH:mm"),
            UnitWidth = TimeSpan.FromMinutes(1).Ticks
        }
    };

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
    }

    private async void LoadCryptoData()
    {
        if (string.IsNullOrEmpty(Crypto))
            return;

        CryptoNameLabel.Text = Crypto;

        try
        {
            if (Crypto.Contains("Bitcoin"))
            {
                var response = await _apiService.GetBitcoinDataAsync();

                if (response != null && response.RetCode == 0 && response.Result?.List?.Count > 0)
                {
                    var candlesticks = response.Result.List.Select(item => new FinancialPoint(
                        item.GetStartDateTime(),
                        (double)item.HighPrice,
                        (double)item.OpenPrice,
                        (double)item.ClosePrice,
                        (double)item.LowPrice
                    )).ToArray();

                    Series = new ISeries[]
                    {
                        new CandlesticksSeries<FinancialPoint>
                        {
                            Values = candlesticks,
                            UpStroke = new SolidColorPaint(SKColors.LightGreen) { StrokeThickness = 3 },
                            DownStroke = new SolidColorPaint(SKColors.IndianRed) { StrokeThickness = 3 },
                            UpFill = new SolidColorPaint(SKColors.LightGreen.WithAlpha(90)),
                            DownFill = new SolidColorPaint(SKColors.IndianRed.WithAlpha(90))
                        }
                    };

                    OnPropertyChanged(nameof(Series));
                }
                else
                {
                    ShowErrorMessage("Failed to load Bitcoin data.");
                }
            }
            else
            {
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
}
