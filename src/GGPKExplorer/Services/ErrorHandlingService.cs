using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for comprehensive error handling, logging, and recovery
    /// Reference: Requirements 8.1, 8.2, 8.3, 8.4, 8.5
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly ILogger<ErrorHandlingService> _logger;
        private readonly IContentDialogService _dialogService;
        private readonly IToastService _toastService;
        private readonly IErrorRecoveryService _recoveryService;
        private readonly string _logFilePath;

        public ErrorHandlingService(
            ILogger<ErrorHandlingService> logger,
            IContentDialogService dialogService,
            IToastService toastService,
            IErrorRecoveryService recoveryService)
        {
            _logger = logger;
            _dialogService = dialogService;
            _toastService = toastService;
            _recoveryService = recoveryService;
            
            // Create logs directory if it doesn't exist
            var logsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGPKExplorer", "Logs");
            Directory.CreateDirectory(logsDir);
            _logFilePath = Path.Combine(logsDir, $"error_log_{DateTime.Now:yyyyMMdd}.txt");
        }

        public async Task HandleExceptionAsync(Exception exception, string context = "", bool showDialog = true)
        {
            // Log the error first
            LogError(exception, context);

            // Handle specific exception types
            switch (exception)
            {
                case GGPKException ggpkException:
                    await HandleGGPKExceptionAsync(ggpkException, context);
                    return;
                
                case UnauthorizedAccessException:
                    if (showDialog)
                    {
                        await ShowErrorDialogAsync(
                            "Access Denied",
                            "The application doesn't have permission to access the requested file or directory. Please check file permissions or run as administrator.",
                            exception,
                            context,
                            showRecoveryOptions: false);
                    }
                    break;
                
                case FileNotFoundException fileNotFound:
                    if (showDialog)
                    {
                        await ShowErrorDialogAsync(
                            "File Not Found",
                            $"The file '{fileNotFound.FileName}' could not be found. It may have been moved, deleted, or renamed.",
                            exception,
                            context,
                            showRecoveryOptions: false);
                    }
                    break;
                
                case OutOfMemoryException:
                    if (showDialog)
                    {
                        await ShowErrorDialogAsync(
                            "Out of Memory",
                            "The application has run out of memory. Try closing other applications or working with smaller GGPK files.",
                            exception,
                            context,
                            showRecoveryOptions: true);
                    }
                    // Force garbage collection
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    break;
                
                default:
                    if (showDialog)
                    {
                        await ShowErrorDialogAsync(
                            "Unexpected Error",
                            "An unexpected error occurred. Please check the error log for more details.",
                            exception,
                            context,
                            showRecoveryOptions: true);
                    }
                    break;
            }
        }

        public async Task HandleGGPKExceptionAsync(GGPKException ggpkException, string context = "")
        {
            LogError(ggpkException, context);

            switch (ggpkException)
            {
                case GGPKCorruptedException corruptedException:
                    await ShowErrorDialogAsync(
                        "GGPK File Corruption Detected",
                        $"The GGPK file appears to be corrupted at offset {corruptedException.CorruptedOffset:X8}. " +
                        "This may be due to incomplete download, disk errors, or file modification.",
                        corruptedException,
                        context,
                        showRecoveryOptions: true);
                    
                    // Attempt automatic recovery
                    var recoverySuccess = await AttemptCorruptionRecoveryAsync(corruptedException);
                    if (recoverySuccess)
                    {
                        _toastService.ShowSuccess("Partial file recovery was successful. Some data may be unavailable.", "Recovery Successful");
                    }
                    break;
                
                case BundleDecompressionException bundleException:
                    await ShowErrorDialogAsync(
                        "Bundle Decompression Failed",
                        $"Failed to decompress bundle '{bundleException.BundleName}'. " +
                        "This may be due to missing oo2core.dll, corrupted bundle data, or unsupported compression format.",
                        bundleException,
                        context,
                        showRecoveryOptions: true);
                    
                    // Attempt bundle recovery
                    var bundleRecoverySuccess = await AttemptBundleRecoveryAsync(bundleException);
                    if (bundleRecoverySuccess)
                    {
                        _toastService.ShowWarning("Continuing without bundle data. Some files may be unavailable.", "Partial Recovery");
                    }
                    break;
                
                case FileOperationException fileOpException:
                    await ShowErrorDialogAsync(
                        $"File {fileOpException.OperationType} Failed",
                        $"Failed to {fileOpException.OperationType.ToString().ToLower()} file: {fileOpException.FilePath}",
                        fileOpException,
                        context,
                        showRecoveryOptions: false);
                    break;
                
                default:
                    await ShowErrorDialogAsync(
                        "GGPK Operation Failed",
                        "A GGPK-related operation failed. Please check the error log for more details.",
                        ggpkException,
                        context,
                        showRecoveryOptions: true);
                    break;
            }
        }

        public void LogError(Exception exception, string context = "")
        {
            var logEntry = CreateLogEntry("ERROR", exception.Message, context, exception);
            _logger.LogError(exception, "Error in context: {Context}", context);
            WriteToLogFile(logEntry);
        }

        public void LogWarning(string message, string context = "")
        {
            var logEntry = CreateLogEntry("WARNING", message, context);
            _logger.LogWarning("Warning in context {Context}: {Message}", context, message);
            WriteToLogFile(logEntry);
        }

        public void LogInfo(string message, string context = "")
        {
            var logEntry = CreateLogEntry("INFO", message, context);
            _logger.LogInformation("Info in context {Context}: {Message}", context, message);
            WriteToLogFile(logEntry);
        }

        public string GetDiagnosticInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== GGPK Explorer Diagnostic Information ===");
            sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Application Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            sb.AppendLine($"OS Version: {Environment.OSVersion}");
            sb.AppendLine($"CLR Version: {Environment.Version}");
            sb.AppendLine($"Working Set: {Environment.WorkingSet / 1024 / 1024} MB");
            sb.AppendLine($"GC Memory: {GC.GetTotalMemory(false) / 1024 / 1024} MB");
            sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
            sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");
            sb.AppendLine($"Command Line: {Environment.CommandLine}");
            
            // Add process information
            var process = Process.GetCurrentProcess();
            sb.AppendLine($"Process ID: {process.Id}");
            sb.AppendLine($"Process Name: {process.ProcessName}");
            sb.AppendLine($"Start Time: {process.StartTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total Processor Time: {process.TotalProcessorTime}");
            
            return sb.ToString();
        }

        public async Task<bool> AttemptCorruptionRecoveryAsync(GGPKCorruptedException corruptedException)
        {
            try
            {
                LogInfo($"Attempting corruption recovery at offset {corruptedException.CorruptedOffset:X8}", "Recovery");
                
                var recoveryResult = await _recoveryService.RecoverFromCorruptionAsync(corruptedException, string.Empty);
                
                if (recoveryResult.IsSuccessful)
                {
                    LogInfo($"Corruption recovery successful: {recoveryResult.Description}", "Recovery");
                    
                    // Show user what functionality is available
                    if (recoveryResult.AvailableFunctionality.Length > 0)
                    {
                        var availableFeatures = string.Join(", ", recoveryResult.AvailableFunctionality);
                        _toastService.ShowSuccess($"Available: {availableFeatures}", "Partial Recovery");
                    }
                }
                else
                {
                    LogWarning($"Corruption recovery failed: {recoveryResult.Description}", "Recovery");
                }
                
                return recoveryResult.IsSuccessful;
            }
            catch (Exception ex)
            {
                LogError(ex, "Recovery attempt failed");
                return false;
            }
        }

        public async Task<bool> AttemptBundleRecoveryAsync(BundleDecompressionException bundleException)
        {
            try
            {
                LogInfo($"Attempting bundle recovery for '{bundleException.BundleName}'", "Recovery");
                
                var recoveryResult = await _recoveryService.RecoverFromBundleFailureAsync(bundleException, string.Empty);
                
                if (recoveryResult.IsSuccessful)
                {
                    LogInfo($"Bundle recovery successful: {recoveryResult.Description}", "Recovery");
                    
                    // Show user what functionality is available
                    if (recoveryResult.AvailableFunctionality.Length > 0)
                    {
                        var availableFeatures = string.Join(", ", recoveryResult.AvailableFunctionality);
                        _toastService.ShowSuccess($"Available: {availableFeatures}", "Bundle Recovery");
                    }
                }
                else
                {
                    LogWarning($"Bundle recovery failed: {recoveryResult.Description}", "Recovery");
                    
                    // Show recommendations to user
                    if (recoveryResult.Recommendations.Length > 0)
                    {
                        var recommendations = string.Join("; ", recoveryResult.Recommendations);
                        _toastService.ShowWarning(recommendations, "Recovery Failed");
                    }
                }
                
                return recoveryResult.IsSuccessful;
            }
            catch (Exception ex)
            {
                LogError(ex, "Bundle recovery attempt failed");
                return false;
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message, Exception exception, string context, bool showRecoveryOptions)
        {
            try
            {
                // Temporary fix: Use MessageBox instead of ContentDialog until WPF-UI dialog service is properly configured
                var errorMessage = $"{message}\n\nContext: {context}\nException: {exception?.Message}";
                var result = System.Windows.MessageBox.Show(
                    errorMessage,
                    title,
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                
                // For now, we'll skip the retry and clipboard functionality
                await Task.CompletedTask;
            }
            catch (Exception dialogEx)
            {
                // Fallback to message box if dialog service fails
                _logger.LogError(dialogEx, "Failed to show error dialog");
                MessageBox.Show($"{title}\n\n{message}\n\nAdditional error occurred while showing dialog: {dialogEx.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private string CreateLogEntry(string level, string message, string context, Exception? exception = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");
            
            if (!string.IsNullOrEmpty(context))
            {
                sb.AppendLine($"Context: {context}");
            }
            
            if (exception != null)
            {
                sb.AppendLine($"Exception: {exception.GetType().FullName}");
                sb.AppendLine($"Stack Trace: {exception.StackTrace}");
                
                if (exception.InnerException != null)
                {
                    sb.AppendLine($"Inner Exception: {exception.InnerException.GetType().FullName} - {exception.InnerException.Message}");
                }
            }
            
            sb.AppendLine(new string('-', 80));
            return sb.ToString();
        }

        private void WriteToLogFile(string logEntry)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry);
            }
            catch (Exception ex)
            {
                // If we can't write to log file, at least log to system logger
                _logger.LogError(ex, "Failed to write to log file: {LogFilePath}", _logFilePath);
            }
        }
    }
}