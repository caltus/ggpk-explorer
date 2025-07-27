using System;
using System.Collections.Generic;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Enhanced logging service interface for comprehensive GGPK operation logging
    /// </summary>
    public interface IEnhancedLoggingService
    {
        /// <summary>
        /// Logs the start of a GGPK operation with context
        /// </summary>
        /// <param name="operation">Name of the operation</param>
        /// <param name="filePath">Optional file path being operated on</param>
        /// <param name="context">Additional context information</param>
        void LogGGPKOperation(string operation, string? filePath = null, Dictionary<string, object>? context = null);

        /// <summary>
        /// Logs the completion of a GGPK operation with metrics
        /// </summary>
        /// <param name="operation">Name of the operation</param>
        /// <param name="duration">Time taken for the operation</param>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="metrics">Performance metrics and results</param>
        void LogGGPKOperationComplete(string operation, TimeSpan duration, bool success, Dictionary<string, object>? metrics = null);

        /// <summary>
        /// Logs file operations with detailed context
        /// </summary>
        /// <param name="operation">Type of file operation</param>
        /// <param name="filePath">Path to the file</param>
        /// <param name="fileSize">Size of the file in bytes</param>
        /// <param name="additionalContext">Additional context information</param>
        void LogFileOperation(string operation, string filePath, long? fileSize = null, string? additionalContext = null);

        /// <summary>
        /// Logs memory operations and usage patterns
        /// </summary>
        /// <param name="operation">Type of memory operation</param>
        /// <param name="memoryUsage">Memory usage in bytes</param>
        /// <param name="resourceName">Name of the resource using memory</param>
        void LogMemoryOperation(string operation, long memoryUsage, string? resourceName = null);

        /// <summary>
        /// Logs performance metrics
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Metric value</param>
        /// <param name="unit">Unit of measurement</param>
        /// <param name="context">Additional context for the metric</param>
        void LogPerformanceMetric(string metricName, double value, string? unit = null, Dictionary<string, object>? context = null);

        /// <summary>
        /// Logs performance metrics object
        /// </summary>
        /// <param name="metrics">Performance metrics to log</param>
        void LogPerformanceMetrics(GGPKExplorer.Models.PerformanceMetrics metrics);

        /// <summary>
        /// Logs a structured log entry
        /// </summary>
        /// <param name="logEntry">Structured log entry to write</param>
        void LogEntry(GGPKLogEntry logEntry);

        /// <summary>
        /// Begins a logging scope for related operations
        /// </summary>
        /// <param name="operationName">Name of the operation scope</param>
        /// <param name="properties">Optional properties for the scope</param>
        /// <returns>Disposable scope object</returns>
        IDisposable BeginScope(string operationName, Dictionary<string, object>? properties = null);

        /// <summary>
        /// Gets the current logging configuration
        /// </summary>
        EnhancedLoggingConfiguration Configuration { get; }
    }
}