using System;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for error recovery and graceful degradation
    /// Reference: Requirements 8.4, 8.5 - Error recovery mechanisms and graceful degradation
    /// </summary>
    public interface IErrorRecoveryService
    {
        /// <summary>
        /// Attempts to recover from a GGPK corruption error
        /// </summary>
        /// <param name="corruptedException">The corruption exception</param>
        /// <param name="filePath">Path to the corrupted GGPK file</param>
        /// <returns>Recovery result with success status and available functionality</returns>
        Task<RecoveryResult> RecoverFromCorruptionAsync(GGPKCorruptedException corruptedException, string filePath);

        /// <summary>
        /// Attempts to recover from bundle decompression failures
        /// </summary>
        /// <param name="bundleException">The bundle decompression exception</param>
        /// <param name="filePath">Path to the GGPK file with bundle issues</param>
        /// <returns>Recovery result with fallback options</returns>
        Task<RecoveryResult> RecoverFromBundleFailureAsync(BundleDecompressionException bundleException, string filePath);

        /// <summary>
        /// Attempts to recover from file operation failures
        /// </summary>
        /// <param name="fileOpException">The file operation exception</param>
        /// <returns>Recovery result with alternative approaches</returns>
        Task<RecoveryResult> RecoverFromFileOperationAsync(FileOperationException fileOpException);

        /// <summary>
        /// Attempts to recover from memory-related errors
        /// </summary>
        /// <param name="memoryException">The memory-related exception</param>
        /// <returns>Recovery result after memory cleanup</returns>
        Task<RecoveryResult> RecoverFromMemoryErrorAsync(Exception memoryException);

        /// <summary>
        /// Provides graceful degradation options when full functionality is not available
        /// </summary>
        /// <param name="exception">The exception causing the degradation</param>
        /// <returns>Degradation options and available functionality</returns>
        Task<DegradationResult> GetGracefulDegradationOptionsAsync(Exception exception);

        /// <summary>
        /// Checks system health and suggests preventive measures
        /// </summary>
        /// <returns>System health status and recommendations</returns>
        Task<SystemHealthResult> CheckSystemHealthAsync();
    }

    /// <summary>
    /// Result of an error recovery attempt
    /// </summary>
    public class RecoveryResult
    {
        /// <summary>
        /// Whether the recovery was successful
        /// </summary>
        public bool IsSuccessful { get; set; }

        /// <summary>
        /// Description of what was recovered or attempted
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Available functionality after recovery
        /// </summary>
        public string[] AvailableFunctionality { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Functionality that is no longer available
        /// </summary>
        public string[] UnavailableFunctionality { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Recommendations for the user
        /// </summary>
        public string[] Recommendations { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether a retry of the original operation might succeed
        /// </summary>
        public bool CanRetry { get; set; }
    }

    /// <summary>
    /// Result of graceful degradation analysis
    /// </summary>
    public class DegradationResult
    {
        /// <summary>
        /// Available degraded functionality
        /// </summary>
        public string[] AvailableFeatures { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Features that are disabled due to the error
        /// </summary>
        public string[] DisabledFeatures { get; set; } = Array.Empty<string>();

        /// <summary>
        /// User-friendly explanation of the degradation
        /// </summary>
        public string Explanation { get; set; } = "";

        /// <summary>
        /// Suggested workarounds
        /// </summary>
        public string[] Workarounds { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Result of system health check
    /// </summary>
    public class SystemHealthResult
    {
        /// <summary>
        /// Overall system health status
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// Available memory in MB
        /// </summary>
        public long AvailableMemoryMB { get; set; }

        /// <summary>
        /// Available disk space in MB
        /// </summary>
        public long AvailableDiskSpaceMB { get; set; }

        /// <summary>
        /// Whether required DLLs are available
        /// </summary>
        public bool RequiredDllsAvailable { get; set; }

        /// <summary>
        /// Health issues found
        /// </summary>
        public string[] Issues { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Recommendations to improve system health
        /// </summary>
        public string[] Recommendations { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// System health status levels
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// System is healthy and fully functional
        /// </summary>
        Healthy,

        /// <summary>
        /// System has minor issues but is mostly functional
        /// </summary>
        Warning,

        /// <summary>
        /// System has significant issues affecting functionality
        /// </summary>
        Critical,

        /// <summary>
        /// System is in a failed state
        /// </summary>
        Failed
    }
}