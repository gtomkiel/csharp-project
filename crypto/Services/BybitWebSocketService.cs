using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace crypto.Services;

public class PriceUpdateEventArgs : EventArgs
{
    public string Symbol { get; set; }
    public decimal Price { get; set; }

    public PriceUpdateEventArgs(string symbol, decimal price)
    {
        Symbol = symbol;
        Price = price;
    }
}

public class BybitWebSocketService
{
    private const string BYBIT_WS_URL = "wss://stream.bybit.com/v5/public/spot";
    private ClientWebSocket _webSocket;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isRunning;
    private List<string> _subscribedSymbols = new List<string>();

    public event EventHandler<PriceUpdateEventArgs> OnPriceUpdate;

    public BybitWebSocketService()
    {
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public async Task StartAsync(params string[] symbols)
    {
        if (_isRunning)
            return;

        // Default to BTCUSDT if no symbols provided
        if (symbols == null || symbols.Length == 0)
        {
            symbols = new[] { "BTCUSDT" };
        }

        _isRunning = true;
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
        _subscribedSymbols = new List<string>(symbols);

        try
        {
            await _webSocket.ConnectAsync(new Uri(BYBIT_WS_URL), _cancellationTokenSource.Token);

            // Create topic strings for each symbol
            var topics = symbols.Select(s => $"tickers.{s}").ToArray();

            // Subscribe to all requested tickers
            var subscribeMessage = new
            {
                op = "subscribe",
                args = topics
            };

            var subscribeJson = JsonSerializer.Serialize(subscribeMessage);
            var subscribeBytes = Encoding.UTF8.GetBytes(subscribeJson);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(subscribeBytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);

            Console.WriteLine($"Sent subscription request for {string.Join(", ", topics)}");

            // Start receiving messages
            _ = ReceiveMessagesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket connection error: {ex.Message}");
            _isRunning = false;
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
            return;

        _cancellationTokenSource.Cancel();

        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
        }

        _webSocket.Dispose();
        _isRunning = false;
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];

        try
        {
            while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                var message = new StringBuilder();

                do
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed by the server",
                            CancellationToken.None);
                        break;
                    }
                }
                while (!result.EndOfMessage);

                if (message.Length > 0)
                {
                    ProcessMessage(message.ToString());
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in WebSocket receive: {ex.Message}");
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            using (JsonDocument doc = JsonDocument.Parse(message))
            {
                var root = doc.RootElement;

                // Handle ping/pong for keeping connection alive
                if (root.TryGetProperty("op", out var op) && op.GetString() == "ping")
                {
                    SendPongAsync().ConfigureAwait(false);
                    return;
                }

                // Check if this is ticker data
                if (root.TryGetProperty("topic", out var topic) &&
                    root.TryGetProperty("data", out var dataElement))
                {
                    string topicValue = topic.GetString();
                    if (topicValue != null && topicValue.StartsWith("tickers."))
                    {
                        // Extract symbol from topic (format: "tickers.SYMBOL")
                        string symbol = topicValue.Substring("tickers.".Length);

                        // Process only if we're subscribed to this symbol
                        if (_subscribedSymbols.Contains(symbol))
                        {
                            // Try to get price data - first attempt with indexPrice
                            if (dataElement.TryGetProperty("indexPrice", out var indexPriceElement))
                            {
                                if (decimal.TryParse(indexPriceElement.GetString(), out decimal price))
                                {
                                    Console.WriteLine($"{symbol} Index Price: {price}");
                                    OnPriceUpdate?.Invoke(this, new PriceUpdateEventArgs(symbol, price));
                                }
                            }
                            // If indexPrice is not available, try lastPrice
                            else if (dataElement.TryGetProperty("lastPrice", out var lastPriceElement))
                            {
                                if (decimal.TryParse(lastPriceElement.GetString(), out decimal price))
                                {
                                    Console.WriteLine($"{symbol} Last Price: {price}");
                                    OnPriceUpdate?.Invoke(this, new PriceUpdateEventArgs(symbol, price));
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing message: {ex.Message}");
        }
    }

    private async Task SendPongAsync()
    {
        try
        {
            var pongMessage = new { op = "pong" };
            var pongJson = JsonSerializer.Serialize(pongMessage);
            var pongBytes = Encoding.UTF8.GetBytes(pongJson);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(pongBytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending pong: {ex.Message}");
        }
    }
}
