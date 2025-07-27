using System;
using System.Collections.Generic;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Represents performance metrics for GGPK operations
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>
        /// Name of the operation being measured
        /// </summary>
        public string OperationName { get; set; } = string.Empty;

        /// <summary>
        /// Duration of the operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Memory usage before the operation (in bytes)
        /// </summary>
        public long MemoryBefore { get; set; }

        /// <summary>
        /// Memory usage after the operation (in bytes)
        /// </summary>
        public long MemoryAfter { get; set; }

        /// <summary>
        /// Memory delta (difference between after and before)
        /// </summary>
        public long MemoryDelta => MemoryAfter - MemoryBefore;

        /// <summary>
        /// Custom metrics specific to the operation
        /// </summary>
        public Dictionary<string, double> CustomMetrics { get; set; } = new();

        /// <summary>
        /// Timestamp when the metrics were captured
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Thread ID where the operation occurred
        /// </summary>
        public int ThreadId { get; set; } = Environment.CurrentManagedThreadId;

        /// <summary>
        /// Success indicator for the operation
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Additional context information
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }
}