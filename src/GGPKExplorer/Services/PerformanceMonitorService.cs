using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for monitoring application performance and memory usage
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public sealed class PerformanceMonitorService : IPerformanceMonitorService
    {
        private readonly ILogger<PerformanceMonitorService> _logger;
        private readonly Timer _monitoringTimer;
        private readonly ConcurrentDictionary<string, OperationMetrics> _activeOperations = new();
        private readonly object _metricsLock = new();
        
        private volatile bool _disposed;
        private volatile bool _isMonitoring;
        private long _peakMemoryUsage;
        private MemoryPressureLevel _currentPressureLevel = MemoryPressureLevel.Normal;
        
        // Memory thresholds in bytes
        private const long ModerateMemoryThreshold = 512L * 1024 * 1024; // 512 MB
        private const long HighMemoryThreshold = 1024L * 1024 * 1024; // 1 GB
        private const long CriticalMemoryThreshold = 2048L * 1024 * 1024; // 2 GB
        
        // Monitoring interval
        private static readonly TimeSpan MonitoringInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Event raised when memory pressure is detected
        /// </summary>
        public event EventHandler<MemoryPressureEventArgs>? MemoryPressureDetected;

        /// <summary>
        /// Event raised when performance metrics are updated
        /// </summary>
        public event EventHandler<PerformanceMetricsEventArgs>? PerformanceMetricsUpdated;

        /// <summary>
        /// Gets the current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage => GC.GetTotalMemory(false);

        /// <summary>
        /// Gets the peak memory usage since monitoring started
        /// </summary>
        public long PeakMemoryUsage => _peakMemoryUsage;

        /// <summary>
        /// Gets whether memory pressure is currently detected
        /// </summary>
        public bool IsMemoryPressureDetected => _currentPressureLevel > MemoryPressureLevel.Normal;

        /// <summary>
        /// Gets the current operation count in the GGPK queue
        /// </summary>
        public int QueuedOperationCount => _activeOperations.Count;

        /// <summary>
        /// Initializes a new instance of the PerformanceMonitorService class
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        public PerformanceMonitorService(ILogger<PerformanceMonitorService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Initialize monitoring timer (but don't start it yet)
            _monitoringTimer = new Timer(MonitorPerformance, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("Performance monitor service initialized");
        }

        /// <summary>
        /// Starts performance monitoring
        /// </summary>
        public void StartMonitoring()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerformanceMonitorService));

            if (_isMonitoring)
                return;

            _isMonitoring = true;
            _monitoringTimer.Change(TimeSpan.Zero, MonitoringInterval);
            
            _logger.LogInformation("Performance monitoring started");
        }

        /// <summary>
        /// Stops performance monitoring
        /// </summary>
        public void StopMonitoring()
        {
            if (_disposed)
                return;

            if (!_isMonitoring)
                return;

            _isMonitoring = false;
            _monitoringTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("Performance monitoring stopped");
        }

        /// <summary>
        /// Forces garbage collection and memory cleanup
        /// </summary>
        /// <returns>Amount of memory freed in bytes</returns>
        public async Task<long> ForceMemoryCleanupAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerformanceMonitorService));

            var memoryBefore = GC.GetTotalMemory(false);
            
            _logger.LogInformation("Forcing memory cleanup. Memory before: {MemoryBefore:N0} bytes", memoryBefore);

            // Run garbage collection on a background thread to avoid blocking UI
            await Task.Run(() =>
            {
                // Force garbage collection for all generations
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced, true);
            });

            var memoryAfter = GC.GetTotalMemory(true);
            var memoryFreed = memoryBefore - memoryAfter;
            
            _logger.LogInformation("Memory cleanup completed. Memory after: {MemoryAfter:N0} bytes, Freed: {MemoryFreed:N0} bytes", 
                memoryAfter, memoryFreed);

            return memoryFreed;
        }

        /// <summary>
        /// Registers an operation for performance tracking
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="estimatedMemoryUsage">Estimated memory usage in bytes</param>
        public void RegisterOperation(string operationName, long estimatedMemoryUsage = 0)
        {
            if (_disposed)
                return;

            if (string.IsNullOrWhiteSpace(operationName))
                throw new ArgumentException("Operation name cannot be null or empty", nameof(operationName));

            var metrics = new OperationMetrics
            {
                Name = operationName,
                StartTime = DateTime.UtcNow,
                EstimatedMemoryUsage = estimatedMemoryUsage,
                StartMemoryUsage = CurrentMemoryUsage
            };

            _activeOperations.TryAdd(operationName, metrics);
            
            _logger.LogDebug("Registered operation: {OperationName}, Estimated memory: {EstimatedMemory:N0} bytes", 
                operationName, estimatedMemoryUsage);
        }

        /// <summary>
        /// Unregisters an operation from performance tracking
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        public void UnregisterOperation(string operationName)
        {
            if (_disposed)
                return;

            if (string.IsNullOrWhiteSpace(operationName))
                return;

            if (_activeOperations.TryRemove(operationName, out var metrics))
            {
                var duration = DateTime.UtcNow - metrics.StartTime;
                var memoryDelta = CurrentMemoryUsage - metrics.StartMemoryUsage;
                
                _logger.LogDebug("Unregistered operation: {OperationName}, Duration: {Duration:F2}ms, Memory delta: {MemoryDelta:N0} bytes", 
                    operationName, duration.TotalMilliseconds, memoryDelta);
            }
        }

        /// <summary>
        /// Gets current performance metrics
        /// </summary>
        /// <returns>Current performance metrics</returns>
        public PerformanceMetrics GetCurrentMetrics()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PerformanceMonitorService));

            lock (_metricsLock)
            {
                var currentMemory = CurrentMemoryUsage;
                
                return new PerformanceMetrics
                {
                    MemoryUsage = currentMemory,
                    PeakMemoryUsage = _peakMemoryUsage,
                    QueuedOperations = _activeOperations.Count,
                    ActiveOperations = _activeOperations.Count,
                    AverageOperationTime = CalculateAverageOperationTime(),
                    MemoryPressureLevel = _currentPressureLevel,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        /// <summary>
        /// Monitors performance metrics periodically
        /// </summary>
        /// <param name="state">Timer state (unused)</param>
        private void MonitorPerformance(object? state)
        {
            if (_disposed || !_isMonitoring)
                return;

            try
            {
                var currentMemory = CurrentMemoryUsage;
                
                // Update peak memory usage
                if (currentMemory > _peakMemoryUsage)
                {
                    Interlocked.Exchange(ref _peakMemoryUsage, currentMemory);
                }

                // Check memory pressure
                var newPressureLevel = DetermineMemoryPressureLevel(currentMemory);
                if (newPressureLevel != _currentPressureLevel)
                {
                    var oldLevel = _currentPressureLevel;
                    _currentPressureLevel = newPressureLevel;
                    
                    _logger.LogWarning("Memory pressure level changed from {OldLevel} to {NewLevel}. Current memory: {CurrentMemory:N0} bytes", 
                        oldLevel, newPressureLevel, currentMemory);

                    if (newPressureLevel > MemoryPressureLevel.Normal)
                    {
                        var recommendedAction = GetRecommendedAction(newPressureLevel);
                        OnMemoryPressureDetected(new MemoryPressureEventArgs(currentMemory, newPressureLevel, recommendedAction));
                    }
                }

                // Raise performance metrics update event
                var metrics = GetCurrentMetrics();
                OnPerformanceMetricsUpdated(new PerformanceMetricsEventArgs(metrics));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance monitoring");
            }
        }

        /// <summary>
        /// Determines the memory pressure level based on current memory usage
        /// </summary>
        /// <param name="currentMemory">Current memory usage in bytes</param>
        /// <returns>Memory pressure level</returns>
        private static MemoryPressureLevel DetermineMemoryPressureLevel(long currentMemory)
        {
            return currentMemory switch
            {
                >= CriticalMemoryThreshold => MemoryPressureLevel.Critical,
                >= HighMemoryThreshold => MemoryPressureLevel.High,
                >= ModerateMemoryThreshold => MemoryPressureLevel.Moderate,
                _ => MemoryPressureLevel.Normal
            };
        }

        /// <summary>
        /// Gets recommended action for a memory pressure level
        /// </summary>
        /// <param name="pressureLevel">Memory pressure level</param>
        /// <returns>Recommended action string</returns>
        private static string GetRecommendedAction(MemoryPressureLevel pressureLevel)
        {
            return pressureLevel switch
            {
                MemoryPressureLevel.Moderate => "Consider closing unused files or reducing cache size",
                MemoryPressureLevel.High => "Close unnecessary files and force garbage collection",
                MemoryPressureLevel.Critical => "Immediately close files and restart application if needed",
                _ => "No action required"
            };
        }

        /// <summary>
        /// Calculates the average operation time for active operations
        /// </summary>
        /// <returns>Average operation time in milliseconds</returns>
        private double CalculateAverageOperationTime()
        {
            if (_activeOperations.IsEmpty)
                return 0.0;

            var totalTime = 0.0;
            var count = 0;
            var now = DateTime.UtcNow;

            foreach (var operation in _activeOperations.Values)
            {
                totalTime += (now - operation.StartTime).TotalMilliseconds;
                count++;
            }

            return count > 0 ? totalTime / count : 0.0;
        }

        /// <summary>
        /// Raises the MemoryPressureDetected event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnMemoryPressureDetected(MemoryPressureEventArgs e)
        {
            MemoryPressureDetected?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the PerformanceMetricsUpdated event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnPerformanceMetricsUpdated(PerformanceMetricsEventArgs e)
        {
            PerformanceMetricsUpdated?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the service and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            StopMonitoring();
            _monitoringTimer?.Dispose();
            _activeOperations.Clear();
            
            _logger.LogInformation("Performance monitor service disposed");
        }

        /// <summary>
        /// Metrics for tracking individual operations
        /// </summary>
        private class OperationMetrics
        {
            public string Name { get; set; } = string.Empty;
            public DateTime StartTime { get; set; }
            public long EstimatedMemoryUsage { get; set; }
            public long StartMemoryUsage { get; set; }
        }
    }
}