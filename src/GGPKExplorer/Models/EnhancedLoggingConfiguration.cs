using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Configuration settings for enhanced logging
    /// </summary>
    public class EnhancedLoggingConfiguration
    {
        /// <summary>
        /// Whether to enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// Whether to enable performance logging
        /// </summary>
        public bool EnablePerformanceLogging { get; set; } = true;

        /// <summary>
        /// Whether to enable memory usage logging
        /// </summary>
        public bool EnableMemoryLogging { get; set; } = true;

        /// <summary>
        /// Directory where log files are stored (relative to application directory)
        /// </summary>
        public string LogDirectory { get; set; } = "logs";

        /// <summary>
        /// Maximum size of a single log file in bytes
        /// </summary>
        public long MaxLogFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// Maximum number of log files to retain
        /// </summary>
        public int MaxLogFiles { get; set; } = 10;

        /// <summary>
        /// Minimum log level to write
        /// </summary>
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

        /// <summary>
        /// Whether to use structured JSON logging format
        /// </summary>
        public bool UseStructuredLogging { get; set; } = true;

        /// <summary>
        /// Whether to include stack traces in error logs
        /// </summary>
        public bool IncludeStackTraces { get; set; } = true;

        /// <summary>
        /// Whether to log thread information
        /// </summary>
        public bool LogThreadInfo { get; set; } = true;

        /// <summary>
        /// Whether to enable async logging for better performance
        /// </summary>
        public bool EnableAsyncLogging { get; set; } = true;

        /// <summary>
        /// Buffer size for async logging
        /// </summary>
        public int AsyncLoggingBufferSize { get; set; } = 1000;

        /// <summary>
        /// Timeout for flushing async logs on shutdown
        /// </summary>
        public int FlushTimeoutMs { get; set; } = 5000;
    }
}