using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Represents a structured log entry for GGPK operations
    /// </summary>
    public class GGPKLogEntry
    {
        /// <summary>
        /// Timestamp when the log entry was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Log level of the entry
        /// </summary>
        public LogLevel Level { get; set; } = LogLevel.None;

        /// <summary>
        /// Category or component that generated the log entry
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Name of the operation being logged
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// File path associated with the operation (if applicable)
        /// </summary>
        public string? FilePath { get; set; }

        /// <summary>
        /// File size in bytes (if applicable)
        /// </summary>
        public long? FileSize { get; set; }

        /// <summary>
        /// Duration of the operation (if applicable)
        /// </summary>
        public TimeSpan? Duration { get; set; }

        /// <summary>
        /// Memory usage in bytes (if applicable)
        /// </summary>
        public long? MemoryUsage { get; set; }

        /// <summary>
        /// Additional context information
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();

        /// <summary>
        /// Error message (if applicable)
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Stack trace (if applicable)
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Thread ID where the operation occurred
        /// </summary>
        public int ThreadId { get; set; } = Environment.CurrentManagedThreadId;

        /// <summary>
        /// Correlation ID for tracking related operations
        /// </summary>
        public string? CorrelationId { get; set; }
    }
}