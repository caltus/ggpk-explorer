using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for managing GGPK operation queuing with enhanced cancellation and priority support
    /// 
    /// CRITICAL THREAD SAFETY: This service ensures single-threaded execution of all GGPK operations.
    /// Uses a dedicated processing thread to prevent concurrent access to LibGGPK3 which is NOT thread-safe.
    /// All operations are queued and executed sequentially to prevent GGPK file corruption.
    /// 
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public sealed class OperationQueueService : IOperationQueueService
    {
        private readonly ILogger<OperationQueueService> _logger;
        private readonly IPerformanceMonitorService _performanceMonitor;
        private readonly Thread _processingThread;
        private readonly CancellationTokenSource _shutdownTokenSource = new();
        private readonly object _queueLock = new();
        
        // Priority queues for different operation priorities
        private readonly ConcurrentQueue<QueuedOperation> _criticalQueue = new();
        private readonly ConcurrentQueue<QueuedOperation> _highQueue = new();
        private readonly ConcurrentQueue<QueuedOperation> _normalQueue = new();
        private readonly ConcurrentQueue<QueuedOperation> _lowQueue = new();
        
        private volatile bool _disposed;
        private volatile bool _isRunning;
        private volatile QueuedOperation? _currentOperation;
        private CancellationTokenSource? _currentOperationCancellation;

        /// <summary>
        /// Event raised when an operation starts executing
        /// </summary>
        public event EventHandler<OperationStartedEventArgs>? OperationStarted;

        /// <summary>
        /// Event raised when an operation completes
        /// </summary>
        public event EventHandler<OperationCompletedEventArgs>? OperationCompleted;

        /// <summary>
        /// Event raised when an operation is canceled
        /// </summary>
        public event EventHandler<OperationCanceledEventArgs>? OperationCanceled;

        /// <summary>
        /// Event raised when the queue status changes
        /// </summary>
        public event EventHandler<QueueStatusChangedEventArgs>? QueueStatusChanged;

        /// <summary>
        /// Gets the number of operations currently in the queue
        /// </summary>
        public int QueuedOperationCount => 
            _criticalQueue.Count + _highQueue.Count + _normalQueue.Count + _lowQueue.Count;

        /// <summary>
        /// Gets whether an operation is currently executing
        /// </summary>
        public bool IsOperationExecuting => _currentOperation != null;

        /// <summary>
        /// Gets the name of the currently executing operation
        /// </summary>
        public string? CurrentOperationName => _currentOperation?.Name;

        /// <summary>
        /// Initializes a new instance of the OperationQueueService class
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        /// <param name="performanceMonitor">Performance monitoring service</param>
        public OperationQueueService(ILogger<OperationQueueService> logger, IPerformanceMonitorService performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            
            // Create dedicated thread for operation processing
            _processingThread = new Thread(ProcessOperationQueue)
            {
                Name = "GGPK-OperationQueue",
                IsBackground = false
            };
            
            _logger.LogInformation("Operation queue service initialized");
        }

        /// <summary>
        /// Enqueues an operation for execution with normal priority
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for tracking</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the operation finishes</returns>
        public Task<T> EnqueueOperationAsync<T>(Func<CancellationToken, T> operation, string operationName, CancellationToken cancellationToken = default)
        {
            return EnqueueOperationAsync(operation, operationName, OperationPriority.Normal, cancellationToken);
        }

        /// <summary>
        /// Enqueues an operation for execution with specified priority
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for tracking</param>
        /// <param name="priority">Priority of the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the operation finishes</returns>
        public Task<T> EnqueueOperationAsync<T>(Func<CancellationToken, T> operation, string operationName, OperationPriority priority, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OperationQueueService));

            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            var tcs = new TaskCompletionSource<T>();
            var queuedOperation = new QueuedOperation<T>(operation, operationName, priority, cancellationToken, tcs);

            // Add to appropriate priority queue
            var targetQueue = priority switch
            {
                OperationPriority.Critical => _criticalQueue,
                OperationPriority.High => _highQueue,
                OperationPriority.Normal => _normalQueue,
                OperationPriority.Low => _lowQueue,
                _ => _normalQueue
            };

            targetQueue.Enqueue(queuedOperation);
            
            // Register with performance monitor
            _performanceMonitor.RegisterOperation(operationName);
            
            _logger.LogDebug("Enqueued operation: {OperationName} with priority: {Priority}", operationName, priority);
            
            // Notify queue status change
            OnQueueStatusChanged();

            return tcs.Task;
        }

        /// <summary>
        /// Cancels the currently executing operation
        /// </summary>
        public void CancelCurrentOperation()
        {
            if (_disposed)
                return;

            var current = _currentOperation;
            if (current != null)
            {
                _currentOperationCancellation?.Cancel();
                _logger.LogInformation("Canceled current operation: {OperationName}", current.Name);
                OnOperationCanceled(new OperationCanceledEventArgs(current.Name, "User requested cancellation"));
            }
        }

        /// <summary>
        /// Cancels all queued operations
        /// </summary>
        public void CancelAllOperations()
        {
            if (_disposed)
                return;

            var canceledCount = 0;
            
            // Cancel operations in all queues
            canceledCount += CancelOperationsInQueue(_criticalQueue);
            canceledCount += CancelOperationsInQueue(_highQueue);
            canceledCount += CancelOperationsInQueue(_normalQueue);
            canceledCount += CancelOperationsInQueue(_lowQueue);
            
            // Cancel current operation
            CancelCurrentOperation();
            
            _logger.LogInformation("Canceled {CanceledCount} queued operations", canceledCount);
            OnQueueStatusChanged();
        }

        /// <summary>
        /// Cancels operations with the specified name
        /// </summary>
        /// <param name="operationName">Name of operations to cancel</param>
        public void CancelOperationsByName(string operationName)
        {
            if (_disposed || string.IsNullOrWhiteSpace(operationName))
                return;

            var canceledCount = 0;
            
            // Cancel operations in all queues
            canceledCount += CancelOperationsInQueueByName(_criticalQueue, operationName);
            canceledCount += CancelOperationsInQueueByName(_highQueue, operationName);
            canceledCount += CancelOperationsInQueueByName(_normalQueue, operationName);
            canceledCount += CancelOperationsInQueueByName(_lowQueue, operationName);
            
            // Cancel current operation if it matches
            if (_currentOperation?.Name == operationName)
            {
                CancelCurrentOperation();
                canceledCount++;
            }
            
            _logger.LogInformation("Canceled {CanceledCount} operations with name: {OperationName}", canceledCount, operationName);
            OnQueueStatusChanged();
        }

        /// <summary>
        /// Gets the current queue status
        /// </summary>
        /// <returns>Current queue status</returns>
        public QueueStatus GetQueueStatus()
        {
            var current = _currentOperation;
            
            return new QueueStatus
            {
                QueuedOperations = QueuedOperationCount,
                IsExecuting = current != null,
                CurrentOperationName = current?.Name,
                CurrentOperationPriority = current?.Priority,
                CurrentOperationStartTime = current?.StartTime,
                IsRunning = _isRunning
            };
        }

        /// <summary>
        /// Starts the operation queue processing
        /// </summary>
        public void Start()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OperationQueueService));

            if (_isRunning)
                return;

            _isRunning = true;
            _processingThread.Start();
            
            _logger.LogInformation("Operation queue processing started");
            OnQueueStatusChanged();
        }

        /// <summary>
        /// Stops the operation queue processing
        /// </summary>
        public void Stop()
        {
            if (_disposed || !_isRunning)
                return;

            _isRunning = false;
            _shutdownTokenSource.Cancel();
            
            // Cancel all operations
            CancelAllOperations();
            
            // Wait for processing thread to finish
            if (_processingThread.IsAlive)
            {
                _processingThread.Join(TimeSpan.FromSeconds(5));
            }
            
            _logger.LogInformation("Operation queue processing stopped");
            OnQueueStatusChanged();
        }

        /// <summary>
        /// Processes the operation queue on the dedicated thread
        /// </summary>
        private void ProcessOperationQueue()
        {
            _logger.LogInformation("Operation queue processing thread started");

            while (!_shutdownTokenSource.Token.IsCancellationRequested && _isRunning)
            {
                try
                {
                    var operation = DequeueNextOperation();
                    if (operation == null)
                    {
                        // No operations available, wait a bit
                        Thread.Sleep(10);
                        continue;
                    }

                    ExecuteOperation(operation);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in operation queue processing");
                }
            }

            _logger.LogInformation("Operation queue processing thread stopped");
        }

        /// <summary>
        /// Dequeues the next operation based on priority
        /// </summary>
        /// <returns>Next operation to execute, or null if no operations are available</returns>
        private QueuedOperation? DequeueNextOperation()
        {
            // Process in priority order: Critical -> High -> Normal -> Low
            if (_criticalQueue.TryDequeue(out var operation))
                return operation;
            
            if (_highQueue.TryDequeue(out operation))
                return operation;
            
            if (_normalQueue.TryDequeue(out operation))
                return operation;
            
            if (_lowQueue.TryDequeue(out operation))
                return operation;

            return null;
        }

        /// <summary>
        /// Executes a queued operation
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        private void ExecuteOperation(QueuedOperation operation)
        {
            _currentOperation = operation;
            operation.StartTime = DateTime.UtcNow;
            
            // Create linked cancellation token
            _currentOperationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                operation.CancellationToken, _shutdownTokenSource.Token);

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Starting operation: {OperationName} with priority: {Priority}", 
                    operation.Name, operation.Priority);
                
                OnOperationStarted(new OperationStartedEventArgs(operation.Name, operation.Priority, operation.StartTime));
                OnQueueStatusChanged();

                // Execute the operation
                _currentOperationCancellation.Token.ThrowIfCancellationRequested();
                var result = operation.Execute(_currentOperationCancellation.Token);
                
                stopwatch.Stop();
                operation.SetResult(result);
                
                _logger.LogDebug("Completed operation: {OperationName} in {Duration:F2}ms", 
                    operation.Name, stopwatch.Elapsed.TotalMilliseconds);
                
                OnOperationCompleted(new OperationCompletedEventArgs(operation.Name, stopwatch.Elapsed, true));
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                operation.SetCanceled();
                
                _logger.LogDebug("Canceled operation: {OperationName} after {Duration:F2}ms", 
                    operation.Name, stopwatch.Elapsed.TotalMilliseconds);
                
                OnOperationCanceled(new OperationCanceledEventArgs(operation.Name, "Operation was canceled"));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                operation.SetException(ex);
                
                _logger.LogError(ex, "Failed operation: {OperationName} after {Duration:F2}ms", 
                    operation.Name, stopwatch.Elapsed.TotalMilliseconds);
                
                OnOperationCompleted(new OperationCompletedEventArgs(operation.Name, stopwatch.Elapsed, false, ex));
            }
            finally
            {
                // Unregister from performance monitor
                _performanceMonitor.UnregisterOperation(operation.Name);
                
                _currentOperation = null;
                _currentOperationCancellation?.Dispose();
                _currentOperationCancellation = null;
                
                OnQueueStatusChanged();
            }
        }

        /// <summary>
        /// Cancels operations in a specific queue
        /// </summary>
        /// <param name="queue">Queue to cancel operations in</param>
        /// <returns>Number of operations canceled</returns>
        private int CancelOperationsInQueue(ConcurrentQueue<QueuedOperation> queue)
        {
            var canceledCount = 0;
            var operations = new List<QueuedOperation>();
            
            // Drain the queue
            while (queue.TryDequeue(out var operation))
            {
                operations.Add(operation);
            }
            
            // Cancel all operations
            foreach (var operation in operations)
            {
                operation.SetCanceled();
                _performanceMonitor.UnregisterOperation(operation.Name);
                canceledCount++;
            }
            
            return canceledCount;
        }

        /// <summary>
        /// Cancels operations with a specific name in a queue
        /// </summary>
        /// <param name="queue">Queue to cancel operations in</param>
        /// <param name="operationName">Name of operations to cancel</param>
        /// <returns>Number of operations canceled</returns>
        private int CancelOperationsInQueueByName(ConcurrentQueue<QueuedOperation> queue, string operationName)
        {
            var canceledCount = 0;
            var operations = new List<QueuedOperation>();
            
            // Drain the queue
            while (queue.TryDequeue(out var operation))
            {
                operations.Add(operation);
            }
            
            // Process operations - cancel matching ones, re-queue others
            foreach (var operation in operations)
            {
                if (operation.Name == operationName)
                {
                    operation.SetCanceled();
                    _performanceMonitor.UnregisterOperation(operation.Name);
                    canceledCount++;
                }
                else
                {
                    queue.Enqueue(operation);
                }
            }
            
            return canceledCount;
        }

        /// <summary>
        /// Raises the OperationStarted event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnOperationStarted(OperationStartedEventArgs e)
        {
            OperationStarted?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the OperationCompleted event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnOperationCompleted(OperationCompletedEventArgs e)
        {
            OperationCompleted?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the OperationCanceled event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnOperationCanceled(OperationCanceledEventArgs e)
        {
            OperationCanceled?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the QueueStatusChanged event
        /// </summary>
        private void OnQueueStatusChanged()
        {
            var status = GetQueueStatus();
            QueueStatusChanged?.Invoke(this, new QueueStatusChangedEventArgs(status));
        }

        /// <summary>
        /// Disposes the service and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            Stop();
            _shutdownTokenSource.Dispose();
            _currentOperationCancellation?.Dispose();
            
            _logger.LogInformation("Operation queue service disposed");
        }

        /// <summary>
        /// Base class for queued operations
        /// </summary>
        private abstract class QueuedOperation
        {
            public string Name { get; }
            public OperationPriority Priority { get; }
            public CancellationToken CancellationToken { get; }
            public DateTime StartTime { get; set; }

            protected QueuedOperation(string name, OperationPriority priority, CancellationToken cancellationToken)
            {
                Name = name;
                Priority = priority;
                CancellationToken = cancellationToken;
            }

            public abstract object Execute(CancellationToken cancellationToken);
            public abstract void SetResult(object result);
            public abstract void SetCanceled();
            public abstract void SetException(Exception exception);
        }

        /// <summary>
        /// Generic queued operation with typed result
        /// </summary>
        /// <typeparam name="T">Type of the operation result</typeparam>
        private class QueuedOperation<T> : QueuedOperation
        {
            private readonly Func<CancellationToken, T> _operation;
            private readonly TaskCompletionSource<T> _taskCompletionSource;

            public QueuedOperation(Func<CancellationToken, T> operation, string name, OperationPriority priority, 
                CancellationToken cancellationToken, TaskCompletionSource<T> taskCompletionSource)
                : base(name, priority, cancellationToken)
            {
                _operation = operation;
                _taskCompletionSource = taskCompletionSource;
            }

            public override object Execute(CancellationToken cancellationToken)
            {
                return _operation(cancellationToken)!;
            }

            public override void SetResult(object result)
            {
                _taskCompletionSource.SetResult((T)result);
            }

            public override void SetCanceled()
            {
                _taskCompletionSource.SetCanceled();
            }

            public override void SetException(Exception exception)
            {
                _taskCompletionSource.SetException(exception);
            }
        }
    }
}