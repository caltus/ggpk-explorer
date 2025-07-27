using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for memory management and resource cleanup
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public sealed class MemoryManagementService : IMemoryManagementService
    {
        private readonly ILogger<MemoryManagementService> _logger;
        private readonly ConcurrentDictionary<string, TrackedResource> _trackedResources = new();
        private readonly object _cleanupLock = new();
        
        private volatile bool _disposed;
        private volatile bool _automaticCleanupEnabled = true;
        private long _warningThreshold = 512 * 1024 * 1024; // 512 MB
        private long _criticalThreshold = 1024 * 1024 * 1024; // 1 GB

        /// <summary>
        /// Event raised when memory cleanup is performed
        /// </summary>
        public event EventHandler<MemoryCleanupEventArgs>? MemoryCleanupPerformed;

        /// <summary>
        /// Event raised when a resource is registered for tracking
        /// </summary>
        public event EventHandler<ResourceRegisteredEventArgs>? ResourceRegistered;

        /// <summary>
        /// Event raised when a resource is unregistered from tracking
        /// </summary>
        public event EventHandler<ResourceUnregisteredEventArgs>? ResourceUnregistered;

        /// <summary>
        /// Gets the number of tracked resources
        /// </summary>
        public int TrackedResourceCount => _trackedResources.Count;

        /// <summary>
        /// Gets the estimated memory usage of tracked resources
        /// </summary>
        public long EstimatedMemoryUsage => _trackedResources.Values.Sum(r => r.EstimatedSize);

        /// <summary>
        /// Initializes a new instance of the MemoryManagementService class
        /// </summary>
        /// <param name="logger">Logger for diagnostic information</param>
        public MemoryManagementService(ILogger<MemoryManagementService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            _logger.LogInformation("Memory management service initialized");
        }

        /// <summary>
        /// Registers a disposable resource for automatic cleanup
        /// </summary>
        /// <param name="resource">Resource to track</param>
        /// <param name="resourceName">Name of the resource for tracking</param>
        /// <param name="estimatedSize">Estimated memory size in bytes</param>
        public void RegisterResource(IDisposable resource, string resourceName, long estimatedSize = 0)
        {
            if (_disposed)
                return;

            if (resource == null)
                throw new ArgumentNullException(nameof(resource));

            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Resource name cannot be null or empty", nameof(resourceName));

            var trackedResource = new TrackedResource
            {
                Resource = new WeakReference<IDisposable>(resource),
                Name = resourceName,
                Type = resource.GetType(),
                EstimatedSize = estimatedSize,
                RegistrationTime = DateTime.UtcNow
            };

            // Use a unique key to handle multiple resources with the same name
            var uniqueKey = $"{resourceName}_{Guid.NewGuid():N}";
            
            if (_trackedResources.TryAdd(uniqueKey, trackedResource))
            {
                _logger.LogDebug("Registered resource: {ResourceName} ({ResourceType}), Size: {EstimatedSize:N0} bytes", 
                    resourceName, resource.GetType().Name, estimatedSize);
                
                OnResourceRegistered(new ResourceRegisteredEventArgs(resourceName, resource.GetType(), estimatedSize));
                
                // Check if cleanup is needed after registration
                if (_automaticCleanupEnabled && IsMemoryCleanupNeeded())
                {
                    _ = Task.Run(async () => await PerformMemoryCleanupAsync(false));
                }
            }
        }

        /// <summary>
        /// Unregisters a resource from tracking
        /// </summary>
        /// <param name="resource">Resource to unregister</param>
        public void UnregisterResource(IDisposable resource)
        {
            if (_disposed || resource == null)
                return;

            var keysToRemove = new List<string>();
            
            foreach (var kvp in _trackedResources)
            {
                if (kvp.Value.Resource.TryGetTarget(out var trackedResource) && ReferenceEquals(trackedResource, resource))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                if (_trackedResources.TryRemove(key, out var trackedResource))
                {
                    _logger.LogDebug("Unregistered resource: {ResourceName} ({ResourceType})", 
                        trackedResource.Name, trackedResource.Type.Name);
                    
                    OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, false));
                }
            }
        }

        /// <summary>
        /// Unregisters a resource by name
        /// </summary>
        /// <param name="resourceName">Name of the resource to unregister</param>
        public void UnregisterResource(string resourceName)
        {
            if (_disposed || string.IsNullOrWhiteSpace(resourceName))
                return;

            var keysToRemove = _trackedResources
                .Where(kvp => kvp.Value.Name == resourceName)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_trackedResources.TryRemove(key, out var trackedResource))
                {
                    _logger.LogDebug("Unregistered resource by name: {ResourceName} ({ResourceType})", 
                        trackedResource.Name, trackedResource.Type.Name);
                    
                    OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, false));
                }
            }
        }

        /// <summary>
        /// Forces cleanup of all tracked resources
        /// </summary>
        /// <returns>Number of resources cleaned up</returns>
        public async Task<int> CleanupAllResourcesAsync()
        {
            if (_disposed)
                return 0;

            return await Task.Run(() =>
            {
                lock (_cleanupLock)
                {
                    var cleanedCount = 0;
                    var keysToRemove = new List<string>();

                    foreach (var kvp in _trackedResources)
                    {
                        var trackedResource = kvp.Value;
                        
                        if (trackedResource.Resource.TryGetTarget(out var resource))
                        {
                            try
                            {
                                resource.Dispose();
                                cleanedCount++;
                                
                                _logger.LogDebug("Disposed resource: {ResourceName} ({ResourceType})", 
                                    trackedResource.Name, trackedResource.Type.Name);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error disposing resource: {ResourceName} ({ResourceType})", 
                                    trackedResource.Name, trackedResource.Type.Name);
                            }
                        }
                        
                        keysToRemove.Add(kvp.Key);
                    }

                    // Remove all tracked resources
                    foreach (var key in keysToRemove)
                    {
                        if (_trackedResources.TryRemove(key, out var trackedResource))
                        {
                            OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, true));
                        }
                    }

                    _logger.LogInformation("Cleaned up {CleanedCount} resources", cleanedCount);
                    return cleanedCount;
                }
            });
        }

        /// <summary>
        /// Forces cleanup of resources by type
        /// </summary>
        /// <typeparam name="T">Type of resources to cleanup</typeparam>
        /// <returns>Number of resources cleaned up</returns>
        public async Task<int> CleanupResourcesByTypeAsync<T>() where T : IDisposable
        {
            if (_disposed)
                return 0;

            return await Task.Run(() =>
            {
                lock (_cleanupLock)
                {
                    var cleanedCount = 0;
                    var keysToRemove = new List<string>();
                    var targetType = typeof(T);

                    foreach (var kvp in _trackedResources)
                    {
                        var trackedResource = kvp.Value;
                        
                        if (targetType.IsAssignableFrom(trackedResource.Type))
                        {
                            if (trackedResource.Resource.TryGetTarget(out var resource))
                            {
                                try
                                {
                                    resource.Dispose();
                                    cleanedCount++;
                                    
                                    _logger.LogDebug("Disposed resource by type: {ResourceName} ({ResourceType})", 
                                        trackedResource.Name, trackedResource.Type.Name);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error disposing resource by type: {ResourceName} ({ResourceType})", 
                                        trackedResource.Name, trackedResource.Type.Name);
                                }
                            }
                            
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    // Remove cleaned resources
                    foreach (var key in keysToRemove)
                    {
                        if (_trackedResources.TryRemove(key, out var trackedResource))
                        {
                            OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, true));
                        }
                    }

                    _logger.LogInformation("Cleaned up {CleanedCount} resources of type {ResourceType}", cleanedCount, targetType.Name);
                    return cleanedCount;
                }
            });
        }

        /// <summary>
        /// Forces cleanup of resources by name pattern
        /// </summary>
        /// <param name="namePattern">Pattern to match resource names</param>
        /// <returns>Number of resources cleaned up</returns>
        public async Task<int> CleanupResourcesByNameAsync(string namePattern)
        {
            if (_disposed || string.IsNullOrWhiteSpace(namePattern))
                return 0;

            return await Task.Run(() =>
            {
                lock (_cleanupLock)
                {
                    var cleanedCount = 0;
                    var keysToRemove = new List<string>();
                    var regex = new Regex(namePattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

                    foreach (var kvp in _trackedResources)
                    {
                        var trackedResource = kvp.Value;
                        
                        if (regex.IsMatch(trackedResource.Name))
                        {
                            if (trackedResource.Resource.TryGetTarget(out var resource))
                            {
                                try
                                {
                                    resource.Dispose();
                                    cleanedCount++;
                                    
                                    _logger.LogDebug("Disposed resource by pattern: {ResourceName} ({ResourceType})", 
                                        trackedResource.Name, trackedResource.Type.Name);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Error disposing resource by pattern: {ResourceName} ({ResourceType})", 
                                        trackedResource.Name, trackedResource.Type.Name);
                                }
                            }
                            
                            keysToRemove.Add(kvp.Key);
                        }
                    }

                    // Remove cleaned resources
                    foreach (var key in keysToRemove)
                    {
                        if (_trackedResources.TryRemove(key, out var trackedResource))
                        {
                            OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, true));
                        }
                    }

                    _logger.LogInformation("Cleaned up {CleanedCount} resources matching pattern: {Pattern}", cleanedCount, namePattern);
                    return cleanedCount;
                }
            });
        }

        /// <summary>
        /// Performs garbage collection and memory optimization
        /// </summary>
        /// <param name="aggressive">Whether to perform aggressive cleanup</param>
        /// <returns>Amount of memory freed in bytes</returns>
        public async Task<long> PerformMemoryCleanupAsync(bool aggressive = false)
        {
            if (_disposed)
                return 0;

            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var resourcesCleaned = 0;

            try
            {
                _logger.LogInformation("Starting memory cleanup (aggressive: {Aggressive}). Memory before: {MemoryBefore:N0} bytes", 
                    aggressive, memoryBefore);

                // Clean up dead weak references first
                resourcesCleaned += await CleanupDeadReferencesAsync();

                // Perform garbage collection
                await Task.Run(() =>
                {
                    if (aggressive)
                    {
                        // Aggressive cleanup: multiple GC passes
                        for (int i = 0; i < 3; i++)
                        {
                            GC.Collect(2, GCCollectionMode.Forced, true);
                            GC.WaitForPendingFinalizers();
                        }
                        GC.Collect(2, GCCollectionMode.Forced, true);
                    }
                    else
                    {
                        // Normal cleanup: single GC pass
                        GC.Collect(2, GCCollectionMode.Optimized, false);
                        GC.WaitForPendingFinalizers();
                        GC.Collect(2, GCCollectionMode.Optimized, false);
                    }
                });

                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(true);
                var memoryFreed = memoryBefore - memoryAfter;

                _logger.LogInformation("Memory cleanup completed in {Duration:F2}ms. Memory after: {MemoryAfter:N0} bytes, Freed: {MemoryFreed:N0} bytes", 
                    stopwatch.Elapsed.TotalMilliseconds, memoryAfter, memoryFreed);

                OnMemoryCleanupPerformed(new MemoryCleanupEventArgs(memoryFreed, resourcesCleaned, aggressive, stopwatch.Elapsed));

                return memoryFreed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during memory cleanup");
                return 0;
            }
        }

        /// <summary>
        /// Checks if memory pressure cleanup is needed
        /// </summary>
        /// <returns>True if cleanup is recommended</returns>
        public bool IsMemoryCleanupNeeded()
        {
            if (_disposed)
                return false;

            var currentMemory = GC.GetTotalMemory(false);
            return currentMemory >= _warningThreshold;
        }

        /// <summary>
        /// Gets memory usage statistics
        /// </summary>
        /// <returns>Memory usage statistics</returns>
        public MemoryUsageStatistics GetMemoryStatistics()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MemoryManagementService));

            return new MemoryUsageStatistics
            {
                CurrentMemoryUsage = GC.GetTotalMemory(false),
                PeakMemoryUsage = GC.GetTotalMemory(false), // WPF doesn't track peak easily
                TrackedResourceMemory = EstimatedMemoryUsage,
                TrackedResourceCount = TrackedResourceCount,
                WarningThreshold = _warningThreshold,
                CriticalThreshold = _criticalThreshold,
                AutomaticCleanupEnabled = _automaticCleanupEnabled,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Sets memory pressure thresholds
        /// </summary>
        /// <param name="warningThreshold">Warning threshold in bytes</param>
        /// <param name="criticalThreshold">Critical threshold in bytes</param>
        public void SetMemoryThresholds(long warningThreshold, long criticalThreshold)
        {
            if (warningThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(warningThreshold), "Warning threshold must be positive");

            if (criticalThreshold <= warningThreshold)
                throw new ArgumentOutOfRangeException(nameof(criticalThreshold), "Critical threshold must be greater than warning threshold");

            _warningThreshold = warningThreshold;
            _criticalThreshold = criticalThreshold;

            _logger.LogInformation("Memory thresholds updated - Warning: {WarningThreshold:N0} bytes, Critical: {CriticalThreshold:N0} bytes", 
                warningThreshold, criticalThreshold);
        }

        /// <summary>
        /// Enables or disables automatic memory cleanup
        /// </summary>
        /// <param name="enabled">Whether automatic cleanup is enabled</param>
        public void SetAutomaticCleanup(bool enabled)
        {
            _automaticCleanupEnabled = enabled;
            _logger.LogInformation("Automatic memory cleanup {Status}", enabled ? "enabled" : "disabled");
        }

        /// <summary>
        /// Cleans up dead weak references
        /// </summary>
        /// <returns>Number of dead references cleaned up</returns>
        private async Task<int> CleanupDeadReferencesAsync()
        {
            return await Task.Run(() =>
            {
                var keysToRemove = new List<string>();
                
                foreach (var kvp in _trackedResources)
                {
                    if (!kvp.Value.Resource.TryGetTarget(out _))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    if (_trackedResources.TryRemove(key, out var trackedResource))
                    {
                        OnResourceUnregistered(new ResourceUnregisteredEventArgs(trackedResource.Name, trackedResource.Type, false));
                    }
                }

                if (keysToRemove.Count > 0)
                {
                    _logger.LogDebug("Cleaned up {DeadReferenceCount} dead resource references", keysToRemove.Count);
                }

                return keysToRemove.Count;
            });
        }

        /// <summary>
        /// Raises the MemoryCleanupPerformed event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnMemoryCleanupPerformed(MemoryCleanupEventArgs e)
        {
            MemoryCleanupPerformed?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ResourceRegistered event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnResourceRegistered(ResourceRegisteredEventArgs e)
        {
            ResourceRegistered?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ResourceUnregistered event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnResourceUnregistered(ResourceUnregisteredEventArgs e)
        {
            ResourceUnregistered?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the service and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // Clean up all tracked resources
            _ = CleanupAllResourcesAsync().GetAwaiter().GetResult();
            
            _logger.LogInformation("Memory management service disposed");
        }

        /// <summary>
        /// Represents a tracked resource
        /// </summary>
        private class TrackedResource
        {
            public WeakReference<IDisposable> Resource { get; set; } = null!;
            public string Name { get; set; } = string.Empty;
            public Type Type { get; set; } = null!;
            public long EstimatedSize { get; set; }
            public DateTime RegistrationTime { get; set; }
        }
    }
}