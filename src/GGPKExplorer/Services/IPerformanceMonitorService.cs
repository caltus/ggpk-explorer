using System;
using System.Threading.Tasks;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for monitoring application performance and memory usage
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public interface IPerformanceMonitorService : IDisposable
    {
        /// <summary>
        /// Event raised when memory pressure is detected
        /// </summary>
        event EventHandler<MemoryPressureEventArgs>? MemoryPressureDetected;

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsUpdated;

        /// <summary>
        /// Gets the current memory usage in bytes
        /// </summary>
        long CurrentMemoryUsage { get; }

        /// <summary>
        /// Gets the peak memory usage since monitoring started
        /// </summary>
        long PeakMemoryUsage { get; }

        /// <summary>
        /// Gets whether memory pressure is currently detected
        /// </summary>
        bool IsMemoryPressureDetected { get; }

        /// <summary>
        /// Gets the current operation count in the GGPK queue
        /// </summary>
        int QueuedOperationCount { get; }

        /// <summary>
        /// Starts performance monitoring
        /// </summary>
        void StartMonitoring();

        /// <summary>
        /// Stops performance monitoring
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Forces garbage collection and memory cleanup
        /// </summary>
        /// <returns>Amount of memory freed in bytes</returns>
        Task<long> ForceMemoryCleanupAsync();

        /// <summary>
        /// Registers an operation for performance tracking
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="estimatedMemoryUsage">Estimated memory usage in bytes</param>
        void RegisterOperation(string operationName, long estimatedMemoryUsage = 0);

        /// <summary>
        /// Unregisters an operation from performance tracking
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        void UnregisterOperation(string operationName);

        /// <summary>
        /// Gets current performance metrics
        /// </summary>
        /// <returns>Current performance metrics</returns>
        PerformanceMetrics GetCurrentMetrics();
    }

    /// <summary>
    /// Event arguments for memory pressure detection
    /// </summary>
    public class MemoryPressureEventArgs : EventArgs
    {
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage { get; }

        /// <summary>
        /// Memory pressure level
        /// </summary>
        public MemoryPressureLevel PressureLevel { get; }

        /// <summary>
        /// Recommended action to take
        /// </summary>
        public string RecommendedAction { get; }

        /// <summary>
        /// Initializes a new instance of the MemoryPressureEventArgs class
        /// </summary>
        public MemoryPressureEventArgs(long currentMemoryUsage, MemoryPressureLevel pressureLevel, string recommendedAction)
        {
            CurrentMemoryUsage = currentMemoryUsage;
            PressureLevel = pressureLevel;
            RecommendedAction = recommendedAction;
        }
    }

    /// <summary>
    /// Event arguments for performance metrics updates
    /// </summary>
    public class PerformanceMetricsEventArgs : EventArgs
    {
        /// <summary>
        /// Current performance metrics
        /// </summary>
        public PerformanceMetrics Metrics { get; }

        /// <summary>
        /// Initializes a new instance of the PerformanceMetricsEventArgs class
        /// </summary>
        public PerformanceMetricsEventArgs(PerformanceMetrics metrics)
        {
            Metrics = metrics;
        }
    }

    /// <summary>
    /// Memory pressure levels
    /// </summary>
    public enum MemoryPressureLevel
    {
        /// <summary>
        /// Normal memory usage
        /// </summary>
        Normal,

        /// <summary>
        /// Moderate memory pressure
        /// </summary>
        Moderate,

        /// <summary>
        /// High memory pressure
        /// </summary>
        High,

        /// <summary>
        /// Critical memory pressure
        /// </summary>
        Critical
    }

    /// <summary>
    /// Performance metrics data
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Peak memory usage in bytes
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Number of queued operations
        /// </summary>
        public int QueuedOperations { get; set; }

        /// <summary>
        /// Number of active operations
        /// </summary>
        public int ActiveOperations { get; set; }

        /// <summary>
        /// Average operation execution time in milliseconds
        /// </summary>
        public double AverageOperationTime { get; set; }

        /// <summary>
        /// Memory pressure level
        /// </summary>
        public MemoryPressureLevel MemoryPressureLevel { get; set; }

        /// <summary>
        /// Timestamp when metrics were captured
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}