using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wpf.Ui;
using Wpf.Ui.Controls;
using GGPKExplorer.Models;
using GGPKExplorer.Services;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.ViewModels;

/// <summary>
/// Main view model for the application window
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IContentDialogService _contentDialogService;
    private readonly ISettingsService _settingsService;
    private readonly IToastService _toastService;

    /// <summary>
    /// Explorer view model for the dual-pane interface
    /// </summary>
    public ExplorerViewModel ExplorerViewModel { get; private set; }

    public MainViewModel(
        IContentDialogService contentDialogService,
        IGGPKService ggpkService,
        IFileOperationsService fileOperationsService,
        ISettingsService settingsService,
        ISearchService searchService,
        IToastService toastService)
    {
        _contentDialogService = contentDialogService;
        _settingsService = settingsService;
        _toastService = toastService;
        
        RecentFiles = new ObservableCollection<string>();
        
        // Initialize explorer view model with proper services
        ExplorerViewModel = new ExplorerViewModel(ggpkService, fileOperationsService, settingsService, searchService);
        
        // Subscribe to explorer view model property changes for progress updates
        ExplorerViewModel.PropertyChanged += OnExplorerViewModelPropertyChanged;
        
        // Load recent files from settings service
        LoadRecentFiles();
        
        // Subscribe to settings changes
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Collection of recently opened GGPK files
    /// </summary>
    public ObservableCollection<string> RecentFiles { get; }

    [ObservableProperty]
    private string _currentFilePath = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private double _progressValue = 0.0;

    [ObservableProperty]
    private bool _isProgressVisible = false;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private bool _isFileLoaded = false;

    /// <summary>
    /// Command to open a GGPK file
    /// </summary>
    [RelayCommand]
    private async Task OpenFileAsync()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Open GGPK File",
            Filter = "GGPK Files (*.ggpk)|*.ggpk|All Files (*.*)|*.*",
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            await LoadGGPKFileAsync(openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Command to open a recent file
    /// </summary>
    [RelayCommand]
    private async Task OpenRecentFileAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            await LoadGGPKFileAsync(filePath);
        }
        else
        {
            _toastService.ShowWarning($"The file '{filePath}' could not be found.", "File Not Found");
            
            // Remove from recent files if it doesn't exist
            RecentFiles.Remove(filePath);
        }
    }



    /// <summary>
    /// Command to exit the application
    /// </summary>
    [RelayCommand]
    private void Exit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>
    /// Command to show logs dialog
    /// </summary>
    [RelayCommand]
    private void ShowLogs()
    {
        System.Diagnostics.Debug.WriteLine("ShowLogs called!");
        
        try
        {
            var logsDialog = new GGPKExplorer.Views.Dialogs.LogsDialog();
            
            // Set the owner to position the dialog relative to the main window
            if (Application.Current?.MainWindow != null)
            {
                logsDialog.Owner = Application.Current.MainWindow;
            }
            
            logsDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error showing logs dialog: {ex.Message}");
            
            // Show error message to user
            System.Windows.MessageBox.Show(
                $"Error opening logs dialog: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private bool _isSettingsDialogOpen = false;

    /// <summary>
    /// Command to show settings dialog
    /// </summary>
    [RelayCommand]
    private void ShowSettings()
    {
        // Prevent multiple settings dialogs from opening
        if (_isSettingsDialogOpen)
        {
            System.Diagnostics.Debug.WriteLine("ShowSettings: Settings dialog is already open, ignoring request");
            return;
        }
        try
        {
            System.Diagnostics.Debug.WriteLine("ShowSettings: Starting settings dialog creation");
            Console.WriteLine("ShowSettings: Starting settings dialog creation");
            
            if (_settingsService == null)
            {
                System.Diagnostics.Debug.WriteLine("ShowSettings: ERROR - SettingsService is null");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("ShowSettings: SettingsService is available, creating SettingsDialogViewModel");

            // Check SettingsService properties before creating ViewModel
            try
            {
                System.Diagnostics.Debug.WriteLine($"ShowSettings: SettingsService.ViewSettings = {_settingsService.ViewSettings}");
                System.Diagnostics.Debug.WriteLine($"ShowSettings: SettingsService.WindowSettings = {_settingsService.WindowSettings}");
                System.Diagnostics.Debug.WriteLine($"ShowSettings: SettingsService.RecentFiles count = {_settingsService.RecentFiles?.Count ?? -1}");
            }
            catch (Exception propEx)
            {
                System.Diagnostics.Debug.WriteLine($"ShowSettings: ERROR accessing SettingsService properties: {propEx.Message}");
                System.Diagnostics.Debug.WriteLine($"ShowSettings: StackTrace: {propEx.StackTrace}");
            }

            System.Diagnostics.Debug.WriteLine("ShowSettings: Creating SettingsDialogViewModel");
            var settingsViewModel = new SettingsDialogViewModel(_settingsService);
            
            if (settingsViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("ShowSettings: ERROR - SettingsDialogViewModel is null after creation");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("ShowSettings: SettingsDialogViewModel created successfully");
            System.Diagnostics.Debug.WriteLine("ShowSettings: Creating SettingsDialog");
            
            var settingsDialog = new Views.Dialogs.SettingsDialog(settingsViewModel);
            
            if (settingsDialog == null)
            {
                System.Diagnostics.Debug.WriteLine("ShowSettings: ERROR - SettingsDialog is null after creation");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("ShowSettings: SettingsDialog created successfully");
            System.Diagnostics.Debug.WriteLine("ShowSettings: Setting dialog owner");
            
            if (Application.Current?.MainWindow != null)
            {
                settingsDialog.Owner = Application.Current.MainWindow;
                System.Diagnostics.Debug.WriteLine("ShowSettings: Dialog owner set successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ShowSettings: WARNING - MainWindow is null, dialog will have no owner");
            }

            _isSettingsDialogOpen = true;
            System.Diagnostics.Debug.WriteLine("ShowSettings: Showing dialog");
            
            try
            {
                // Temporarily unsubscribe from settings changes to prevent dialog reopening
                _settingsService.SettingsChanged -= OnSettingsChanged;
                
                var result = settingsDialog.ShowDialog();
                System.Diagnostics.Debug.WriteLine($"ShowSettings: Dialog result: {result}");
                
                if (result == true)
                {
                    System.Diagnostics.Debug.WriteLine("ShowSettings: Dialog closed with OK");
                    // Don't show success notification for OK - user just closed dialog
                }
            }
            finally
            {
                // Re-subscribe to settings changes
                _settingsService.SettingsChanged += OnSettingsChanged;
                
                _isSettingsDialogOpen = false;
                System.Diagnostics.Debug.WriteLine("ShowSettings: Settings dialog closed, flag reset");
            }
        }
        catch (InvalidOperationException ioEx)
        {
            System.Diagnostics.Debug.WriteLine($"ShowSettings: InvalidOperationException: {ioEx.Message}");
            System.Diagnostics.Debug.WriteLine($"ShowSettings: StackTrace: {ioEx.StackTrace}");
            // Handle STA thread requirement during testing
            // In tests, we can't create WPF UI components
        }
        catch (NullReferenceException nullEx)
        {
            System.Diagnostics.Debug.WriteLine($"ShowSettings: NullReferenceException: {nullEx.Message}");
            System.Diagnostics.Debug.WriteLine($"ShowSettings: StackTrace: {nullEx.StackTrace}");
            
            _toastService.ShowError($"Settings dialog failed to open: {nullEx.Message}", "Error");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowSettings: General Exception: {ex.GetType().Name}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"ShowSettings: StackTrace: {ex.StackTrace}");
            
            _toastService.ShowError($"Failed to open settings dialog: {ex.Message}", "Error");
        }
        finally
        {
            _isSettingsDialogOpen = false;
            System.Diagnostics.Debug.WriteLine("ShowSettings: Exception occurred, settings dialog flag reset");
        }
    }

    /// <summary>
    /// Loads a GGPK file asynchronously
    /// </summary>
    private async Task LoadGGPKFileAsync(string filePath)
    {
        try
        {
            IsLoading = true;
            IsProgressVisible = true;
            ProgressValue = 0.0;
            ProgressText = "Loading GGPK file...";
            StatusText = $"Loading {Path.GetFileName(filePath)}...";

            // Actually load the GGPK file using the service
            var success = await ExplorerViewModel.LoadGGPKAsync(filePath);
            
            if (success)
            {
                CurrentFilePath = filePath;
                StatusText = $"Loaded: {Path.GetFileName(filePath)}";
                IsFileLoaded = true;
                
                // Add to recent files using settings service
                _settingsService.AddRecentFile(filePath);
                LoadRecentFiles(); // Refresh the UI collection

                _toastService.ShowSuccess($"Successfully loaded {Path.GetFileName(filePath)}", "File Loaded");
            }
            else
            {
                StatusText = "Failed to load file";
                _toastService.ShowError($"Failed to load {Path.GetFileName(filePath)}", "Load Failed");
            }
        }
        catch (Exception ex)
        {
            StatusText = "Error loading file";
            
            // Temporary fix: Use MessageBox instead of ContentDialog until WPF-UI dialog service is properly configured
            System.Windows.MessageBox.Show(
                $"Failed to load the GGPK file:\n\n{ex.Message}",
                "Error Loading GGPK File",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
            IsProgressVisible = false;
            ProgressValue = 0.0;
            ProgressText = string.Empty;
        }
    }

    /// <summary>
    /// Handles property changes from the ExplorerViewModel
    /// </summary>
    private void OnExplorerViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExplorerViewModel.IsLoading))
        {
            IsLoading = ExplorerViewModel.IsLoading;
            IsProgressVisible = ExplorerViewModel.IsLoading;
        }
        else if (e.PropertyName == nameof(ExplorerViewModel.StatusText))
        {
            StatusText = ExplorerViewModel.StatusText;
        }
    }

    /// <summary>
    /// Loads recent files from settings service
    /// </summary>
    private void LoadRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var file in _settingsService.RecentFiles)
        {
            RecentFiles.Add(file);
        }
    }

    /// <summary>
    /// Handles settings changes
    /// </summary>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.SettingKey == "RecentFiles")
        {
            LoadRecentFiles();
        }
    }


}