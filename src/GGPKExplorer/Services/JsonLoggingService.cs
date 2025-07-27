using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Dedicated service for structured JSON logging of GGPK operations
    /// </summary>
    public interface IJsonLoggingService
    {
        void LogGGPKOperation(string operation, string? filePath = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information);
        void LogFileOperation(string operation, string filePath, long? fileSize = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information);
        void LogExtractionOperation(string operation, string sourcePath, string? destinationPath = null, long? fileSize = null, TimeSpan? duration = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information);
        void LogDecompressionOperation(string operation, string filePath, long originalSize, long decompressedSize, TimeSpan duration, string compressionType, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information);
        void LogPerformanceMetric(string operation, double value, string unit, Dictionary<string, object>? context = null);
        void LogError(string operation, Exception exception, Dictionary<string, object>? context = null);
        string BeginOperationScope(string operation, Dictionary<string, object>? context = null);
        void EndOperationScope(string correlationId, bool success, Dictionary<string, object>? context = null);
    }

    /// <summary>
    /// Implementation of JSON logging service for GGPK operations
    /// </summary>
    public class JsonLoggingService : IJsonLoggingService
    {
        private readonly ILogger<JsonLoggingService> _logger;
        private readonly string _logDirectory;
        private readonly object _logLock = new object();

        public JsonLoggingService(ILogger<JsonLoggingService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Get logs directory from application base directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logDirectory = Path.Combine(appDirectory, "logs");
            
            // Ensure logs directory exists
            Directory.CreateDirectory(_logDirectory);
        }

        public void LogGGPKOperation(string operation, string? filePath = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information)
        {
            var logEntry = CreateBaseLogEntry(operation, "GGPKService", level);
            logEntry.FilePath = filePath;
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public void LogFileOperation(string operation, string filePath, long? fileSize = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information)
        {
            var logEntry = CreateBaseLogEntry(operation, "FileOperations", level);
            logEntry.FilePath = filePath;
            logEntry.FileSize = fileSize;
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public void LogExtractionOperation(string operation, string sourcePath, string? destinationPath = null, long? fileSize = null, TimeSpan? duration = null, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information)
        {
            var logEntry = CreateBaseLogEntry(operation, "Extraction", level);
            logEntry.FilePath = sourcePath;
            logEntry.FileSize = fileSize;
            logEntry.Duration = duration;
            
            logEntry.Context["SourcePath"] = sourcePath;
            if (destinationPath != null)
                logEntry.Context["DestinationPath"] = destinationPath;
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public void LogDecompressionOperation(string operation, string filePath, long originalSize, long decompressedSize, TimeSpan duration, string compressionType, Dictionary<string, object>? context = null, LogLevel level = LogLevel.Information)
        {
            var logEntry = CreateBaseLogEntry(operation, "Decompression", level);
            logEntry.FilePath = filePath;
            logEntry.Duration = duration;
            
            logEntry.Context["OriginalSize"] = originalSize;
            logEntry.Context["DecompressedSize"] = decompressedSize;
            logEntry.Context["CompressionRatio"] = originalSize > 0 ? (double)decompressedSize / originalSize : 0.0;
            logEntry.Context["CompressionType"] = compressionType;
            logEntry.Context["SpaceSaved"] = Math.Max(0, originalSize - decompressedSize);
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public void LogPerformanceMetric(string operation, double value, string unit, Dictionary<string, object>? context = null)
        {
            var logEntry = CreateBaseLogEntry(operation, "Performance", LogLevel.Debug);
            
            logEntry.Context["MetricValue"] = value;
            logEntry.Context["Unit"] = unit;
            logEntry.Context["Timestamp"] = DateTime.UtcNow;
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public void LogError(string operation, Exception exception, Dictionary<string, object>? context = null)
        {
            var logEntry = CreateBaseLogEntry(operation, "Error", LogLevel.Error);
            logEntry.ErrorMessage = exception.Message;
            logEntry.StackTrace = exception.StackTrace;
            
            logEntry.Context["ExceptionType"] = exception.GetType().Name;
            logEntry.Context["InnerException"] = exception.InnerException?.Message ?? string.Empty;
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        public string BeginOperationScope(string operation, Dictionary<string, object>? context = null)
        {
            var correlationId = Guid.NewGuid().ToString();
            var logEntry = CreateBaseLogEntry($"{operation}_Start", "OperationScope", LogLevel.Information);
            logEntry.CorrelationId = correlationId;
            
            logEntry.Context["OperationStart"] = DateTime.UtcNow;
            logEntry.Context["MemoryBefore"] = GC.GetTotalMemory(false);
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
            return correlationId;
        }

        public void EndOperationScope(string correlationId, bool success, Dictionary<string, object>? context = null)
        {
            var logEntry = CreateBaseLogEntry("OperationScope_End", "OperationScope", success ? LogLevel.Information : LogLevel.Warning);
            logEntry.CorrelationId = correlationId;
            
            logEntry.Context["OperationEnd"] = DateTime.UtcNow;
            logEntry.Context["Success"] = success;
            logEntry.Context["MemoryAfter"] = GC.GetTotalMemory(false);
            
            if (context != null)
            {
                foreach (var kvp in context)
                {
                    logEntry.Context[kvp.Key] = kvp.Value;
                }
            }

            WriteJsonLog(logEntry);
        }

        private GGPKLogEntry CreateBaseLogEntry(string operation, string category, LogLevel level)
        {
            return new GGPKLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Category = category,
                Operation = operation,
                ThreadId = Environment.CurrentManagedThreadId
            };
        }

        private void WriteJsonLog(GGPKLogEntry logEntry)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonString = JsonSerializer.Serialize(logEntry, jsonOptions);
                
                // Write to both debug output and file
                Debug.WriteLine(jsonString);
                _logger.Log(logEntry.Level, "{JsonLog}", jsonString);
                
                // Also write to dedicated JSON log file
                WriteToJsonLogFile(jsonString);
            }
            catch (Exception ex)
            {
                // Fallback logging if JSON serialization fails
                Debug.WriteLine($"JSON logging error: {ex.Message}");
                _logger.LogError(ex, "Failed to write JSON log entry for operation: {Operation}", logEntry.Operation);
            }
        }

        private void WriteToJsonLogFile(string jsonString)
        {
            try
            {
                lock (_logLock)
                {
                    var logFileName = $"ggpk-operations-{DateTime.UtcNow:yyyy-MM-dd}.json";
                    var logFilePath = Path.Combine(_logDirectory, logFileName);
                    
                    // Append to JSONL format (one JSON object per line)
                    File.AppendAllText(logFilePath, jsonString + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to write to JSON log file: {ex.Message}");
            }
        }
    }
}