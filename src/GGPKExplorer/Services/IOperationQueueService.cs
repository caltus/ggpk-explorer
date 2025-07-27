using System;
using System.Threading;
using System.Threading.Tasks;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for managing GGPK operation queuing with enhanced cancellation and priority support
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public interface IOperationQueueService : IDisposable
    {
        /// <summary>
        /// Event raised when an operation starts executing
        /// </summary>
        event EventHandler<OperationStartedEventArgs>? OperationStarted;

        /// <summary>
        /// Event raised when an operation completes
        /// </summary>
        event EventHandler<OperationCompletedEventArgs>? OperationCompleted;

        /// <summary>
        /// Event raised when an operation is canceled
        /// </summary>
        event EventHandler<OperationCanceledEventArgs>? OperationCanceled;

        /// <summary>
        /// Event raised when the queue status changes
        /// </summary>
        event EventHandler<QueueStatusChangedEventArgs>? QueueStatusChanged;

        /// <summary>
        /// Gets the number of operations currently in the queue
        /// </summary>
        int QueuedOperationCount { get; }

        /// <summary>
        /// Gets whether an operation is currently executing
        /// </summary>
        bool IsOperationExecuting { get; }

        /// <summary>
        /// Gets the name of the currently executing operation
        /// </summary>
        string? CurrentOperationName { get; }

        /// <summary>
        /// Enqueues an operation for execution with normal priority
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for tracking</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the operation finishes</returns>
        Task<T> EnqueueOperationAsync<T>(Func<CancellationToken, T> operation, string operationName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Enqueues an operation for execution with specified priority
        /// </summary>
        /// <typeparam name="T">Return type of the operation</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <param name="operationName">Name of the operation for tracking</param>
        /// <param name="priority">Priority of the operation</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the operation finishes</returns>
        Task<T> EnqueueOperationAsync<T>(Func<CancellationToken, T> operation, string operationName, OperationPriority priority, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the currently executing operation
        /// </summary>
        void CancelCurrentOperation();

        /// <summary>
        /// Cancels all queued operations
        /// </summary>
        void CancelAllOperations();

        /// <summary>
        /// Cancels operations with the specified name
        /// </summary>
        /// <param name="operationName">Name of operations to cancel</param>
        void CancelOperationsByName(string operationName);

        /// <summary>
        /// Gets the current queue status
        /// </summary>
        /// <returns>Current queue status</returns>
        QueueStatus GetQueueStatus();

        /// <summary>
        /// Starts the operation queue processing
        /// </summary>
        void Start();

        /// <summary>
        /// Stops the operation queue processing
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// Priority levels for operations
    /// </summary>
    public enum OperationPriority
    {
        /// <summary>
        /// Low priority - background operations
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority - default for most operations
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority - user-initiated operations
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority - system operations that must complete
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// Event arguments for operation started events
    /// </summary>
    public class OperationStartedEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the operation
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Priority of the operation
        /// </summary>
        public OperationPriority Priority { get; }

        /// <summary>
        /// Timestamp when the operation started
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Initializes a new instance of the OperationStartedEventArgs class
        /// </summary>
        public OperationStartedEventArgs(string operationName, OperationPriority priority, DateTime startTime)
        {
            OperationName = operationName;
            Priority = priority;
            StartTime = startTime;
        }
    }

    /// <summary>
    /// Event arguments for operation completed events
    /// </summary>
    public class OperationCompletedEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the operation
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Whether the operation completed successfully
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Exception if the operation failed
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Initializes a new instance of the OperationCompletedEventArgs class
        /// </summary>
        public OperationCompletedEventArgs(string operationName, TimeSpan duration, bool success, Exception? exception = null)
        {
            OperationName = operationName;
            Duration = duration;
            Success = success;
            Exception = exception;
        }
    }

    /// <summary>
    /// Event arguments for operation canceled events
    /// </summary>
    public class OperationCanceledEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the operation
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Reason for cancellation
        /// </summary>
        public string CancellationReason { get; }

        /// <summary>
        /// Initializes a new instance of the OperationCanceledEventArgs class
        /// </summary>
        public OperationCanceledEventArgs(string operationName, string cancellationReason)
        {
            OperationName = operationName;
            CancellationReason = cancellationReason;
        }
    }

    /// <summary>
    /// Event arguments for queue status changed events
    /// </summary>
    public class QueueStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Current queue status
        /// </summary>
        public QueueStatus Status { get; }

        /// <summary>
        /// Initializes a new instance of the QueueStatusChangedEventArgs class
        /// </summary>
        public QueueStatusChangedEventArgs(QueueStatus status)
        {
            Status = status;
        }
    }

    /// <summary>
    /// Queue status information
    /// </summary>
    public class QueueStatus
    {
        /// <summary>
        /// Number of operations in the queue
        /// </summary>
        public int QueuedOperations { get; set; }

        /// <summary>
        /// Whether an operation is currently executing
        /// </summary>
        public bool IsExecuting { get; set; }

        /// <summary>
        /// Name of the currently executing operation
        /// </summary>
        public string? CurrentOperationName { get; set; }

        /// <summary>
        /// Priority of the currently executing operation
        /// </summary>
        public OperationPriority? CurrentOperationPriority { get; set; }

        /// <summary>
        /// When the current operation started
        /// </summary>
        public DateTime? CurrentOperationStartTime { get; set; }

        /// <summary>
        /// Whether the queue is running
        /// </summary>
        public bool IsRunning { get; set; }
    }
}