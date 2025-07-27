using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for marshaling operations to the UI thread
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public sealed class UIThreadMarshalingService : IUIThreadMarshalingService
    {
        private readonly ILogger<UIThreadMarshalingService> _logger;
        private readonly Dispatcher _uiDispatcher;

        /// <summary>
        /// Gets whether the current thread is the UI thread
        /// </summary>
        public bool IsUIThread => _uiDispatcher.CheckAccess();

        /// <summary>
        /// Initializes a new instance of the UIThreadMarshalingService class
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        public UIThreadMarshalingService(ILogger<UIThreadMarshalingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Get the UI dispatcher from the current thread (should be called from UI thread during startup)
            _uiDispatcher = Dispatcher.CurrentDispatcher;
            
            if (_uiDispatcher == null)
            {
                throw new InvalidOperationException("UIThreadMarshalingService must be initialized from the UI thread");
            }
            
            _logger.LogInformation("UI thread marshaling service initialized on thread {ThreadId}", 
                _uiDispatcher.Thread.ManagedThreadId);
        }

        /// <summary>
        /// Executes an action on the UI thread synchronously
        /// </summary>
        /// <param name="action">Action to execute</param>
        public void InvokeOnUIThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                action();
            }
            else
            {
                // Marshal to UI thread
                _uiDispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Executes an action on the UI thread asynchronously
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <returns>Task that completes when the action finishes</returns>
        public Task InvokeOnUIThreadAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                action();
                return Task.CompletedTask;
            }
            else
            {
                // Marshal to UI thread asynchronously
                return _uiDispatcher.InvokeAsync(action).Task;
            }
        }

        /// <summary>
        /// Executes a function on the UI thread synchronously
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <returns>Result of the function</returns>
        public T InvokeOnUIThread<T>(Func<T> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                return function();
            }
            else
            {
                // Marshal to UI thread
                return _uiDispatcher.Invoke(function);
            }
        }

        /// <summary>
        /// Executes a function on the UI thread asynchronously
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <returns>Task that completes with the function result</returns>
        public Task<T> InvokeOnUIThreadAsync<T>(Func<T> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                try
                {
                    var result = function();
                    return Task.FromResult(result);
                }
                catch (Exception ex)
                {
                    return Task.FromException<T>(ex);
                }
            }
            else
            {
                // Marshal to UI thread asynchronously
                return _uiDispatcher.InvokeAsync(function).Task;
            }
        }

        /// <summary>
        /// Executes an async function on the UI thread
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Async function to execute</param>
        /// <returns>Task that completes with the function result</returns>
        public async Task<T> InvokeOnUIThreadAsync<T>(Func<Task<T>> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                return await function();
            }
            else
            {
                // Marshal to UI thread and await the async function
                var result = await _uiDispatcher.InvokeAsync(async () => await function());
                return await result;
            }
        }

        /// <summary>
        /// Posts an action to be executed on the UI thread without waiting
        /// </summary>
        /// <param name="action">Action to execute</param>
        public void PostToUIThread(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                action();
            }
            else
            {
                // Post to UI thread without waiting
                _uiDispatcher.BeginInvoke(action);
            }
        }

        /// <summary>
        /// Executes an action on the UI thread with a timeout
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <returns>True if the action completed within the timeout</returns>
        public bool TryInvokeOnUIThread(Action action, TimeSpan timeout)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                try
                {
                    action();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing action on UI thread");
                    return false;
                }
            }
            else
            {
                // Marshal to UI thread with timeout
                try
                {
                    var operation = _uiDispatcher.BeginInvoke(action);
                    var result = operation.Wait(timeout);
                    
                    if (result != DispatcherOperationStatus.Completed)
                    {
                        _logger.LogWarning("UI thread operation timed out after {Timeout:F2}ms", timeout.TotalMilliseconds);
                        operation.Abort();
                        return false;
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing action on UI thread with timeout");
                    return false;
                }
            }
        }

        /// <summary>
        /// Executes a function on the UI thread with a timeout
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="result">Result of the function if successful</param>
        /// <returns>True if the function completed within the timeout</returns>
        public bool TryInvokeOnUIThread<T>(Func<T> function, TimeSpan timeout, out T result)
        {
            result = default(T)!;
            
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive");

            if (IsUIThread)
            {
                // Already on UI thread, execute directly
                try
                {
                    result = function();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing function on UI thread");
                    return false;
                }
            }
            else
            {
                // Marshal to UI thread with timeout
                try
                {
                    var operation = _uiDispatcher.BeginInvoke(function);
                    var status = operation.Wait(timeout);
                    
                    if (status == DispatcherOperationStatus.Completed)
                    {
                        result = (T)operation.Result;
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning("UI thread operation timed out after {Timeout:F2}ms", timeout.TotalMilliseconds);
                        operation.Abort();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing function on UI thread with timeout");
                    return false;
                }
            }
        }
    }
}