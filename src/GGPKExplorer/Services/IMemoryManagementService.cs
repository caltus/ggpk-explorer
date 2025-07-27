using System;
using System.Threading.Tasks;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for memory management and resource cleanup
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public interface IMemoryManagementService : IDisposable
    {
        /// <summary>
        /// Event raised when memory cleanup is performed
        /// </summary>
        event EventHandler<MemoryCleanupEventArgs>? MemoryCleanupPerformed;

        /// <summary>
        /// Event raised when a resource is registered for tracking
        /// </summary>
        event EventHandler<ResourceRegisteredEventArgs>? ResourceRegistered;

        /// <summary>
        /// Event raised when a resource is unregistered from tracking
        /// </summary>
        event EventHandler<ResourceUnregisteredEventArgs>? ResourceUnregistered;

        /// <summary>
        /// Gets the number of tracked resources
        /// </summary>
        int TrackedResourceCount { get; }

        /// <summary>
        /// Gets the estimated memory usage of tracked resources
        /// </summary>
        long EstimatedMemoryUsage { get; }

        /// <summary>
        /// Registers a disposable resource for automatic cleanup
        /// </summary>
        /// <param name="resource">Resource to track</param>
        /// <param name="resourceName">Name of the resource for tracking</param>
        /// <param name="estimatedSize">Estimated memory size in bytes</param>
        void RegisterResource(IDisposable resource, string resourceName, long estimatedSize = 0);

        /// <summary>
        /// Unregisters a resource from tracking
        /// </summary>
        /// <param name="resource">Resource to unregister</param>
        void UnregisterResource(IDisposable resource);

        /// <summary>
        /// Unregisters a resource by name
        /// </summary>
        /// <param name="resourceName">Name of the resource to unregister</param>
        void UnregisterResource(string resourceName);

        /// <summary>
        /// Forces cleanup of all tracked resources
        /// </summary>
        /// <returns>Number of resources cleaned up</returns>
        Task<int> CleanupAllResourcesAsync();

        /// <summary>
        /// Forces cleanup of resources by type
        /// </summary>
        /// <typeparam name="T">Type of resources to cleanup</typeparam>
        /// <returns>Number of resources cleaned up</returns>
        Task<int> CleanupResourcesByTypeAsync<T>() where T : IDisposable;

        /// <summary>
        /// Forces cleanup of resources by name pattern
        /// </summary>
        /// <param name="namePattern">Pattern to match resource names</param>
        /// <returns>Number of resources cleaned up</returns>
        Task<int> CleanupResourcesByNameAsync(string namePattern);

        /// <summary>
        /// Performs garbage collection and memory optimization
        /// </summary>
        /// <param name="aggressive">Whether to perform aggressive cleanup</param>
        /// <returns>Amount of memory freed in bytes</returns>
        Task<long> PerformMemoryCleanupAsync(bool aggressive = false);

        /// <summary>
        /// Checks if memory pressure cleanup is needed
        /// </summary>
        /// <returns>True if cleanup is recommended</returns>
        bool IsMemoryCleanupNeeded();

        /// <summary>
        /// Gets memory usage statistics
        /// </summary>
        /// <returns>Memory usage statistics</returns>
        MemoryUsageStatistics GetMemoryStatistics();

        /// <summary>
        /// Sets memory pressure thresholds
        /// </summary>
        /// <param name="warningThreshold">Warning threshold in bytes</param>
        /// <param name="criticalThreshold">Critical threshold in bytes</param>
        void SetMemoryThresholds(long warningThreshold, long criticalThreshold);

        /// <summary>
        /// Enables or disables automatic memory cleanup
        /// </summary>
        /// <param name="enabled">Whether automatic cleanup is enabled</param>
        void SetAutomaticCleanup(bool enabled);
    }

    /// <summary>
    /// Event arguments for memory cleanup events
    /// </summary>
    public class MemoryCleanupEventArgs : EventArgs
    {
        /// <summary>
        /// Amount of memory freed in bytes
        /// </summary>
        public long MemoryFreed { get; }

        /// <summary>
        /// Number of resources cleaned up
        /// </summary>
        public int ResourcesCleaned { get; }

        /// <summary>
        /// Whether the cleanup was aggressive
        /// </summary>
        public bool WasAggressive { get; }

        /// <summary>
        /// Duration of the cleanup operation
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Initializes a new instance of the MemoryCleanupEventArgs class
        /// </summary>
        public MemoryCleanupEventArgs(long memoryFreed, int resourcesCleaned, bool wasAggressive, TimeSpan duration)
        {
            MemoryFreed = memoryFreed;
            ResourcesCleaned = resourcesCleaned;
            WasAggressive = wasAggressive;
            Duration = duration;
        }
    }

    /// <summary>
    /// Event arguments for resource registration events
    /// </summary>
    public class ResourceRegisteredEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the registered resource
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// Type of the registered resource
        /// </summary>
        public Type ResourceType { get; }

        /// <summary>
        /// Estimated size of the resource in bytes
        /// </summary>
        public long EstimatedSize { get; }

        /// <summary>
        /// Initializes a new instance of the ResourceRegisteredEventArgs class
        /// </summary>
        public ResourceRegisteredEventArgs(string resourceName, Type resourceType, long estimatedSize)
        {
            ResourceName = resourceName;
            ResourceType = resourceType;
            EstimatedSize = estimatedSize;
        }
    }

    /// <summary>
    /// Event arguments for resource unregistration events
    /// </summary>
    public class ResourceUnregisteredEventArgs : EventArgs
    {
        /// <summary>
        /// Name of the unregistered resource
        /// </summary>
        public string ResourceName { get; }

        /// <summary>
        /// Type of the unregistered resource
        /// </summary>
        public Type ResourceType { get; }

        /// <summary>
        /// Whether the resource was disposed during unregistration
        /// </summary>
        public bool WasDisposed { get; }

        /// <summary>
        /// Initializes a new instance of the ResourceUnregisteredEventArgs class
        /// </summary>
        public ResourceUnregisteredEventArgs(string resourceName, Type resourceType, bool wasDisposed)
        {
            ResourceName = resourceName;
            ResourceType = resourceType;
            WasDisposed = wasDisposed;
        }
    }

    /// <summary>
    /// Memory usage statistics
    /// </summary>
    public class MemoryUsageStatistics
    {
        /// <summary>
        /// Current memory usage in bytes
        /// </summary>
        public long CurrentMemoryUsage { get; set; }

        /// <summary>
        /// Peak memory usage in bytes
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Memory usage by tracked resources in bytes
        /// </summary>
        public long TrackedResourceMemory { get; set; }

        /// <summary>
        /// Number of tracked resources
        /// </summary>
        public int TrackedResourceCount { get; set; }

        /// <summary>
        /// Memory warning threshold in bytes
        /// </summary>
        public long WarningThreshold { get; set; }

        /// <summary>
        /// Memory critical threshold in bytes
        /// </summary>
        public long CriticalThreshold { get; set; }

        /// <summary>
        /// Whether automatic cleanup is enabled
        /// </summary>
        public bool AutomaticCleanupEnabled { get; set; }

        /// <summary>
        /// Timestamp when statistics were captured
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}