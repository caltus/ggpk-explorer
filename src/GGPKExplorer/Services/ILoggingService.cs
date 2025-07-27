using System;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Interface for logging service
    /// </summary>
    public interface ILoggingService
    {
        /// <summary>
        /// Logs an information message
        /// </summary>
        void LogInfo(string message);

        /// <summary>
        /// Logs a warning message
        /// </summary>
        void LogWarning(string message);

        /// <summary>
        /// Logs an error message
        /// </summary>
        void LogError(string message);

        /// <summary>
        /// Logs an error with exception details
        /// </summary>
        void LogError(string message, Exception exception);

        /// <summary>
        /// Logs a debug message (only in debug builds)
        /// </summary>
        void LogDebug(string message);

        /// <summary>
        /// Clears old log files to prevent disk space issues
        /// </summary>
        void ClearOldLogs(int daysToKeep = 7);
    }
}