using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Implementation of enhanced logging service for comprehensive GGPK operation logging
    /// </summary>
    public class EnhancedLoggingService : IEnhancedLoggingService, IDisposable
    {
        private readonly ILogger<EnhancedLoggingService> _logger;
        private readonly EnhancedLoggingConfiguration _configuration;

        /// <summary>
        /// Gets the current logging configuration
        /// </summary>
        public EnhancedLoggingConfiguration Configuration => _configuration;

        /// <summary>
        /// Initializes a new instance of the EnhancedLoggingService
        /// </summary>
        /// <param name="logger">Base logger instance</param>
        /// <param name="configuration">Logging configuration</param>
        public EnhancedLoggingService(ILogger<EnhancedLoggingService> logger, EnhancedLoggingConfiguration configuration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// Logs the start of a GGPK operation with context
        /// </summary>
        public void LogGGPKOperation(string operation, string? filePath = null, Dictionary<string, object>? context = null)
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = LogLevel.Debug,
                Category = "GGPK",
                Operation = operation,
                FilePath = filePath,
                Context = context ?? new Dictionary<string, object>()
            };

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs the completion of a GGPK operation with metrics
        /// </summary>
        public void LogGGPKOperationComplete(string operation, TimeSpan duration, bool success, Dictionary<string, object>? metrics = null)
        {
            var level = success ? LogLevel.Information : LogLevel.Warning;
            if (!_logger.IsEnabled(level)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = level,
                Category = "GGPK",
                Operation = $"{operation}_Complete",
                Duration = duration,
                Context = metrics ?? new Dictionary<string, object>()
            };

            logEntry.Context["Success"] = success;
            logEntry.Context["DurationMs"] = duration.TotalMilliseconds;

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs file operations with detailed context
        /// </summary>
        public void LogFileOperation(string operation, string filePath, long? fileSize = null, string? additionalContext = null)
        {
            if (!_logger.IsEnabled(LogLevel.Debug)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = LogLevel.Debug,
                Category = "FileOperation",
                Operation = operation,
                FilePath = filePath,
                FileSize = fileSize
            };

            if (!string.IsNullOrEmpty(additionalContext))
            {
                logEntry.Context["AdditionalContext"] = additionalContext;
            }

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs memory operations and usage patterns
        /// </summary>
        public void LogMemoryOperation(string operation, long memoryUsage, string? resourceName = null)
        {
            if (!_configuration.EnableMemoryLogging || !_logger.IsEnabled(LogLevel.Debug)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = LogLevel.Debug,
                Category = "Memory",
                Operation = operation,
                MemoryUsage = memoryUsage
            };

            if (!string.IsNullOrEmpty(resourceName))
            {
                logEntry.Context["ResourceName"] = resourceName;
            }

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs performance metrics
        /// </summary>
        public void LogPerformanceMetric(string metricName, double value, string? unit = null, Dictionary<string, object>? context = null)
        {
            if (!_configuration.EnablePerformanceLogging || !_logger.IsEnabled(LogLevel.Debug)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = LogLevel.Debug,
                Category = "Performance",
                Operation = metricName,
                Context = context ?? new Dictionary<string, object>()
            };

            logEntry.Context["MetricValue"] = value;
            if (!string.IsNullOrEmpty(unit))
            {
                logEntry.Context["Unit"] = unit;
            }

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs performance metrics object
        /// </summary>
        public void LogPerformanceMetrics(GGPKExplorer.Models.PerformanceMetrics metrics)
        {
            if (!_configuration.EnablePerformanceLogging || !_logger.IsEnabled(LogLevel.Information)) return;

            var logEntry = new GGPKLogEntry
            {
                Level = LogLevel.Information,
                Category = "Performance",
                Operation = metrics.OperationName,
                Duration = metrics.Duration,
                MemoryUsage = metrics.MemoryDelta,
                Context = new Dictionary<string, object>
                {
                    ["MemoryBefore"] = metrics.MemoryBefore,
                    ["MemoryAfter"] = metrics.MemoryAfter,
                    ["MemoryDelta"] = metrics.MemoryDelta,
                    ["Success"] = metrics.Success,
                    ["CustomMetrics"] = metrics.CustomMetrics
                }
            };

            // Add custom metrics to context
            foreach (var metric in metrics.CustomMetrics)
            {
                logEntry.Context[$"Metric_{metric.Key}"] = metric.Value;
            }

            LogEntry(logEntry);
        }

        /// <summary>
        /// Logs a structured log entry
        /// </summary>
        public void LogEntry(GGPKLogEntry logEntry)
        {
            if (!_logger.IsEnabled(logEntry.Level)) return;

            try
            {
                if (_configuration.UseStructuredLogging)
                {
                    LogStructuredEntry(logEntry);
                }
                else
                {
                    LogFormattedEntry(logEntry);
                }
            }
            catch (Exception ex)
            {
                // Fallback logging to prevent logging failures from breaking the application
                _logger.LogError(ex, "Failed to write log entry for operation: {Operation}", logEntry.Operation);
            }
        }

        /// <summary>
        /// Begins a logging scope for related operations
        /// </summary>
        public IDisposable BeginScope(string operationName, Dictionary<string, object>? properties = null)
        {
            var scopeProperties = new Dictionary<string, object>
            {
                ["Operation"] = operationName,
                ["CorrelationId"] = Guid.NewGuid().ToString("N")[..8]
            };

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    scopeProperties[prop.Key] = prop.Value;
                }
            }

            return _logger.BeginScope(scopeProperties) ?? throw new InvalidOperationException("Failed to create logger scope");
        }

        /// <summary>
        /// Logs structured entry with detailed context
        /// </summary>
        private void LogStructuredEntry(GGPKLogEntry logEntry)
        {
            var message = BuildLogMessage(logEntry);
            var args = BuildLogArguments(logEntry);

            _logger.Log(logEntry.Level, message, args);
        }

        /// <summary>
        /// Logs formatted entry for human readability
        /// </summary>
        private void LogFormattedEntry(GGPKLogEntry logEntry)
        {
            var message = FormatLogMessage(logEntry);
            _logger.Log(logEntry.Level, message);
        }

        /// <summary>
        /// Builds structured log message template
        /// </summary>
        private string BuildLogMessage(GGPKLogEntry logEntry)
        {
            var parts = new List<string>
            {
                "[{Category}] {Operation}"
            };

            if (!string.IsNullOrEmpty(logEntry.FilePath))
                parts.Add("File: {FilePath}");

            if (logEntry.FileSize.HasValue)
                parts.Add("Size: {FileSize:N0} bytes");

            if (logEntry.Duration.HasValue)
                parts.Add("Duration: {Duration:F2}ms");

            if (logEntry.MemoryUsage.HasValue)
                parts.Add("Memory: {MemoryUsage:N0} bytes");

            if (_configuration.LogThreadInfo)
                parts.Add("Thread: {ThreadId}");

            if (!string.IsNullOrEmpty(logEntry.ErrorMessage))
                parts.Add("Error: {ErrorMessage}");

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Builds log arguments array for structured logging
        /// </summary>
        private object[] BuildLogArguments(GGPKLogEntry logEntry)
        {
            var args = new List<object>
            {
                logEntry.Category,
                logEntry.Operation
            };

            if (!string.IsNullOrEmpty(logEntry.FilePath))
                args.Add(logEntry.FilePath);

            if (logEntry.FileSize.HasValue)
                args.Add(logEntry.FileSize.Value);

            if (logEntry.Duration.HasValue)
                args.Add(logEntry.Duration.Value.TotalMilliseconds);

            if (logEntry.MemoryUsage.HasValue)
                args.Add(logEntry.MemoryUsage.Value);

            if (_configuration.LogThreadInfo)
                args.Add(logEntry.ThreadId);

            if (!string.IsNullOrEmpty(logEntry.ErrorMessage))
                args.Add(logEntry.ErrorMessage);

            return args.ToArray();
        }

        /// <summary>
        /// Formats log message for human readability
        /// </summary>
        private string FormatLogMessage(GGPKLogEntry logEntry)
        {
            var message = $"[{logEntry.Category}] {logEntry.Operation}";

            if (!string.IsNullOrEmpty(logEntry.FilePath))
                message += $" | File: {logEntry.FilePath}";

            if (logEntry.FileSize.HasValue)
                message += $" | Size: {logEntry.FileSize:N0} bytes";

            if (logEntry.Duration.HasValue)
                message += $" | Duration: {logEntry.Duration.Value.TotalMilliseconds:F2}ms";

            if (logEntry.MemoryUsage.HasValue)
                message += $" | Memory: {logEntry.MemoryUsage:N0} bytes";

            if (_configuration.LogThreadInfo)
                message += $" | Thread: {logEntry.ThreadId}";

            if (!string.IsNullOrEmpty(logEntry.ErrorMessage))
                message += $" | Error: {logEntry.ErrorMessage}";

            // Add context information
            if (logEntry.Context.Count > 0)
            {
                var contextParts = new List<string>();
                foreach (var kvp in logEntry.Context)
                {
                    contextParts.Add($"{kvp.Key}={kvp.Value}");
                }
                message += $" | Context: {string.Join(", ", contextParts)}";
            }

            return message;
        }

        /// <summary>
        /// Disposes the logging service
        /// </summary>
        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}