using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings Dialog using WPF-UI NavigationView with Card controls
    /// </summary>
    public partial class SettingsDialogViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;

        [ObservableProperty]
        private int selectedNavigationIndex = 0;

        [ObservableProperty]
        private ViewSettings viewSettings = new();

        [ObservableProperty]
        private WindowSettings windowSettings = new();

        [ObservableProperty]
        private ApplicationTheme selectedTheme;

        [ObservableProperty]
        private bool showConfirmationDialogs;

        [ObservableProperty]
        private bool autoCheckForUpdates;

        [ObservableProperty]
        private int maxRecentFiles;

        [ObservableProperty]
        private SearchOptions defaultSearchOptions = new();

        [ObservableProperty]
        private bool hasUnsavedChanges;

        [ObservableProperty]
        private bool enableDebugConsole;

        [ObservableProperty]
        private LogVerbosity logVerbosity;

        public ObservableCollection<string> RecentFiles { get; }
        public ObservableCollection<NavigationItem> NavigationItems { get; }

        public SettingsDialogViewModel(ISettingsService settingsService)
        {
            System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Constructor starting");
            
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: SettingsService assigned");
            
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Creating RecentFiles collection");
                var recentFiles = _settingsService.RecentFiles;
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: RecentFiles from service: {recentFiles?.Count ?? -1} items");
                RecentFiles = new ObservableCollection<string>(recentFiles ?? new List<string>());
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: RecentFiles collection created with {RecentFiles.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: ERROR creating RecentFiles: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: StackTrace: {ex.StackTrace}");
                RecentFiles = new ObservableCollection<string>();
            }
            
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Creating NavigationItems");
                NavigationItems = new ObservableCollection<NavigationItem>
                {
                    new NavigationItem { Title = "General", Icon = "Settings", Tag = "general" },
                    new NavigationItem { Title = "Recent Files", Icon = "History", Tag = "recent" },
                    new NavigationItem { Title = "Logs", Icon = "Document", Tag = "logs" },
                    new NavigationItem { Title = "About", Icon = "Info", Tag = "about" }
                };
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: NavigationItems created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: ERROR creating NavigationItems: {ex.Message}");
                NavigationItems = new ObservableCollection<NavigationItem>();
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Loading current settings");
                LoadCurrentSettings();
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Settings loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: ERROR loading settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: StackTrace: {ex.StackTrace}");
                throw; // Re-throw to surface the actual error
            }
            
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Subscribing to property changes");
                // Subscribe to property changes to track unsaved changes
                PropertyChanged += OnPropertyChanged;
                
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: Theme loaded: {SelectedTheme}");
                System.Diagnostics.Debug.WriteLine("SettingsDialogViewModel: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialogViewModel: ERROR subscribing to property changes: {ex.Message}");
                throw;
            }
        }

        private void LoadCurrentSettings()
        {
            System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Starting to load settings");
            
            _isLoadingSettings = true;
            
            try
            {
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Getting ViewSettings from service");
                var serviceViewSettings = _settingsService.ViewSettings ?? new ViewSettings();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ViewSettings retrieved: {serviceViewSettings != null}");
                
                var serviceSortSettings = serviceViewSettings?.SortSettings ?? new SortSettings();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: SortSettings retrieved: {serviceSortSettings != null}");
                
                var serviceColumnSettings = serviceViewSettings?.ColumnSettings ?? new ColumnSettings();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ColumnSettings retrieved: {serviceColumnSettings != null}");

                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Creating ViewSettings object");
                ViewSettings = new ViewSettings
                {
                    ViewMode = serviceViewSettings?.ViewMode ?? ViewMode.Details,
                    ShowHiddenFiles = serviceViewSettings?.ShowHiddenFiles ?? false,
                    ShowFileExtensions = serviceViewSettings?.ShowFileExtensions ?? true,
                    ShowCompressionInfo = serviceViewSettings?.ShowCompressionInfo ?? false,
                    ShowFileHashes = serviceViewSettings?.ShowFileHashes ?? false,
                    ShowFileOffsets = serviceViewSettings?.ShowFileOffsets ?? false,
                    SortSettings = new SortSettings
                    {
                        SortColumn = serviceSortSettings?.SortColumn ?? "Name",
                        SortDirection = serviceSortSettings?.SortDirection ?? System.ComponentModel.ListSortDirection.Ascending,
                        DirectoriesFirst = serviceSortSettings?.DirectoriesFirst ?? true
                    },
                    ColumnSettings = new ColumnSettings
                    {
                        NameColumnWidth = serviceColumnSettings?.NameColumnWidth ?? 200,
                        SizeColumnWidth = serviceColumnSettings?.SizeColumnWidth ?? 100,
                        TypeColumnWidth = serviceColumnSettings?.TypeColumnWidth ?? 100,
                        ModifiedColumnWidth = serviceColumnSettings?.ModifiedColumnWidth ?? 150,
                        CompressionColumnWidth = serviceColumnSettings?.CompressionColumnWidth ?? 100,
                        ShowNameColumn = serviceColumnSettings?.ShowNameColumn ?? true,
                        ShowSizeColumn = serviceColumnSettings?.ShowSizeColumn ?? true,
                        ShowTypeColumn = serviceColumnSettings?.ShowTypeColumn ?? true,
                        ShowModifiedColumn = serviceColumnSettings?.ShowModifiedColumn ?? true,
                        ShowCompressionColumn = serviceColumnSettings?.ShowCompressionColumn ?? false
                    }
                };
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: ViewSettings created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ERROR creating ViewSettings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: StackTrace: {ex.StackTrace}");
                throw;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Getting WindowSettings from service");
                var serviceWindowSettings = _settingsService.WindowSettings ?? new WindowSettings();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: WindowSettings retrieved: {serviceWindowSettings != null}");
                
                WindowSettings = new WindowSettings
                {
                    Width = serviceWindowSettings?.Width ?? 1200,
                    Height = serviceWindowSettings?.Height ?? 800,
                    Left = serviceWindowSettings?.Left ?? 100,
                    Top = serviceWindowSettings?.Top ?? 100,
                    IsMaximized = serviceWindowSettings?.IsMaximized ?? false,
                    SplitterPosition = serviceWindowSettings?.SplitterPosition ?? 300,
                    ShowStatusBar = serviceWindowSettings?.ShowStatusBar ?? true,
                    ShowToolbar = serviceWindowSettings?.ShowToolbar ?? true,
                    RememberWindowPosition = serviceWindowSettings?.RememberWindowPosition ?? true
                };
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: WindowSettings created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ERROR creating WindowSettings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: StackTrace: {ex.StackTrace}");
                throw;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Getting basic settings from service");
                SelectedTheme = _settingsService.Theme;
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: Theme: {SelectedTheme}");
                
                ShowConfirmationDialogs = _settingsService.ShowConfirmationDialogs;
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ShowConfirmationDialogs: {ShowConfirmationDialogs}");
                
                AutoCheckForUpdates = _settingsService.AutoCheckForUpdates;
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: AutoCheckForUpdates: {AutoCheckForUpdates}");
                
                MaxRecentFiles = _settingsService.MaxRecentFiles;
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: MaxRecentFiles: {MaxRecentFiles}");
                
                EnableDebugConsole = _settingsService.GetSetting("EnableDebugConsole", false);
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: EnableDebugConsole: {EnableDebugConsole}");
                
                LogVerbosity = _settingsService.LogVerbosity;
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: LogVerbosity: {LogVerbosity}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ERROR getting basic settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: StackTrace: {ex.StackTrace}");
                throw;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Getting DefaultSearchOptions from service");
                var serviceSearchOptions = _settingsService.DefaultSearchOptions ?? new SearchOptions();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: DefaultSearchOptions retrieved: {serviceSearchOptions != null}");
                
                DefaultSearchOptions = new SearchOptions
                {
                    MatchCase = serviceSearchOptions?.MatchCase ?? false,
                    UseRegex = serviceSearchOptions?.UseRegex ?? false
                };
                System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: DefaultSearchOptions created successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: ERROR creating DefaultSearchOptions: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LoadCurrentSettings: StackTrace: {ex.StackTrace}");
                _isLoadingSettings = false;
                throw;
            }

            HasUnsavedChanges = false;
            _isLoadingSettings = false;
            System.Diagnostics.Debug.WriteLine("LoadCurrentSettings: Settings loading completed successfully");
        }

        private bool _isLoadingSettings = false;

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Don't mark as unsaved if we're loading settings or if it's an excluded property
            if (_isLoadingSettings || 
                e.PropertyName == nameof(HasUnsavedChanges) || 
                e.PropertyName == nameof(SelectedNavigationIndex))
            {
                System.Diagnostics.Debug.WriteLine($"OnPropertyChanged: Ignoring property '{e.PropertyName}' (loading: {_isLoadingSettings})");
                return;
            }

            // Only mark as unsaved for actual settings properties
            var settingsProperties = new[]
            {
                nameof(SelectedTheme),
                nameof(ShowConfirmationDialogs),
                nameof(AutoCheckForUpdates),
                nameof(MaxRecentFiles),
                nameof(ViewSettings),
                nameof(WindowSettings),
                nameof(DefaultSearchOptions),
                nameof(EnableDebugConsole),
                nameof(LogVerbosity)
            };

            if (settingsProperties.Contains(e.PropertyName))
            {
                System.Diagnostics.Debug.WriteLine($"OnPropertyChanged: Property '{e.PropertyName}' changed, marking as unsaved");
                HasUnsavedChanges = true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"OnPropertyChanged: Ignoring non-settings property '{e.PropertyName}'");
            }
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SaveSettings: Starting to save settings");
                
                // Temporarily disable property change notifications to prevent dialog reopening
                PropertyChanged -= OnPropertyChanged;
                
                try
                {
                    // Update settings - these methods call Settings.Default.Save() but that's okay
                    // since we're doing it in the correct order
                    _settingsService.UpdateViewSettings(ViewSettings);
                    System.Diagnostics.Debug.WriteLine("SaveSettings: ViewSettings updated");
                    
                    _settingsService.UpdateWindowSettings(WindowSettings);
                    System.Diagnostics.Debug.WriteLine("SaveSettings: WindowSettings updated");
                    
                    // Update other settings
                    _settingsService.Theme = SelectedTheme;
                    _settingsService.ShowConfirmationDialogs = ShowConfirmationDialogs;
                    _settingsService.AutoCheckForUpdates = AutoCheckForUpdates;
                    _settingsService.MaxRecentFiles = MaxRecentFiles;
                    _settingsService.DefaultSearchOptions = DefaultSearchOptions;
                    _settingsService.SetSetting("EnableDebugConsole", EnableDebugConsole);
                    _settingsService.LogVerbosity = LogVerbosity;
                    System.Diagnostics.Debug.WriteLine("SaveSettings: Basic settings updated");

                    // Final save to ensure everything is persisted
                    await _settingsService.SaveSettingsAsync();
                    System.Diagnostics.Debug.WriteLine("SaveSettings: Settings saved to storage");
                }
                finally
                {
                    // Re-enable property change notifications
                    PropertyChanged += OnPropertyChanged;
                }
                
                HasUnsavedChanges = false;
                System.Diagnostics.Debug.WriteLine("SaveSettings: Settings save completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveSettings: Error saving settings: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SaveSettings: StackTrace: {ex.StackTrace}");
                
                // Re-throw the exception so the UI can handle it properly
                throw;
            }
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            System.Diagnostics.Debug.WriteLine("ResetToDefaults: Resetting settings to defaults");
            _settingsService.ResetToDefaults();
            LoadCurrentSettings();
        }

        [RelayCommand]
        private void ClearRecentFiles()
        {
            _settingsService.ClearRecentFiles();
            RecentFiles.Clear();
            HasUnsavedChanges = true;
        }

        [RelayCommand]
        private void RemoveRecentFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                _settingsService.RemoveRecentFile(filePath);
                RecentFiles.Remove(filePath);
                HasUnsavedChanges = true;
            }
        }



        [RelayCommand]
        private void Cancel()
        {
            System.Diagnostics.Debug.WriteLine("Cancel: Reloading current settings");
            LoadCurrentSettings();
        }
    }

    /// <summary>
    /// Navigation item for the settings dialog
    /// </summary>
    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}