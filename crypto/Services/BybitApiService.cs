using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Threading;

namespace crypto.Services;

public class BybitApiService
{
    private readonly HttpClient _httpClient;
    private readonly CryptoThreadPoolService _threadPool;
    private const string baseUrl = "https://api.bybit.nl/v5/market/index-price-kline";
    private readonly object _httpClientLock = new object(); // Lock object for HttpClient operations

    public BybitApiService(CryptoThreadPoolService threadPool = null)
    {
        _httpClient = new HttpClient();
        _threadPool = threadPool ?? new CryptoThreadPoolService();
    }

    /// <summary>
    /// Fetches kline (candlestick) data from Bybit API
    /// </summary>
    /// <param name="symbol">Trading pair symbol (e.g., BTCUSD)</param>
    /// <param name="interval">Time interval in minutes (e.g., 60 for 1 hour) or as a string (e.g., "D" for daily)</param>
    /// <param name="startTime">Start time in Unix timestamp milliseconds</param>
    /// <param name="endTime">End time in Unix timestamp milliseconds</param>
    /// <param name="category">Market category (default: linear)</param>
    /// <returns>KlineResponse object containing the API response data</returns>
    public async Task<KlineResponse> GetKlineDataAsync(
        string symbol,
        object interval,
        long startTime,
        long endTime,
        string category = "linear")
    {
        var url = $"{baseUrl}?category={category}&symbol={symbol}&interval={interval}&start={startTime}&end={endTime}";
        Console.WriteLine(url);

        try
        {
            // Use lock for thread-safe access to HttpClient
            // Note: We're using a hybrid approach here - lock for synchronization but 
            // still allowing the HTTP request itself to run asynchronously
            Task<KlineResponse> requestTask;
            
            lock (_httpClientLock)
            {
                // Initialize the task while holding the lock
                requestTask = _httpClient.GetFromJsonAsync<KlineResponse>(url);
            }
            
            // Wait for the task to complete outside the lock
            return await requestTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching kline data: {ex.Message}");
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"DATA: {ex.Data}");
            return null;
        }
    }

    /// <summary>
    /// Helper method to fetch token data for a specific time range
    /// </summary>
    public async Task<KlineResponse> GetTokenDataAsync(string symbol,
        object interval,
        long startTime,
        long endTime)
    {
        Console.WriteLine($"REQUESTING {symbol} PRICE - Interval: {interval}, Start: {new DateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(startTime).DateTime).ToString()}");
        return await GetKlineDataAsync(symbol, interval, startTime, endTime);
    }

    /// <summary>
    /// Fetches data for multiple tokens in parallel using the thread pool
    /// </summary>
    /// <param name="symbols">List of token symbols to fetch</param>
    /// <param name="interval">Time interval</param>
    /// <param name="startTime">Start time in Unix timestamp milliseconds</param>
    /// <param name="endTime">End time in Unix timestamp milliseconds</param>
    /// <returns>Dictionary mapping symbols to their respective data responses</returns>
    public async Task<Dictionary<string, KlineResponse>> GetMultipleTokensDataAsync(
        IEnumerable<string> symbols,
        object interval,
        long startTime,
        long endTime)
    {
        var results = new Dictionary<string, KlineResponse>();
        var tasks = new List<Task>();

        foreach (var symbol in symbols)
        {
            var task = _threadPool.EnqueueTaskWithResultAsync<KlineResponse>(
                $"fetch_{symbol}_{interval}",
                async (cancellationToken) =>
                {
                    return await GetTokenDataAsync(symbol, interval, startTime, endTime);
                }
            ).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    lock (results)
                    {
                        results[symbol] = t.Result;
                    }
                }
            });

            tasks.Add(task);
        }

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Performs batch processing of data for a symbol over multiple time intervals
    /// </summary>
    /// <param name="symbol">The token symbol</param>
    /// <param name="intervalList">List of intervals to process</param>
    /// <param name="startTime">Start time</param>
    /// <param name="endTime">End time</param>
    /// <returns>Dictionary mapping intervals to their respective data responses</returns>
    public async Task<Dictionary<object, KlineResponse>> ProcessSymbolBatchAsync(
        string symbol,
        IEnumerable<object> intervalList,
        long startTime,
        long endTime)
    {
        var results = new Dictionary<object, KlineResponse>();
        var tasks = new List<Task>();

        foreach (var interval in intervalList)
        {
            var task = _threadPool.EnqueueTaskWithResultAsync<KlineResponse>(
                $"process_{symbol}_{interval}",
                async (cancellationToken) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }
                    return await GetTokenDataAsync(symbol, interval, startTime, endTime);
                }
            ).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    lock (results)
                    {
                        results[interval] = t.Result;
                    }
                }
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Disposes resources used by the service
    /// </summary>
    public void Dispose()
    {
        _httpClient?.Dispose();
        (_threadPool as IDisposable)?.Dispose();
    }
}

