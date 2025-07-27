using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Enhanced file logger provider with rotation, structured logging, and async capabilities
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly EnhancedLoggingConfiguration _configuration;
        private readonly ConcurrentDictionary<string, EnhancedFileLogger> _loggers = new();
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public FileLoggerProvider(EnhancedLoggingConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            // Ensure log directory exists
            if (!Directory.Exists(_configuration.LogDirectory))
            {
                Directory.CreateDirectory(_configuration.LogDirectory);
            }

            // Set up periodic cleanup timer (runs every hour)
            _cleanupTimer = new Timer(PerformLogCleanup, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new EnhancedFileLogger(name, _configuration));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();
                
                foreach (var logger in _loggers.Values)
                {
                    logger.Dispose();
                }
                _loggers.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// Performs periodic log file cleanup
        /// </summary>
        private void PerformLogCleanup(object? state)
        {
            try
            {
                var logFiles = Directory.GetFiles(_configuration.LogDirectory, "*.log")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Remove old log files beyond the retention limit
                var filesToDelete = logFiles.Skip(_configuration.MaxLogFiles).ToList();
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch
                    {
                        // Ignore deletion errors for individual files
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors to prevent breaking the application
            }
        }
    }

    /// <summary>
    /// Enhanced file logger implementation with rotation, structured logging, and async capabilities
    /// </summary>
    public class EnhancedFileLogger : ILogger, IDisposable
    {
        private readonly string _categoryName;
        private readonly EnhancedLoggingConfiguration _configuration;
        private readonly object _lock = new object();
        private readonly Queue<string> _logQueue = new();
        private readonly Timer _flushTimer = null!;
        private string _currentLogFilePath;
        private bool _disposed = false;

        public EnhancedFileLogger(string categoryName, EnhancedLoggingConfiguration configuration)
        {
            _categoryName = categoryName ?? throw new ArgumentNullException(nameof(categoryName));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            
            _currentLogFilePath = GetCurrentLogFilePath();
            
            // Set up periodic flush timer if async logging is enabled
            if (_configuration.EnableAsyncLogging)
            {
                _flushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return new LogScope(state?.ToString() ?? string.Empty);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _configuration.MinimumLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || _disposed)
                return;

            try
            {
                var logEntry = CreateLogEntry(logLevel, eventId, state, exception, formatter);
                
                if (_configuration.EnableAsyncLogging)
                {
                    EnqueueLogEntry(logEntry);
                }
                else
                {
                    WriteLogEntry(logEntry);
                }
            }
            catch (Exception logException)
            {
                // Try to log the logging error to debug output
                try
                {
                    System.Diagnostics.Debug.WriteLine($"EnhancedFileLogger error: {logException.Message}");
                }
                catch
                {
                    // Ignore if even debug logging fails
                }
            }
        }

        /// <summary>
        /// Creates a log entry string based on configuration
        /// </summary>
        private string CreateLogEntry<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (_configuration.UseStructuredLogging)
            {
                return CreateStructuredLogEntry(logLevel, eventId, state, exception, formatter);
            }
            else
            {
                return CreateFormattedLogEntry(logLevel, eventId, state, exception, formatter);
            }
        }

        /// <summary>
        /// Creates a structured JSON log entry
        /// </summary>
        private string CreateStructuredLogEntry<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var logEntry = new GGPKLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = logLevel,
                Category = _categoryName,
                Operation = eventId.Name ?? "Unknown",
                ErrorMessage = exception?.Message,
                StackTrace = _configuration.IncludeStackTraces ? exception?.StackTrace : null,
                ThreadId = _configuration.LogThreadInfo ? Environment.CurrentManagedThreadId : 0
            };

            // Add formatted message to context
            logEntry.Context["Message"] = formatter(state, exception);
            logEntry.Context["EventId"] = eventId.Id;
            
            if (exception != null)
            {
                logEntry.Context["ExceptionType"] = exception.GetType().Name;
            }

            return JsonSerializer.Serialize(logEntry, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }) + Environment.NewLine;
        }

        /// <summary>
        /// Creates a formatted text log entry
        /// </summary>
        private string CreateFormattedLogEntry<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var memoryUsage = GC.GetTotalMemory(false);
            
            var logEntry = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";
            
            if (_configuration.LogThreadInfo)
            {
                logEntry += $" [T{Environment.CurrentManagedThreadId}]";
            }
            
            if (exception != null)
            {
                logEntry += $"\nException: {exception.Message}";
                if (_configuration.IncludeStackTraces && !string.IsNullOrEmpty(exception.StackTrace))
                {
                    logEntry += $"\nStack Trace: {exception.StackTrace}";
                }
            }
            
            logEntry += $" (Memory: {memoryUsage:N0} bytes)\n";
            return logEntry;
        }

        /// <summary>
        /// Enqueues log entry for async processing
        /// </summary>
        private void EnqueueLogEntry(string logEntry)
        {
            lock (_lock)
            {
                if (_logQueue.Count >= _configuration.AsyncLoggingBufferSize)
                {
                    // Remove oldest entry to prevent memory buildup
                    _logQueue.Dequeue();
                }
                _logQueue.Enqueue(logEntry);
            }
        }

        /// <summary>
        /// Writes log entry directly to file
        /// </summary>
        private void WriteLogEntry(string logEntry)
        {
            lock (_lock)
            {
                CheckAndRotateLogFile();
                
                try
                {
                    File.AppendAllText(_currentLogFilePath, logEntry);
                }
                catch (IOException)
                {
                    // If file is locked, try to create a new file with timestamp
                    var timestamp = DateTime.Now.ToString("HHmmss");
                    var directory = Path.GetDirectoryName(_currentLogFilePath);
                    var fileName = Path.GetFileNameWithoutExtension(_currentLogFilePath);
                    var extension = Path.GetExtension(_currentLogFilePath);
                    var newPath = Path.Combine(directory!, $"{fileName}_{timestamp}{extension}");
                    
                    _currentLogFilePath = newPath;
                    File.AppendAllText(_currentLogFilePath, logEntry);
                }
            }
        }

        /// <summary>
        /// Flushes queued log entries to file
        /// </summary>
        private void FlushLogs(object? state)
        {
            if (_disposed) return;

            List<string> entriesToWrite;
            lock (_lock)
            {
                if (_logQueue.Count == 0) return;
                
                entriesToWrite = new List<string>(_logQueue);
                _logQueue.Clear();
            }

            try
            {
                CheckAndRotateLogFile();
                var combinedEntries = string.Join("", entriesToWrite);
                File.AppendAllText(_currentLogFilePath, combinedEntries);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to flush logs: {ex.Message}");
                
                // Re-queue the entries for next flush attempt
                lock (_lock)
                {
                    foreach (var entry in entriesToWrite)
                    {
                        if (_logQueue.Count < _configuration.AsyncLoggingBufferSize)
                        {
                            _logQueue.Enqueue(entry);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if log file needs rotation and performs it if necessary
        /// </summary>
        private void CheckAndRotateLogFile()
        {
            if (!File.Exists(_currentLogFilePath)) return;

            var fileInfo = new FileInfo(_currentLogFilePath);
            if (fileInfo.Length > _configuration.MaxLogFileSizeBytes)
            {
                // Rotate the log file
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var directory = Path.GetDirectoryName(_currentLogFilePath);
                var fileName = Path.GetFileNameWithoutExtension(_currentLogFilePath);
                var extension = Path.GetExtension(_currentLogFilePath);
                var rotatedPath = Path.Combine(directory!, $"{fileName}_{timestamp}{extension}");
                
                try
                {
                    File.Move(_currentLogFilePath, rotatedPath);
                    _currentLogFilePath = GetCurrentLogFilePath();
                }
                catch
                {
                    // If rotation fails, create a new file with timestamp
                    _currentLogFilePath = Path.Combine(directory!, $"{fileName}_{timestamp}_new{extension}");
                }
            }
        }

        /// <summary>
        /// Gets the current log file path
        /// </summary>
        private string GetCurrentLogFilePath()
        {
            var fileName = _configuration.UseStructuredLogging 
                ? $"ggpk-explorer_{DateTime.Now:yyyy-MM-dd}.json"
                : $"ggpk-explorer_{DateTime.Now:yyyy-MM-dd}.log";
            
            return Path.Combine(_configuration.LogDirectory, fileName);
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _flushTimer?.Dispose();
            
            // Flush any remaining logs
            if (_configuration.EnableAsyncLogging)
            {
                FlushLogs(null);
            }
        }
    }

    /// <summary>
    /// Simple log scope implementation
    /// </summary>
    internal class LogScope : IDisposable
    {
        private readonly string _name;

        public LogScope(string name)
        {
            _name = name;
        }

        public void Dispose()
        {
            // Scope cleanup if needed
        }

        public override string ToString() => _name;
    }
}