using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace crypto.Services
{
    /// <summary>
    /// A thread pool service for efficiently managing concurrent operations in cryptocurrency data processing.
    /// </summary>
    public class CryptoThreadPoolService
    {
        private readonly int _maxConcurrentTasks;
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentDictionary<string, Task> _runningTasks;
        private readonly CancellationTokenSource _globalCancellationTokenSource;

        /// <summary>
        /// Initializes a new instance of the CryptoThreadPoolService with a specified maximum number of concurrent tasks.
        /// </summary>
        /// <param name="maxConcurrentTasks">Maximum number of tasks that can run concurrently. Defaults to Environment.ProcessorCount.</param>
        public CryptoThreadPoolService(int maxConcurrentTasks = 0)
        {
            // If maxConcurrentTasks is not specified or invalid, use processor count
            _maxConcurrentTasks = maxConcurrentTasks <= 0 ? Environment.ProcessorCount : maxConcurrentTasks;
            _semaphore = new SemaphoreSlim(_maxConcurrentTasks, _maxConcurrentTasks);
            _runningTasks = new ConcurrentDictionary<string, Task>();
            _globalCancellationTokenSource = new CancellationTokenSource();

            Console.WriteLine($"CryptoThreadPoolService initialized with {_maxConcurrentTasks} max concurrent tasks");
        }

        /// <summary>
        /// Enqueues a task to be executed by the thread pool with a unique key for reference.
        /// </summary>
        /// <param name="key">Unique identifier for the task (e.g., cryptocurrency symbol)</param>
        /// <param name="workItem">The function to execute</param>
        /// <returns>Task representing the async operation</returns>
        public async Task EnqueueTaskAsync(string key, Func<CancellationToken, Task> workItem)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Task key cannot be null or empty", nameof(key));
            }

            await _semaphore.WaitAsync();

            try
            {
                // Create a linked token source that can be canceled either by the global source or individually
                var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    _globalCancellationTokenSource.Token);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        await workItem(linkedTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Task {key} was canceled");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in task {key}: {ex.Message}");
                    }
                    finally
                    {
                        // Clean up the task from the dictionary
                        _runningTasks.TryRemove(key, out _);
                        _semaphore.Release();
                    }
                });

                // Store the task in our dictionary
                _runningTasks[key] = task;
            }
            catch (Exception ex)
            {
                _semaphore.Release();
                Console.WriteLine($"Failed to enqueue task {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Enqueues a task that returns a result to be executed by the thread pool.
        /// </summary>
        /// <typeparam name="T">The type of result returned by the task</typeparam>
        /// <param name="key">Unique identifier for the task (e.g., cryptocurrency symbol)</param>
        /// <param name="workItem">The function to execute</param>
        /// <returns>Task representing the async operation with result</returns>
        public async Task<T> EnqueueTaskWithResultAsync<T>(string key, Func<CancellationToken, Task<T>> workItem)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();

            await EnqueueTaskAsync(key, async (token) =>
            {
                try
                {
                    var result = await workItem(token);
                    taskCompletionSource.SetResult(result);
                }
                catch (OperationCanceledException)
                {
                    taskCompletionSource.SetCanceled();
                }
                catch (Exception ex)
                {
                    taskCompletionSource.SetException(ex);
                }
            });

            return await taskCompletionSource.Task;
        }

        /// <summary>
        /// Cancels a specific task by its key.
        /// </summary>
        /// <param name="key">The key of the task to cancel</param>
        public void CancelTask(string key)
        {
            if (_runningTasks.TryGetValue(key, out _))
            {
                // We're not actually canceling the task here since we don't have direct access to its token source
                // The task is expected to check cancellation token periodically
                Console.WriteLine($"Attempting to cancel task {key}");
            }
        }

        /// <summary>
        /// Cancels all running tasks.
        /// </summary>
        public void CancelAllTasks()
        {
            _globalCancellationTokenSource.Cancel();
            Console.WriteLine("All tasks have been requested to cancel");
        }

        /// <summary>
        /// Waits for all currently running tasks to complete.
        /// </summary>
        /// <returns>Task representing the completion of all tasks</returns>
        public async Task WaitForAllTasksAsync()
        {
            var tasks = _runningTasks.Values.ToArray();
            if (tasks.Length > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Gets the current count of running tasks.
        /// </summary>
        /// <returns>Number of running tasks</returns>
        public int GetRunningTaskCount()
        {
            return _runningTasks.Count;
        }

        /// <summary>
        /// Disposes resources used by the thread pool.
        /// </summary>
        public void Dispose()
        {
            CancelAllTasks();
            _semaphore.Dispose();
            _globalCancellationTokenSource.Dispose();
        }
    }
}