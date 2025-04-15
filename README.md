# Crypto Price Tracker

A .NET MAUI application for tracking real-time cryptocurrency prices and historical data.

## Features

- Real-time price updates for multiple cryptocurrencies:
  - Bitcoin (BTC)
  - Ethereum (ETH)
  - Solana (SOL)
  - Binance Coin (BNB)
  - Ripple (XRP)
- Add/remove cryptocurrencies to your tracking dashboard
- Color-coded price updates (green for increases, red for decreases)
- View historical price data with customizable timeframes:
  - Last Day (hourly data)
  - Last Week (6-hour intervals)
  - Last Month (daily data)
  - Last Year (monthly data)
- Interactive candlestick charts for detailed price analysis
- Clean, modern dark-themed UI

## Technologies Used

- .NET 9.0
- .NET MAUI for cross-platform UI
- LiveCharts2 for data visualization
- Bybit API for cryptocurrency data
- WebSockets for real-time price updates
- Asynchronous programming for responsive UI
- Thread pooling for efficient API operations

## Multithreading Implementation

The application employs multiple concurrency techniques to ensure responsive UI while efficiently handling network operations:

### Thread Pool

- Thread pooling that limits concurrent operations based on processor count
- Tasks are tracked by unique string identifiers (e.g., cryptocurrency symbol names)
- SemaphoreSlim for controlling the maximum number of concurrent operations
- Supports task cancellation through CancellationTokenSource

### Thread Synchronization

- Lock-based synchronization to protect shared resources and state
- ConcurrentDictionary for thread-safe collections of running tasks
- Object-level locking (`_operationsLock`, `_stateLock`) for atomic operations on shared state
- SemaphoreSlim for limiting concurrent access to critical resources

### Task Parallel Library (TPL) / Async & Await

- Extensive use of Task-based Asynchronous Pattern (TAP)
- async/await throughout the codebase for non-blocking operations
- Task.Run for CPU-bound operations that shouldn't block the UI thread
- Task.WhenAll for coordinating multiple parallel operations
- ContinueWith for handling task completion and propagating results

### Asynchronous I/O

- Non-blocking network operations for WebSocket communication
- Asynchronous HTTP requests for API data retrieval
- Event-based notification system for real-time price updates
- TaskCompletionSource for bridging between callback-based and task-based asynchronous patterns

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Visual Studio 2022 or Visual Studio Code with MAUI workload
- macOS, Windows, or Linux (macOS required for Mac Catalyst builds)

### Installation

1. Clone the repository:

   ```
   git clone https://github.com/gtomkiel/csharp-project.git
   ```

2. Open the solution in your preferred IDE:

   ```
   cd csharp-project/crypto
   dotnet restore
   ```

3. Build the project:

   ```
   dotnet build
   ```

4. Run the application:

   ```
   dotnet run
   ```

## Project Structure

- **Services/**
  - `BybitApiService.cs` - Handles API requests to fetch historical data
  - `BybitWebSocketService.cs` - Manages real-time WebSocket connections
  - `CryptoThreadPoolService.cs` - Custom thread pooling for optimized API operations

- **Views/**
  - `HistoryPage.xaml(.cs)` - Displays historical price charts

- **MainPage.xaml(.cs)** - Main application interface with real-time prices

## API Integration

This application integrates with the Bybit cryptocurrency exchange API to fetch:

- Real-time price updates via WebSocket
- Historical kline (candlestick) data for various timeframes

## Acknowledgments

- [.NET MAUI](https://github.com/dotnet/maui)
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- [Bybit API](https://bybit-exchange.github.io/docs/v5/intro)
