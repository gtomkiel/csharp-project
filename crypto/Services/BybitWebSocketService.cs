using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;

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
    private readonly SemaphoreSlim _webSocketSemaphore = new SemaphoreSlim(1, 1);
    private readonly CryptoThreadPoolService _threadPool;
    private readonly object _stateLock = new object(); // Lock object for thread-safe state management

    public event EventHandler<PriceUpdateEventArgs> OnPriceUpdate;

    public BybitWebSocketService(CryptoThreadPoolService threadPool = null)
    {
        _webSocket = new ClientWebSocket();
        _cancellationTokenSource = new CancellationTokenSource();
        _threadPool = threadPool ?? new CryptoThreadPoolService();
    }

    public async Task StartAsync(params string[] symbols)
    {
        bool shouldConnect = false;
        string[] symbolsToSubscribe;
        
        await _webSocketSemaphore.WaitAsync();
        try
        {
            lock (_stateLock)
            {
                // Default to BTCUSDT if no symbols provided
                if (symbols == null || symbols.Length == 0)
                {
                    symbols = new[] { "BTCUSDT" };
                }
                
                // If already running, check if we need to add new symbols
                if (_isRunning)
                {
                    // Find symbols that aren't already subscribed
                    var newSymbols = symbols.Where(s => !_subscribedSymbols.Contains(s)).ToArray();
                    if (newSymbols.Length == 0)
                    {
                        // Already subscribed to all requested symbols
                        return;
                    }
                    
                    // Add new symbols to our subscription list
                    _subscribedSymbols.AddRange(newSymbols);
                    symbolsToSubscribe = newSymbols;
                    
                    // Only subscribe to new symbols, no need to reconnect
                    shouldConnect = false;
                }
                else
                {
                    // Not running yet, initialize everything
                    _isRunning = true;
                    _webSocket = new ClientWebSocket();
                    _cancellationTokenSource = new CancellationTokenSource();
                    _subscribedSymbols = new List<string>(symbols);
                    symbolsToSubscribe = symbols;
                    shouldConnect = true;
                }
            }
            
            if (shouldConnect)
            {
                try
                {
                    await _webSocket.ConnectAsync(new Uri(BYBIT_WS_URL), _cancellationTokenSource.Token);
                    await SubscribeToSymbols(symbolsToSubscribe);
                    
                    // Start receiving messages once
                    _ = ReceiveMessagesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WebSocket connection error: {ex.Message}");
                    lock (_stateLock)
                    {
                        _isRunning = false;
                    }
                }
            }
            else if (_webSocket.State == WebSocketState.Open)
            {
                // Connection already open, just subscribe to new symbols
                await SubscribeToSymbols(symbolsToSubscribe);
            }
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }

    private async Task SubscribeToSymbols(string[] symbols)
    {
        if (symbols == null || symbols.Length == 0)
            return;
            
        // Create topic strings for each symbol
        var topics = symbols.Select(s => $"tickers.{s}").ToArray();

        // Subscribe to requested tickers
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
    }

    public async Task UnsubscribeFromSymbols(string[] symbols)
    {
        if (symbols == null || symbols.Length == 0)
            return;
            
        await _webSocketSemaphore.WaitAsync();
        try
        {
            if (!_isRunning || _webSocket.State != WebSocketState.Open)
                return;
                
            // Create topic strings for each symbol
            var topics = symbols.Select(s => $"tickers.{s}").ToArray();

            // Unsubscribe from tickers
            var unsubscribeMessage = new
            {
                op = "unsubscribe",
                args = topics
            };

            var unsubscribeJson = JsonSerializer.Serialize(unsubscribeMessage);
            var unsubscribeBytes = Encoding.UTF8.GetBytes(unsubscribeJson);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(unsubscribeBytes),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);
                
            Console.WriteLine($"Sent unsubscription request for {string.Join(", ", topics)}");
            
            // Update subscribed symbols list
            lock (_stateLock)
            {
                _subscribedSymbols = _subscribedSymbols
                    .Where(s => !symbols.Contains(s))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error unsubscribing from symbols: {ex.Message}");
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }

    public async Task StopAsync()
    {
        ClientWebSocket socketToClose = null;
        CancellationTokenSource tokenSourceToCancel = null;
        
        lock (_stateLock)
        {
            if (!_isRunning)
                return;
                
            socketToClose = _webSocket;
            tokenSourceToCancel = _cancellationTokenSource;
            _isRunning = false;
            _subscribedSymbols.Clear();
        }

        tokenSourceToCancel?.Cancel();

        if (socketToClose != null && socketToClose.State == WebSocketState.Open)
        {
            try
            {
                await socketToClose.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, 
                    "Closing", 
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing WebSocket: {ex.Message}");
            }
            
            socketToClose.Dispose();
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[4096];

        try
        {
            ClientWebSocket socket;
            CancellationToken token;
            
            lock (_stateLock)
            {
                socket = _webSocket;
                token = _cancellationTokenSource.Token;
            }
            
            while (socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                var message = new StringBuilder();

                do
                {
                    result = await socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer), token);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Connection closed by the server",
                            CancellationToken.None);
                        break;
                    }
                }
                while (!result.EndOfMessage);

                if (message.Length > 0)
                {
                    // Use thread pool to process messages asynchronously
                    var messageContent = message.ToString();
                    await _threadPool.EnqueueTaskAsync(
                        $"process_ws_message_{DateTime.UtcNow.Ticks}",
                        (token) => Task.Run(() => ProcessMessage(messageContent), token)
                    );
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
            Console.WriteLine("WebSocket receive operation was canceled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in WebSocket receive: {ex.Message}");
        }
        finally
        {
            lock (_stateLock)
            {
                _isRunning = false;
            }
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
                        bool isSubscribed = false;
                        
                        // Thread-safe check for subscription
                        lock (_stateLock)
                        {
                            isSubscribed = _subscribedSymbols.Contains(symbol);
                        }

                        // Process only if we're subscribed to this symbol
                        if (isSubscribed)
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
        var pongMessage = new { op = "pong" };
        var pongJson = JsonSerializer.Serialize(pongMessage);
        var pongBytes = Encoding.UTF8.GetBytes(pongJson);

        await _webSocketSemaphore.WaitAsync();
        try
        {
            if (_webSocket.State == WebSocketState.Open)
            {
                await _webSocket.SendAsync(
                    new ArraySegment<byte>(pongBytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
                Console.WriteLine("Pong sent to keep connection alive");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending pong: {ex.Message}");
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }
}