/// <summary>
/// Root response object for the Bybit API
/// </summary>
public class KlineResponse
{
    [JsonPropertyName("retCode")] public int RetCode { get; set; }

    [JsonPropertyName("retMsg")] public string RetMsg { get; set; }

    [JsonPropertyName("result")] public KlineResult Result { get; set; }

    [JsonPropertyName("time")] public long Time { get; set; }
}

/// <summary>
/// Result container for kline data
/// </summary>
public class KlineResult
{
    [JsonPropertyName("symbol")] public string Symbol { get; set; }

    [JsonPropertyName("category")] public string Category { get; set; }

    [JsonPropertyName("list")]
    [JsonConverter(typeof(KlineArrayConverter))]
    public List<KlineItem> List { get; set; }
}

/// <summary>
/// Custom JSON converter to handle array format of kline data
/// </summary>
public class KlineArrayConverter : JsonConverter<List<KlineItem>>
{
    public override List<KlineItem> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected start of array");

        var klineItems = new List<KlineItem>();

        // Read the outer array
        reader.Read();
        while (reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected start of inner array");

            var klineItem = new KlineItem();
            reader.Read(); // Move to first value

            // Read values in order: startTime, openPrice, highPrice, lowPrice, closePrice
            if (reader.TokenType == JsonTokenType.String)
                if (long.TryParse(reader.GetString(), out var startTime))
                    klineItem.StartTime = startTime;

            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
                if (decimal.TryParse(reader.GetString(), out var openPrice))
                    klineItem.OpenPrice = openPrice;

            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
                if (decimal.TryParse(reader.GetString(), out var highPrice))
                    klineItem.HighPrice = highPrice;

            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
                if (decimal.TryParse(reader.GetString(), out var lowPrice))
                    klineItem.LowPrice = lowPrice;

            reader.Read();

            if (reader.TokenType == JsonTokenType.String)
                if (decimal.TryParse(reader.GetString(), out var closePrice))
                    klineItem.ClosePrice = closePrice;

            reader.Read(); // Move to end of inner array

            klineItems.Add(klineItem);

            reader.Read(); // Move to next item or end of outer array
        }

        return klineItems;
    }

    public override void Write(Utf8JsonWriter writer, List<KlineItem> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            writer.WriteStartArray();
            writer.WriteStringValue(item.StartTime.ToString());
            writer.WriteStringValue(item.OpenPrice.ToString());
            writer.WriteStringValue(item.HighPrice.ToString());
            writer.WriteStringValue(item.LowPrice.ToString());
            writer.WriteStringValue(item.ClosePrice.ToString());
            writer.WriteEndArray();
        }

        writer.WriteEndArray();
    }
}

/// <summary>
/// Individual kline (candlestick) data item
/// </summary>
public class KlineItem
{
    public long StartTime { get; set; }

    public decimal OpenPrice { get; set; }

    public decimal HighPrice { get; set; }

    public decimal LowPrice { get; set; }

    public decimal ClosePrice { get; set; }

    // Helper method to convert Unix timestamp to DateTime
    public DateTime GetStartDateTime()
    {
        // Convert milliseconds to DateTime
        return DateTimeOffset.FromUnixTimeMilliseconds(StartTime).DateTime;
    }
}
