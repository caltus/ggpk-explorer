using System;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for handling application errors
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Handles an exception with appropriate user feedback and logging
        /// </summary>
        /// <param name="exception">The exception to handle</param>
        /// <param name="context">Additional context about where the error occurred</param>
        /// <param name="showDialog">Whether to show an error dialog to the user</param>
        /// <returns>Task representing the error handling operation</returns>
        Task HandleExceptionAsync(Exception exception, string context = "", bool showDialog = true);

        /// <summary>
        /// Handles GGPK-specific exceptions with specialized recovery options
        /// </summary>
        /// <param name="ggpkException">The GGPK exception to handle</param>
        /// <param name="context">Additional context about the operation</param>
        /// <returns>Task representing the error handling operation</returns>
        Task HandleGGPKExceptionAsync(GGPKException ggpkException, string context = "");

        /// <summary>
        /// Logs an error without showing user dialog
        /// </summary>
        /// <param name="exception">The exception to log</param>
        /// <param name="context">Additional context information</param>
        void LogError(Exception exception, string context = "");

        /// <summary>
        /// Logs a warning message
        /// </summary>
        /// <param name="message">The warning message</param>
        /// <param name="context">Additional context information</param>
        void LogWarning(string message, string context = "");

        /// <summary>
        /// Logs an informational message
        /// </summary>
        /// <param name="message">The information message</param>
        /// <param name="context">Additional context information</param>
        void LogInfo(string message, string context = "");

        /// <summary>
        /// Gets diagnostic information about the current application state
        /// </summary>
        /// <returns>Diagnostic information string</returns>
        string GetDiagnosticInfo();

        /// <summary>
        /// Attempts to recover from a GGPK corruption error
        /// </summary>
        /// <param name="corruptedException">The corruption exception</param>
        /// <returns>True if recovery was successful, false otherwise</returns>
        Task<bool> AttemptCorruptionRecoveryAsync(GGPKCorruptedException corruptedException);

        /// <summary>
        /// Attempts to recover from a bundle decompression error
        /// </summary>
        /// <param name="bundleException">The bundle decompression exception</param>
        /// <returns>True if recovery was successful, false otherwise</returns>
        Task<bool> AttemptBundleRecoveryAsync(BundleDecompressionException bundleException);
    }
}