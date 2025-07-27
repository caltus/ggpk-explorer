using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Shell;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Portable settings service that saves settings in the application directory
    /// for true portability without relying on Windows user profile
    /// </summary>
    public class PortableSettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private readonly string _settingsDirectory;
        private PortableSettings _settings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public ViewSettings ViewSettings { get; private set; } = new();
        public WindowSettings WindowSettings { get; private set; } = new();
        public IReadOnlyList<string> RecentFiles => _settings.RecentFiles.AsReadOnly();
        
        public int MaxRecentFiles 
        { 
            get => _settings.MaxRecentFiles;
            set 
            { 
                _settings.MaxRecentFiles = value;
                SaveSettingsInternal();
            }
        }

        public SearchOptions DefaultSearchOptions 
        { 
            get => _settings.DefaultSearchOptions;
            set 
            { 
                _settings.DefaultSearchOptions = value ?? new SearchOptions();
                SaveSettingsInternal();
            }
        }
        
        public bool ShowConfirmationDialogs 
        { 
            get => _settings.ShowConfirmationDialogs;
            set 
            { 
                _settings.ShowConfirmationDialogs = value;
                SaveSettingsInternal();
            }
        }
        
        public bool AutoCheckForUpdates 
        { 
            get => _settings.AutoCheckForUpdates;
            set 
            { 
                _settings.AutoCheckForUpdates = value;
                SaveSettingsInternal();
            }
        }
        
        public ApplicationTheme Theme 
        { 
            get => _settings.Theme;
            set 
            { 
                _settings.Theme = value;
                SaveSettingsInternal();
            }
        }
        
        public LogVerbosity LogVerbosity 
        { 
            get => _settings.LogVerbosity;
            set 
            { 
                _settings.LogVerbosity = value;
                SaveSettingsInternal();
            }
        }

        public PortableSettingsService()
        {
            // Save settings directly in the application directory for portability
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _settingsDirectory = appDirectory;
            _settingsFilePath = Path.Combine(appDirectory, "settings.json");
            
            // Initialize with defaults
            _settings = new PortableSettings();
            
            // Migrate from old settings location if needed
            MigrateOldSettingsIfNeeded(appDirectory);
            
            // Load existing settings
            LoadSettingsFromFile();
            UpdateViewModelSettings();
        }

        public async Task LoadSettingsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    LoadSettingsFromFile();
                    UpdateViewModelSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading portable settings: {ex.Message}");
                    // Use defaults on error
                    _settings = new PortableSettings();
                    UpdateViewModelSettings();
                }
            });
        }

        public async Task SaveSettingsAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    SaveSettingsInternal();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving portable settings: {ex.Message}");
                    throw;
                }
            });
        }

        public void UpdateViewSettings(ViewSettings viewSettings)
        {
            var oldSettings = ViewSettings;
            ViewSettings = viewSettings ?? throw new ArgumentNullException(nameof(viewSettings));
            
            // Store directly in settings object
            _settings.ViewSettings = ViewSettings;
            
            SaveSettingsInternal();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("ViewSettings", oldSettings, ViewSettings));
        }

        public void UpdateWindowSettings(WindowSettings windowSettings)
        {
            var oldSettings = WindowSettings;
            WindowSettings = windowSettings ?? throw new ArgumentNullException(nameof(windowSettings));
            
            // Store directly in settings object
            _settings.WindowSettings = WindowSettings;
            
            SaveSettingsInternal();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("WindowSettings", oldSettings, WindowSettings));
        }

        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            _settings.RecentFiles.Remove(filePath);
            _settings.RecentFiles.Insert(0, filePath);

            while (_settings.RecentFiles.Count > MaxRecentFiles)
            {
                _settings.RecentFiles.RemoveAt(_settings.RecentFiles.Count - 1);
            }

            SaveSettingsInternal();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _settings.RecentFiles));
        }

        public void RemoveRecentFile(string filePath)
        {
            if (_settings.RecentFiles.Remove(filePath))
            {
                SaveSettingsInternal();
                UpdateJumpList();
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _settings.RecentFiles));
            }
        }

        public void ClearRecentFiles()
        {
            _settings.RecentFiles.Clear();
            SaveSettingsInternal();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("RecentFiles", null, _settings.RecentFiles));
        }

        public T GetSetting<T>(string key, T defaultValue = default!)
        {
            try
            {
                if (_settings.CustomSettings.TryGetValue(key, out var value))
                {
                    if (value is JsonElement jsonElement)
                    {
                        return JsonSerializer.Deserialize<T>(jsonElement.GetRawText()) ?? defaultValue;
                    }
                    else if (value is T directValue)
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
                var oldValue = _settings.CustomSettings.TryGetValue(key, out var old) ? old : (object?)null;
                _settings.CustomSettings[key] = value as object ?? DBNull.Value;
                SaveSettingsInternal();
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(key, oldValue, value));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting {key}: {ex.Message}");
            }
        }

        public void ResetToDefaults()
        {
            _settings = new PortableSettings();
            ViewSettings = new ViewSettings();
            WindowSettings = new WindowSettings();
            DefaultSearchOptions = new SearchOptions();
            
            SaveSettingsInternal();
            UpdateJumpList();
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs("Reset", null, null));
        }

        public async Task ExportSettingsAsync(string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(filePath, json);
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
                        var json = await File.ReadAllTextAsync(filePath);
                        var importedSettings = JsonSerializer.Deserialize<PortableSettings>(json);
                        if (importedSettings != null)
                        {
                            _settings = importedSettings;
                            UpdateViewModelSettings();
                            SaveSettingsInternal();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error importing settings: {ex.Message}");
                }
            });
        }

        private void LoadSettingsFromFile()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    var loadedSettings = JsonSerializer.Deserialize<PortableSettings>(json);
                    if (loadedSettings != null)
                    {
                        _settings = loadedSettings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings from file: {ex.Message}");
                // Use defaults on error
                _settings = new PortableSettings();
            }
        }

        private void SaveSettingsInternal()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings to file: {ex.Message}");
            }
        }

        private void UpdateViewModelSettings()
        {
            // Settings are now stored directly in the proper objects, just assign them
            ViewSettings = _settings.ViewSettings ?? new ViewSettings();
            WindowSettings = _settings.WindowSettings ?? new WindowSettings();
            DefaultSearchOptions = _settings.DefaultSearchOptions ?? new SearchOptions();
        }

        /// <summary>
        /// Migrates settings from the old settings/settings.json location to the new settings.json location
        /// </summary>
        private void MigrateOldSettingsIfNeeded(string appDirectory)
        {
            try
            {
                var oldSettingsPath = Path.Combine(appDirectory, "settings", "settings.json");
                var newSettingsPath = _settingsFilePath;
                
                // If old settings exist and new settings don't exist, migrate
                if (File.Exists(oldSettingsPath) && !File.Exists(newSettingsPath))
                {
                    System.Diagnostics.Debug.WriteLine("Migrating settings from old location to new location");
                    File.Copy(oldSettingsPath, newSettingsPath);
                    
                    // Clean up old settings directory if it's empty
                    var oldSettingsDir = Path.Combine(appDirectory, "settings");
                    if (Directory.Exists(oldSettingsDir))
                    {
                        try
                        {
                            Directory.Delete(oldSettingsDir, true);
                            System.Diagnostics.Debug.WriteLine("Removed old settings directory");
                        }
                        catch
                        {
                            // Ignore cleanup errors
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during settings migration: {ex.Message}");
                // Continue with default settings if migration fails
            }
        }

        private void UpdateJumpList()
        {
            try
            {
                var jumpList = new JumpList();
                jumpList.ShowFrequentCategory = false;
                jumpList.ShowRecentCategory = false;

                // Add recent files to jump list
                foreach (var filePath in _settings.RecentFiles.Take(10))
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

    /// <summary>
    /// Portable settings data structure for JSON serialization
    /// Contains only core application settings - complex settings are stored in separate objects
    /// </summary>
    public class PortableSettings
    {
        // Core application settings
        public List<string> RecentFiles { get; set; } = new();
        public int MaxRecentFiles { get; set; } = 10;
        public bool ShowConfirmationDialogs { get; set; } = true;
        public bool AutoCheckForUpdates { get; set; } = true;
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.System;
        
        // Logging settings
        public LogVerbosity LogVerbosity { get; set; } = LogVerbosity.Information;

        // Complex settings stored as JSON objects to avoid duplication
        public ViewSettings ViewSettings { get; set; } = new();
        public WindowSettings WindowSettings { get; set; } = new();
        public SearchOptions DefaultSearchOptions { get; set; } = new();

        // Custom settings dictionary for extensibility
        public Dictionary<string, object> CustomSettings { get; set; } = new();
    }

    /// <summary>
    /// Log verbosity levels for application logging
    /// </summary>
    public enum LogVerbosity
    {
        /// <summary>
        /// Only critical errors and fatal issues
        /// </summary>
        Critical = 0,
        
        /// <summary>
        /// Errors and critical issues
        /// </summary>
        Error = 1,
        
        /// <summary>
        /// Warnings, errors, and critical issues
        /// </summary>
        Warning = 2,
        
        /// <summary>
        /// General information, warnings, errors, and critical issues
        /// </summary>
        Information = 3,
        
        /// <summary>
        /// Debug information and all above levels
        /// </summary>
        Debug = 4,
        
        /// <summary>
        /// Verbose tracing and all above levels
        /// </summary>
        Trace = 5
    }
}