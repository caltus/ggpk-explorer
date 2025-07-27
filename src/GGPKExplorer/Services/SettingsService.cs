using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Shell;
using GGPKExplorer.Models;
using GGPKExplorer.Properties;
using GGPKExplorer.Services;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Enhanced settings service using Properties.Settings for persistent preferences
    /// Supports view mode, column, window layout persistence and recent files with JumpList integration
    /// </summary>
    public class SettingsService : ISettingsService
    {
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public ViewSettings ViewSettings { get; private set; } = new();
        public WindowSettings WindowSettings { get; private set; } = new();
        public IReadOnlyList<string> RecentFiles => _recentFiles.AsReadOnly();
        
        private readonly List<string> _recentFiles = new();

        public int MaxRecentFiles 
        { 
            get => Settings.Default.MaxRecentFiles;
            set 
            { 
                Settings.Default.MaxRecentFiles = value;
                Settings.Default.Save();
            }
        }

        public SearchOptions DefaultSearchOptions { get; set; } = new();
        
        public bool ShowConfirmationDialogs 
        { 
            get => Settings.Default.ShowConfirmationDialogs;
            set 
            { 
                Settings.Default.ShowConfirmationDialogs = value;
                Settings.Default.Save();
            }
        }
        
        public bool AutoCheckForUpdates 
        { 
            get => Settings.Default.AutoCheckForUpdates;
            set 
            { 
                Settings.Default.AutoCheckForUpdates = value;
                Settings.Default.Save();
            }
        }
        
        public ApplicationTheme Theme 
        { 
            get => (ApplicationTheme)Settings.Default.Theme;
            set 
            { 
                Settings.Default.Theme = (int)value;
                Settings.Default.Save();
            }
        }

        public LogVerbosity LogVerbosity 
        { 
            get => (LogVerbosity)Settings.Default.LogVerbosity;
            set 
            { 
                Settings.Default.LogVerbosity = (int)value;
                Settings.Default.Save();
            }
        }

        public SettingsService()
        {
            try
            {
                // Initialize settings from Properties.Settings
                LoadSettingsFromProperties();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SettingsService constructor: {ex}");
                // Initialize with safe defaults
                InitializeWithDefaults();
            }
        }

        public async Task LoadSettingsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Reload settings from storage
                    Settings.Default.Reload();
                    
                    // Load specific settings objects
                    LoadViewSettings();
                    LoadWindowSettings();
                    LoadRecentFiles();
                    LoadDefaultSearchOptions();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    // Use defaults on error
                }
            });
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: Starting to save settings");
                    
                    // Save specific settings objects
                    SaveViewSettings();
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: ViewSettings saved");
                    
                    SaveWindowSettings();
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: WindowSettings saved");
                    
                    SaveRecentFiles();
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: RecentFiles saved");
                    
                    SaveDefaultSearchOptions();
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: DefaultSearchOptions saved");

                    // Save all settings to storage
                    Settings.Default.Save();
                    System.Diagnostics.Debug.WriteLine("SaveSettingsAsync: Settings.Default.Save() completed");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SaveSettingsAsync: Error saving settings: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"SaveSettingsAsync: StackTrace: {ex.StackTrace}");
                    
                    // Re-throw the exception so calling code can handle it
                    throw;
                }
            });
        }

        public void UpdateViewSettings(ViewSettings viewSettings)
        {
            var oldSettings = ViewSettings;
            ViewSettings = viewSettings ?? throw new ArgumentNullException(nameof(viewSettings));
            SaveViewSettings();
            Settings.Default.Save();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("ViewSettings", oldSettings, ViewSettings));
        }

        public void UpdateWindowSettings(WindowSettings windowSettings)
        {
            var oldSettings = WindowSettings;
            WindowSettings = windowSettings ?? throw new ArgumentNullException(nameof(windowSettings));
            SaveWindowSettings();
            Settings.Default.Save();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("WindowSettings", oldSettings, WindowSettings));
        }

        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            _recentFiles.Remove(filePath);
            _recentFiles.Insert(0, filePath);

            while (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }

            SaveRecentFiles();
            Settings.Default.Save();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _recentFiles));
        }

        public void RemoveRecentFile(string filePath)
        {
            if (_recentFiles.Remove(filePath))
            {
                SaveRecentFiles();
                Settings.Default.Save();
                UpdateJumpList();
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _recentFiles));
            }
        }

        public void ClearRecentFiles()
        {
            _recentFiles.Clear();
            SaveRecentFiles();
            Settings.Default.Save();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _recentFiles));
        }

        public T GetSetting<T>(string key, T defaultValue = default!)
        {
            try
            {
                var property = Settings.Default.Properties[key];
                if (property != null)
                {
                    var value = Settings.Default[key];
                    if (value != null && value is T directValue)
                    {
                        return directValue;
                    }
                    else if (value != null)
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting setting {key}: {ex.Message}");
            }
            return defaultValue;
        }

        public void SetSetting<T>(string key, T value)
        {
            try
            {
                var oldValue = Settings.Default[key];
                Settings.Default[key] = value;
                Settings.Default.Save();
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(key, oldValue, value));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting {key}: {ex.Message}");
            }
        }

        public void ResetToDefaults()
        {
            Settings.Default.Reset();
            ViewSettings = new ViewSettings();
            WindowSettings = new WindowSettings();
            _recentFiles.Clear();
            DefaultSearchOptions = new SearchOptions();
            
            Settings.Default.Save();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("Reset", null, null));
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Export settings to a custom format (could be XML or JSON)
                    var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
                    if (config.HasFile)
                    {
                        File.Copy(config.FilePath, filePath, true);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error exporting settings: {ex.Message}");
                }
            });
        }

        public async Task ImportSettingsAsync(string filePath)
        {
            await Task.Run(async () =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal);
                        if (config.HasFile)
                        {
                            File.Copy(filePath, config.FilePath, true);
                            await LoadSettingsAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error importing settings: {ex.Message}");
                }
            });
        }

        private void LoadSettingsFromProperties()
        {
            try
            {
                LoadViewSettings();
                LoadWindowSettings();
                LoadRecentFiles();
                LoadDefaultSearchOptions();
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Configuration error in LoadSettingsFromProperties: {ex}");
                // Configuration system failed (likely during testing)
                // Initialize with default values
                InitializeWithDefaults();
            }
            catch (System.Xml.XmlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"XML error in LoadSettingsFromProperties: {ex}");
                // XML configuration is corrupted (likely during testing)
                // Initialize with default values
                InitializeWithDefaults();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Unexpected error in LoadSettingsFromProperties: {ex}");
                // Initialize with default values for any other error
                InitializeWithDefaults();
            }
        }

        private void InitializeWithDefaults()
        {
            ViewSettings = new ViewSettings
            {
                ViewMode = ViewMode.Details,
                ShowHiddenFiles = false,
                ShowFileExtensions = true,
                ShowCompressionInfo = true,
                ShowFileHashes = false,
                SortSettings = new SortSettings
                {
                    SortColumn = "Name",
                    SortDirection = System.ComponentModel.ListSortDirection.Ascending
                },
                ColumnSettings = new ColumnSettings()
            };

            WindowSettings = new WindowSettings
            {
                Width = 1200,
                Height = 800,
                Left = 100,
                Top = 100,
                IsMaximized = false,
                SplitterPosition = 300,
                ShowStatusBar = true,
                ShowToolbar = true,
                RememberWindowPosition = true
            };

            // Initialize the backing field for RecentFiles
            _recentFiles.Clear();

            DefaultSearchOptions = new SearchOptions
            {
                Query = "",
                UseRegex = false,
                MatchCase = false,
                TypeFilter = new FileTypeFilter(),
                MaxResults = 1000
            };
        }

        private void LoadViewSettings()
        {
            ViewSettings = new ViewSettings
            {
                ViewMode = (ViewMode)Settings.Default.ViewMode,
                ShowHiddenFiles = Settings.Default.ShowHiddenFiles,
                ShowFileExtensions = Settings.Default.ShowFileExtensions,
                ShowCompressionInfo = Settings.Default.ShowCompressionInfo,
                ShowFileHashes = Settings.Default.ShowFileHashes,
                ShowFileOffsets = Settings.Default.ShowFileOffsets,
                SortSettings = new SortSettings
                {
                    SortColumn = Settings.Default.SortColumn ?? "Name",
                    SortDirection = (ListSortDirection)Settings.Default.SortDirection,
                    DirectoriesFirst = Settings.Default.DirectoriesFirst
                },
                ColumnSettings = new ColumnSettings
                {
                    NameColumnWidth = Settings.Default.NameColumnWidth,
                    SizeColumnWidth = Settings.Default.SizeColumnWidth,
                    TypeColumnWidth = Settings.Default.TypeColumnWidth,
                    ModifiedColumnWidth = Settings.Default.ModifiedColumnWidth,
                    CompressionColumnWidth = Settings.Default.CompressionColumnWidth,
                    ShowNameColumn = Settings.Default.ShowNameColumn,
                    ShowSizeColumn = Settings.Default.ShowSizeColumn,
                    ShowTypeColumn = Settings.Default.ShowTypeColumn,
                    ShowModifiedColumn = Settings.Default.ShowModifiedColumn,
                    ShowCompressionColumn = Settings.Default.ShowCompressionColumn
                }
            };
        }

        private void SaveViewSettings()
        {
            Settings.Default.ViewMode = (int)ViewSettings.ViewMode;
            Settings.Default.ShowHiddenFiles = ViewSettings.ShowHiddenFiles;
            Settings.Default.ShowFileExtensions = ViewSettings.ShowFileExtensions;
            Settings.Default.ShowCompressionInfo = ViewSettings.ShowCompressionInfo;
            Settings.Default.ShowFileHashes = ViewSettings.ShowFileHashes;
            Settings.Default.ShowFileOffsets = ViewSettings.ShowFileOffsets;
            
            Settings.Default.SortColumn = ViewSettings.SortSettings.SortColumn;
            Settings.Default.SortDirection = (int)ViewSettings.SortSettings.SortDirection;
            Settings.Default.DirectoriesFirst = ViewSettings.SortSettings.DirectoriesFirst;
            
            Settings.Default.NameColumnWidth = ViewSettings.ColumnSettings.NameColumnWidth;
            Settings.Default.SizeColumnWidth = ViewSettings.ColumnSettings.SizeColumnWidth;
            Settings.Default.TypeColumnWidth = ViewSettings.ColumnSettings.TypeColumnWidth;
            Settings.Default.ModifiedColumnWidth = ViewSettings.ColumnSettings.ModifiedColumnWidth;
            Settings.Default.CompressionColumnWidth = ViewSettings.ColumnSettings.CompressionColumnWidth;
            Settings.Default.ShowNameColumn = ViewSettings.ColumnSettings.ShowNameColumn;
            Settings.Default.ShowSizeColumn = ViewSettings.ColumnSettings.ShowSizeColumn;
            Settings.Default.ShowTypeColumn = ViewSettings.ColumnSettings.ShowTypeColumn;
            Settings.Default.ShowModifiedColumn = ViewSettings.ColumnSettings.ShowModifiedColumn;
            Settings.Default.ShowCompressionColumn = ViewSettings.ColumnSettings.ShowCompressionColumn;
        }

        private void LoadWindowSettings()
        {
            WindowSettings = new WindowSettings
            {
                Width = Settings.Default.WindowWidth,
                Height = Settings.Default.WindowHeight,
                Left = Settings.Default.WindowLeft,
                Top = Settings.Default.WindowTop,
                IsMaximized = Settings.Default.WindowMaximized,
                SplitterPosition = Settings.Default.SplitterPosition,
                ShowStatusBar = Settings.Default.ShowStatusBar,
                ShowToolbar = Settings.Default.ShowToolbar,
                RememberWindowPosition = Settings.Default.RememberWindowPosition
            };
        }

        private void SaveWindowSettings()
        {
            Settings.Default.WindowWidth = WindowSettings.Width;
            Settings.Default.WindowHeight = WindowSettings.Height;
            Settings.Default.WindowLeft = WindowSettings.Left;
            Settings.Default.WindowTop = WindowSettings.Top;
            Settings.Default.WindowMaximized = WindowSettings.IsMaximized;
            Settings.Default.SplitterPosition = WindowSettings.SplitterPosition;
            Settings.Default.ShowStatusBar = WindowSettings.ShowStatusBar;
            Settings.Default.ShowToolbar = WindowSettings.ShowToolbar;
            Settings.Default.RememberWindowPosition = WindowSettings.RememberWindowPosition;
        }

        private void LoadRecentFiles()
        {
            _recentFiles.Clear();
            if (Settings.Default.RecentFiles != null)
            {
                foreach (string? file in Settings.Default.RecentFiles)
                {
                    if (!string.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        _recentFiles.Add(file);
                    }
                }
            }
        }

        private void SaveRecentFiles()
        {
            if (Settings.Default.RecentFiles == null)
            {
                Settings.Default.RecentFiles = new StringCollection();
            }
            
            Settings.Default.RecentFiles.Clear();
            foreach (var file in _recentFiles)
            {
                Settings.Default.RecentFiles.Add(file);
            }
        }

        private void LoadDefaultSearchOptions()
        {
            DefaultSearchOptions = new SearchOptions
            {
                MatchCase = Settings.Default.DefaultSearchMatchCase,
                UseRegex = Settings.Default.DefaultSearchUseRegex
            };
        }

        private void SaveDefaultSearchOptions()
        {
            Settings.Default.DefaultSearchMatchCase = DefaultSearchOptions.MatchCase;
            Settings.Default.DefaultSearchUseRegex = DefaultSearchOptions.UseRegex;
        }

        private void UpdateJumpList()
        {
            try
            {
                var jumpList = new JumpList();
                jumpList.ShowFrequentCategory = false;
                jumpList.ShowRecentCategory = false;

                // Add recent files to jump list
                foreach (var filePath in _recentFiles.Take(10))
                {
                    if (File.Exists(filePath))
                    {
                        var jumpTask = new JumpTask
                        {
                            Title = Path.GetFileName(filePath),
                            Description = filePath,
                            ApplicationPath = System.Reflection.Assembly.GetExecutingAssembly().Location,
                            Arguments = $"\"{filePath}\"",
                            CustomCategory = "Recent Files"
                        };
                        jumpList.JumpItems.Add(jumpTask);
                    }
                }

                JumpList.SetJumpList(System.Windows.Application.Current, jumpList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating jump list: {ex.Message}");
            }
        }
    }
}