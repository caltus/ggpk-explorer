using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wpf.Ui;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Services;
using GGPKExplorer.Models;
using GGPKExplorer.Deployment;

namespace GGPKExplorer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;
    private string _logDirectory = string.Empty;
    private string _startupLogFile = string.Empty;

    /// <summary>
    /// Gets the service provider for dependency injection
    /// </summary>
    public IServiceProvider? Services => _host?.Services;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllocConsole();

    /// <summary>
    /// Determines if the debug console should be enabled based on settings and build configuration
    /// </summary>
    private bool ShouldEnableDebugConsole()
    {
        try
        {
            // Always allow debug console in debug builds
            #if DEBUG
            // Try to load settings to check user preference
            var settingsService = new PortableSettingsService();
            settingsService.LoadSettingsAsync().Wait(TimeSpan.FromSeconds(2)); // Quick timeout
            return settingsService.GetSetting("EnableDebugConsole", true); // Default to true in debug builds
            #else
            // In release builds, only enable if explicitly requested by user
            var settingsService = new PortableSettingsService();
            settingsService.LoadSettingsAsync().Wait(TimeSpan.FromSeconds(2)); // Quick timeout
            return settingsService.GetSetting("EnableDebugConsole", false); // Default to false in release builds
            #endif
        }
        catch
        {
            // If settings can't be loaded, fall back to build configuration
            #if DEBUG
            return true;
            #else
            return false;
            #endif
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            // Check if debug console should be enabled
            if (ShouldEnableDebugConsole())
            {
                AllocConsole();
                Console.WriteLine("=== GGPK EXPLORER DEBUG CONSOLE ===");
                Console.WriteLine("Debug output will appear here");
            }
            
            #if DEBUG
            Console.WriteLine("=== APPLICATION STARTUP BEGINNING ===");
            Console.WriteLine($"Startup arguments: {string.Join(" ", e.Args)}");
            #endif
            
            // Initialize logging first
            InitializeLogging();
            LogStartupStep("Application startup initiated");
        }
        catch (Exception loggingEx)
        {
            #if DEBUG
            Console.WriteLine($"OnStartup: CRITICAL - Failed to initialize logging: {loggingEx.Message}");
            #endif
            // Continue without logging
        }

        try
        {
            // Initialize native libraries
            try
            {
                if (!InitializeNativeLibraries())
                {
                    LogStartupError("Failed to initialize native libraries - continuing without GGPK support");
                }
            }
            catch (Exception ex)
            {
                LogStartupError($"Exception during native library initialization: {ex.Message}");
            }

            // Set up global exception handlers
            SetupGlobalExceptionHandling();

            // Ensure STA threading model
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            }

            // Configure dependency injection
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                    logging.AddDebug();
                    // Add file logging
                    var loggingConfig = new GGPKExplorer.Models.EnhancedLoggingConfiguration
                    {
                        LogDirectory = _logDirectory
                    };
                    logging.AddProvider(new FileLoggerProvider(loggingConfig));
                })
                .Build();

            // Start the host
            _host.Start();

            // Get the main window from DI container
            LogStartupStep("Creating main window");
            
            try
            {
                var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                mainWindow.Show();
                LogStartupStep("Main window shown successfully");
            }
            catch (Exception mainWindowEx)
            {
                LogStartupError($"Failed to create main window: {mainWindowEx}");
                LogStartupError($"Stack trace: {mainWindowEx.StackTrace}");
                if (mainWindowEx.InnerException != null)
                {
                    LogStartupError($"Inner exception: {mainWindowEx.InnerException}");
                    LogStartupError($"Inner stack trace: {mainWindowEx.InnerException.StackTrace}");
                }
                throw; // Re-throw to be caught by outer exception handler
            }

            // Ensure application shuts down when main window closes
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            LogStartupSummary();
            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            LogStartupError($"Critical startup error: {ex}");
            
            var startupErrorMessage = $"A critical error occurred during application startup:\n\n{ex.Message}\n\n" +
                $"Check the log file for details: {_startupLogFile}";
            
            MessageBox.Show(
                startupErrorMessage,
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Shutdown(1);
        }
    }



    private void ConfigureServices(IServiceCollection services)
    {
        // Register WPF-UI services
        services.AddSingleton<Wpf.Ui.IThemeService, Wpf.Ui.ThemeService>();
        services.AddSingleton<ITaskBarService, TaskBarService>();
        services.AddSingleton<IContentDialogService, ContentDialogService>();
        // ISnackbarService removed - using FloatingToastService instead
        
        // Register UI polish services
        services.AddSingleton<GGPKExplorer.Services.IThemeService, GGPKExplorer.Services.ThemeService>();
        services.AddSingleton<GGPKExplorer.Services.IDragDropService, GGPKExplorer.Services.DragDropService>();

        // Register performance and threading services
        services.AddSingleton<GGPKExplorer.Services.IPerformanceMonitorService, GGPKExplorer.Services.PerformanceMonitorService>();
        services.AddSingleton<GGPKExplorer.Services.IOperationQueueService, GGPKExplorer.Services.OperationQueueService>();
        services.AddSingleton<GGPKExplorer.Services.IUIThreadMarshalingService, GGPKExplorer.Services.UIThreadMarshalingService>();
        services.AddSingleton<GGPKExplorer.Services.IMemoryManagementService, GGPKExplorer.Services.MemoryManagementService>();

        LogStartupStep("Configuring services");
        // Configure enhanced logging with production-ready settings
        var loggingConfig = new GGPKExplorer.Models.EnhancedLoggingConfiguration
        {
            EnablePerformanceLogging = true,
            EnableMemoryLogging = true,
            UseStructuredLogging = true,
            LogThreadInfo = true,
            MaxLogFileSizeBytes = 50 * 1024 * 1024, // 50MB
            MaxLogFiles = 10,
            MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.Information,
            LogDirectory = _logDirectory // Ensure logs go to the logs/ folder
        };

        // Validate logging configuration
        if (!ValidateLoggingConfiguration(loggingConfig))
        {
            LogStartupError("Invalid logging configuration detected, using defaults");
            loggingConfig = new GGPKExplorer.Models.EnhancedLoggingConfiguration(); // Use defaults
        }

        // Register enhanced logging configuration as singleton
        services.AddSingleton(loggingConfig);

        // Register enhanced logging service
        services.AddSingleton<GGPKExplorer.Services.IEnhancedLoggingService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<GGPKExplorer.Services.EnhancedLoggingService>>();
            var config = provider.GetRequiredService<GGPKExplorer.Models.EnhancedLoggingConfiguration>();
            return new GGPKExplorer.Services.EnhancedLoggingService(logger, config);
        });

        // Register application services
        services.AddSingleton<GGPKExplorer.Services.IErrorRecoveryService, GGPKExplorer.Services.ErrorRecoveryService>();
        services.AddSingleton<GGPKExplorer.Services.IErrorHandlingService, GGPKExplorer.Services.ErrorHandlingService>();
        services.AddSingleton<GGPKExplorer.Services.ISettingsService, GGPKExplorer.Services.PortableSettingsService>();
        // Register simple file logging service (in addition to enhanced logging)
        services.AddSingleton<GGPKExplorer.Services.ILoggingService, GGPKExplorer.Services.FileLoggingService>();
        services.AddSingleton<GGPKExplorer.Services.ISearchService, GGPKExplorer.Services.SearchService>();
        services.AddSingleton<GGPKExplorer.Services.IExtractionService, GGPKExplorer.Services.ExtractionService>();

        
        // Register floating toast notification service (doesn't need SnackbarPresenter)
        services.AddSingleton<GGPKExplorer.Services.IToastService, GGPKExplorer.Services.FloatingToastService>();
        
        services.AddSingleton<GGPKExplorer.Services.IGGPKService>(provider =>
            new GGPKExplorer.Services.GGPKService(
                provider.GetRequiredService<GGPKExplorer.Services.IErrorHandlingService>(),
                provider.GetRequiredService<GGPKExplorer.Services.IOperationQueueService>(),
                provider.GetRequiredService<GGPKExplorer.Services.IPerformanceMonitorService>(),
                provider.GetRequiredService<GGPKExplorer.Services.IUIThreadMarshalingService>(),
                provider.GetRequiredService<GGPKExplorer.Services.IMemoryManagementService>(),
                provider.GetRequiredService<ILogger<GGPKExplorer.Services.GGPKService>>(),
                provider.GetRequiredService<GGPKExplorer.Services.IEnhancedLoggingService>()));
        services.AddSingleton<GGPKExplorer.Services.IFileOperationsService>(provider =>
            new GGPKExplorer.Services.FileOperationsService(
                provider.GetRequiredService<GGPKExplorer.Services.IGGPKService>(),
                provider.GetRequiredService<GGPKExplorer.Services.IErrorHandlingService>()));

        // Register ViewModels
        services.AddTransient<GGPKExplorer.ViewModels.MainViewModel>();
        services.AddTransient<GGPKExplorer.ViewModels.SettingsDialogViewModel>();

        // Register Windows and Dialogs
        services.AddTransient<MainWindow>();
        services.AddTransient<GGPKExplorer.Views.Dialogs.SettingsDialog>();
        services.AddTransient<GGPKExplorer.Views.Dialogs.AboutDialog>();
        
        // Register dialog factories
        services.AddTransient<Func<GGPKExplorer.Views.Dialogs.AboutDialog>>(provider => 
            () => provider.GetRequiredService<GGPKExplorer.Views.Dialogs.AboutDialog>());

    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_startupLogFile))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] Application shutdown (Exit Code: {e.ApplicationExitCode})\n";
                File.AppendAllText(_startupLogFile, logEntry);
            }
        }
        catch
        {
            // Ignore logging errors
        }
        
        try
        {
            NativeLibraryLoader.Cleanup();
        }
        catch
        {
            // Ignore cleanup errors
        }
        
        try
        {
            if (_host != null)
            {
                var stopTask = _host.StopAsync();
                if (!stopTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    // Force disposal if timeout
                }
                _host.Dispose();
                _host = null;
            }
        }
        catch
        {
            // Ignore disposal errors
        }
        
        base.OnExit(e);
    }

    /// <summary>
    /// Initializes the logging system and creates the logs directory
    /// </summary>
    private void InitializeLogging()
    {
        try
        {
            // Create logs directory in application folder
            _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(_logDirectory);

            // Create simple startup log file
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd");
            _startupLogFile = Path.Combine(_logDirectory, $"application_{timestamp}.log");
            
            // Write minimal startup header
            var startupInfo = $"[{DateTime.Now:HH:mm:ss}] Application started (PID: {System.Diagnostics.Process.GetCurrentProcess().Id})\n";
            File.AppendAllText(_startupLogFile, startupInfo);
        }
        catch
        {
            // Silently continue without file logging if initialization fails
            _startupLogFile = string.Empty;
        }
    }

    /// <summary>
    /// Logs a startup step to the startup log file
    /// </summary>
    private void LogStartupStep(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(_startupLogFile))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(_startupLogFile, logEntry);
            }
        }
        catch
        {
            // Ignore logging errors to prevent cascading failures
        }
    }

    /// <summary>
    /// Logs a startup error to the startup log file
    /// </summary>
    private void LogStartupError(string message)
    {
        try
        {
            if (!string.IsNullOrEmpty(_startupLogFile))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] ERROR: {message}\n";
                File.AppendAllText(_startupLogFile, logEntry);
            }
        }
        catch
        {
            // Ignore logging errors to prevent cascading failures
        }
    }

    /// <summary>
    /// Logs a simple startup completion message
    /// </summary>
    private void LogStartupSummary()
    {
        try
        {
            if (!string.IsNullOrEmpty(_startupLogFile))
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] Application startup completed\n";
                File.AppendAllText(_startupLogFile, logEntry);
            }
        }
        catch
        {
            // Ignore logging errors
        }
    }

    /// <summary>
    /// Initializes native libraries required for GGPK operations
    /// </summary>
    private bool InitializeNativeLibraries()
    {
        try
        {
            // Verify native libraries are present
            if (!DeploymentConfig.VerifyNativeLibraries())
            {
                var missing = DeploymentConfig.GetMissingLibraries();
                var missingMessage = $"Missing required native libraries:\n{string.Join("\n", missing)}\n\n" +
                    "Please reinstall the application or contact support.";
                
                System.Diagnostics.Debug.WriteLine("=== MISSING NATIVE LIBRARIES ERROR ===");
                System.Diagnostics.Debug.WriteLine($"Missing libraries: {string.Join(", ", missing)}");
                System.Diagnostics.Debug.WriteLine($"Showing MessageBox: {missingMessage}");
                System.Diagnostics.Debug.WriteLine("=== END MISSING LIBRARIES ERROR ===");
                
                MessageBox.Show(
                    missingMessage,
                    "Missing Libraries",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Load native libraries
            return NativeLibraryLoader.InitializeNativeLibraries();
        }
        catch (Exception ex)
        {
            var initErrorMessage = $"Error initializing native libraries:\n{ex.Message}";
            
            System.Diagnostics.Debug.WriteLine("=== NATIVE LIBRARY INITIALIZATION ERROR ===");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {ex.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Showing MessageBox: {initErrorMessage}");
            System.Diagnostics.Debug.WriteLine("=== END INITIALIZATION ERROR ===");
            
            MessageBox.Show(
                initErrorMessage,
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>
    /// Sets up global exception handling for the application
    /// Reference: Requirements 8.1, 8.2 - Global exception handler with user-friendly error dialogs
    /// </summary>
    private void SetupGlobalExceptionHandling()
    {
        // Handle unhandled exceptions in the UI thread
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Handle unhandled exceptions in background threads
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // Handle unhandled exceptions in Task operations
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Handles unhandled exceptions in the UI thread
    /// </summary>
    private async void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            // Log the exception details immediately
            System.Diagnostics.Debug.WriteLine("=== UNHANDLED UI THREAD EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Exception Type: {e.Exception.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Message: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"StackTrace: {e.Exception.StackTrace}");
            if (e.Exception.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner Exception: {e.Exception.InnerException.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Inner Message: {e.Exception.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner StackTrace: {e.Exception.InnerException.StackTrace}");
            }
            System.Diagnostics.Debug.WriteLine("=== END EXCEPTION DETAILS ===");

            var errorService = _host?.Services?.GetService<IErrorHandlingService>();
            if (errorService != null)
            {
                System.Diagnostics.Debug.WriteLine("OnDispatcherUnhandledException: Using error service to handle exception");
                await errorService.HandleExceptionAsync(e.Exception, "UI Thread", showDialog: true);
                e.Handled = true; // Prevent application crash
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnDispatcherUnhandledException: Error service not available, showing fallback dialog");
                // Fallback if error service is not available
                var errorMessage = $"An unexpected error occurred in the UI thread:\n\n{e.Exception.Message}\n\nThe application will continue running, but some functionality may be affected.";
                System.Diagnostics.Debug.WriteLine($"OnDispatcherUnhandledException: Showing MessageBox: {errorMessage}");
                
                MessageBox.Show(
                    errorMessage,
                    "Unexpected Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                e.Handled = true;
            }
        }
        catch (Exception handlerException)
        {
            System.Diagnostics.Debug.WriteLine("=== CRITICAL ERROR IN EXCEPTION HANDLER ===");
            System.Diagnostics.Debug.WriteLine($"Handler Exception: {handlerException.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Handler Message: {handlerException.Message}");
            System.Diagnostics.Debug.WriteLine($"Handler StackTrace: {handlerException.StackTrace}");
            System.Diagnostics.Debug.WriteLine($"Original Exception: {e.Exception.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Original Message: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine("=== END CRITICAL ERROR ===");
            
            // Last resort - show basic error dialog
            var criticalMessage = $"Critical error in exception handler:\n{handlerException.Message}\n\nOriginal error:\n{e.Exception.Message}";
            System.Diagnostics.Debug.WriteLine($"OnDispatcherUnhandledException: Showing critical error MessageBox: {criticalMessage}");
            
            MessageBox.Show(
                criticalMessage,
                "Critical Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Handles unhandled exceptions in background threads
    /// </summary>
    private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            // Log the exception details immediately
            System.Diagnostics.Debug.WriteLine("=== UNHANDLED BACKGROUND THREAD EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Is Terminating: {e.IsTerminating}");
            if (e.ExceptionObject is Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Exception Type: {exception.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Message: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {exception.StackTrace}");
                if (exception.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner Exception: {exception.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Inner Message: {exception.InnerException.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Exception Object: {e.ExceptionObject}");
            }
            System.Diagnostics.Debug.WriteLine("=== END BACKGROUND EXCEPTION DETAILS ===");

            var errorService = _host?.Services?.GetService<IErrorHandlingService>();
            if (errorService != null && e.ExceptionObject is Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("OnUnhandledException: Using error service to handle exception");
                await errorService.HandleExceptionAsync(ex, "Background Thread", showDialog: !e.IsTerminating);
            }
            
            if (e.IsTerminating)
            {
                System.Diagnostics.Debug.WriteLine("OnUnhandledException: Application is terminating, showing critical error dialog");
                // Application is terminating - show critical error
                if (e.ExceptionObject is Exception terminatingException)
                {
                    var terminatingMessage = $"A critical error has occurred and the application must close:\n\n{terminatingException.Message}";
                    System.Diagnostics.Debug.WriteLine($"OnUnhandledException: Showing terminating MessageBox: {terminatingMessage}");
                    
                    MessageBox.Show(
                        terminatingMessage,
                        "Critical Error - Application Closing",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
        catch (Exception handlerException)
        {
            System.Diagnostics.Debug.WriteLine("=== CRITICAL ERROR IN BACKGROUND EXCEPTION HANDLER ===");
            System.Diagnostics.Debug.WriteLine($"Handler Exception: {handlerException.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Handler Message: {handlerException.Message}");
            System.Diagnostics.Debug.WriteLine($"Handler StackTrace: {handlerException.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=== END CRITICAL BACKGROUND ERROR ===");
        }
    }

    /// <summary>
    /// Handles unhandled exceptions in Task operations
    /// </summary>
    private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            // Log the exception details immediately
            System.Diagnostics.Debug.WriteLine("=== UNHANDLED TASK EXCEPTION ===");
            System.Diagnostics.Debug.WriteLine($"Aggregate Exception Count: {e.Exception.InnerExceptions.Count}");
            
            for (int i = 0; i < e.Exception.InnerExceptions.Count; i++)
            {
                var innerException = e.Exception.InnerExceptions[i];
                System.Diagnostics.Debug.WriteLine($"--- Inner Exception {i + 1} ---");
                System.Diagnostics.Debug.WriteLine($"Exception Type: {innerException.GetType().FullName}");
                System.Diagnostics.Debug.WriteLine($"Message: {innerException.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {innerException.StackTrace}");
                if (innerException.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Nested Inner Exception: {innerException.InnerException.GetType().FullName}");
                    System.Diagnostics.Debug.WriteLine($"Nested Inner Message: {innerException.InnerException.Message}");
                }
            }
            System.Diagnostics.Debug.WriteLine("=== END TASK EXCEPTION DETAILS ===");

            var errorService = _host?.Services?.GetService<IErrorHandlingService>();
            if (errorService != null)
            {
                System.Diagnostics.Debug.WriteLine("OnUnobservedTaskException: Using error service to handle exceptions");
                // Handle each exception in the aggregate exception
                foreach (var innerException in e.Exception.InnerExceptions)
                {
                    await errorService.HandleExceptionAsync(innerException, "Task Thread", showDialog: false);
                }
                e.SetObserved(); // Prevent application crash
                System.Diagnostics.Debug.WriteLine("OnUnobservedTaskException: All exceptions handled and observed");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("OnUnobservedTaskException: Error service not available, marking as observed");
                e.SetObserved(); // Still prevent crash even without error service
            }
        }
        catch (Exception handlerException)
        {
            System.Diagnostics.Debug.WriteLine("=== CRITICAL ERROR IN TASK EXCEPTION HANDLER ===");
            System.Diagnostics.Debug.WriteLine($"Handler Exception: {handlerException.GetType().FullName}");
            System.Diagnostics.Debug.WriteLine($"Handler Message: {handlerException.Message}");
            System.Diagnostics.Debug.WriteLine($"Handler StackTrace: {handlerException.StackTrace}");
            System.Diagnostics.Debug.WriteLine("=== END CRITICAL TASK ERROR ===");
        }
    }

    /// <summary>
    /// Validates the enhanced logging configuration for correctness and security
    /// </summary>
    /// <param name="config">The logging configuration to validate</param>
    /// <returns>True if configuration is valid, false otherwise</returns>
    private bool ValidateLoggingConfiguration(GGPKExplorer.Models.EnhancedLoggingConfiguration config)
    {
        try
        {
            LogStartupStep("Validating logging configuration");

            // Validate log file size limits
            if (config.MaxLogFileSizeBytes <= 0 || config.MaxLogFileSizeBytes > 1024 * 1024 * 1024) // Max 1GB
            {
                LogStartupError($"Invalid MaxLogFileSizeBytes: {config.MaxLogFileSizeBytes}. Must be between 1 and 1GB.");
                return false;
            }

            // Validate log file count
            if (config.MaxLogFiles <= 0 || config.MaxLogFiles > 100)
            {
                LogStartupError($"Invalid MaxLogFiles: {config.MaxLogFiles}. Must be between 1 and 100.");
                return false;
            }

            // Validate log level
            if (!Enum.IsDefined(typeof(Microsoft.Extensions.Logging.LogLevel), config.MinimumLogLevel))
            {
                LogStartupError($"Invalid LogLevel: {config.MinimumLogLevel}");
                return false;
            }

            // Validate log directory accessibility
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }

                // Test write access
                var testFile = Path.Combine(_logDirectory, "access_test.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                LogStartupError($"Log directory not accessible: {ex.Message}");
                return false;
            }

            LogStartupStep("Logging configuration validation passed");
            return true;
        }
        catch (Exception ex)
        {
            LogStartupError($"Error validating logging configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Performs logging service health checks during application startup
    /// </summary>
    /// <param name="enhancedLoggingService">The enhanced logging service to test</param>
    /// <returns>True if health checks pass, false otherwise</returns>
    private bool PerformLoggingServiceHealthChecks(GGPKExplorer.Services.IEnhancedLoggingService enhancedLoggingService)
    {
        try
        {
            LogStartupStep("Performing logging service health checks");

            // Test basic logging functionality
            enhancedLoggingService.LogGGPKOperation("HealthCheck", null, new System.Collections.Generic.Dictionary<string, object>
            {
                ["TestType"] = "BasicLogging",
                ["Timestamp"] = DateTime.UtcNow,
                ["HealthCheckId"] = Guid.NewGuid().ToString()
            });

            // Test performance logging
            enhancedLoggingService.LogPerformanceMetric("HealthCheckMetric", 123.45, "ms", 
                new System.Collections.Generic.Dictionary<string, object>
                {
                    ["TestType"] = "PerformanceLogging",
                    ["HealthCheckId"] = Guid.NewGuid().ToString()
                });

            // Test memory logging
            enhancedLoggingService.LogMemoryOperation("HealthCheckMemory", GC.GetTotalMemory(false), "HealthCheck");

            // Test error logging
            enhancedLoggingService.LogEntry(new GGPKExplorer.Models.GGPKLogEntry
            {
                Level = Microsoft.Extensions.Logging.LogLevel.Information,
                Category = "HealthCheck",
                Operation = "LoggingServiceHealthCheck",
                Context = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["Result"] = "Success",
                    ["Timestamp"] = DateTime.UtcNow,
                    ["HealthCheckId"] = Guid.NewGuid().ToString()
                }
            });

            // Test logging scope
            using var scope = enhancedLoggingService.BeginScope("HealthCheckScope", 
                new System.Collections.Generic.Dictionary<string, object>
                {
                    ["ScopeType"] = "HealthCheck",
                    ["HealthCheckId"] = Guid.NewGuid().ToString()
                });

            LogStartupStep("Logging service health checks completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogStartupError($"Logging service health check failed: {ex.Message}");
            return false;
        }
    }
}

