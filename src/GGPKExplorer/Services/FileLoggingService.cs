using System;
using System.IO;
using System.Threading;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// File-based logging service that saves logs to the logs/ folder in the application directory
    /// </summary>
    public class FileLoggingService : ILoggingService, IDisposable
    {
        private readonly string _logsDirectory;
        private readonly string _currentLogFile;
        private readonly object _lockObject = new();
        private readonly Timer _cleanupTimer;
        private bool _disposed = false;

        public FileLoggingService()
        {
            // Create logs directory in the application directory for portability
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logsDirectory = Path.Combine(appDirectory, "logs");
            
            // Ensure logs directory exists
            Directory.CreateDirectory(_logsDirectory);
            
            // Create log file with current date
            var logFileName = $"ggpk-explorer-{DateTime.Now:yyyy-MM-dd}.log";
            _currentLogFile = Path.Combine(_logsDirectory, logFileName);
            
            // Log startup
            LogInfo("GGPK Explorer started");
            LogInfo($"Application directory: {appDirectory}");
            LogInfo($"Logs directory: {_logsDirectory}");
            
            // Set up daily cleanup timer (runs every 24 hours)
            _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        }

        public void LogInfo(string message)
        {
            WriteLog("INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog("WARN", message);
        }

        public void LogError(string message)
        {
            WriteLog("ERROR", message);
        }

        public void LogError(string message, Exception exception)
        {
            var fullMessage = $"{message}\nException: {exception.GetType().Name}: {exception.Message}\nStackTrace: {exception.StackTrace}";
            WriteLog("ERROR", fullMessage);
        }

        public void LogDebug(string message)
        {
#if DEBUG
            WriteLog("DEBUG", message);
#endif
        }

        public void ClearOldLogs(int daysToKeep = 7)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                var logFiles = Directory.GetFiles(_logsDirectory, "*.log");
                
                foreach (var logFile in logFiles)
                {
                    var fileInfo = new FileInfo(logFile);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        try
                        {
                            File.Delete(logFile);
                            LogInfo($"Deleted old log file: {Path.GetFileName(logFile)}");
                        }
                        catch (Exception ex)
                        {
                            LogError($"Failed to delete old log file {Path.GetFileName(logFile)}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to clean up old log files", ex);
            }
        }

        private void WriteLog(string level, string message)
        {
            if (_disposed) return;

            try
            {
                lock (_lockObject)
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var threadId = Thread.CurrentThread.ManagedThreadId;
                    var logEntry = $"[{timestamp}] [{level}] [Thread-{threadId}] {message}";
                    
                    File.AppendAllText(_currentLogFile, logEntry + Environment.NewLine);
                    
                    // Also write to debug output in debug builds
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(logEntry);
#endif
                }
            }
            catch (Exception ex)
            {
                // Fallback to debug output if file logging fails
                System.Diagnostics.Debug.WriteLine($"Logging failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Original message: [{level}] {message}");
            }
        }

        private void CleanupCallback(object? state)
        {
            ClearOldLogs();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                LogInfo("GGPK Explorer shutting down");
                
                _cleanupTimer?.Dispose();
                _disposed = true;
            }
        }
    }
}