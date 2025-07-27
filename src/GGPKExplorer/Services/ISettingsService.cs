using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for managing application settings and preferences
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Event raised when settings are changed
        /// </summary>
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// Gets the current view settings
        /// </summary>
        ViewSettings ViewSettings { get; }

        /// <summary>
        /// Gets the current window settings
        /// </summary>
        WindowSettings WindowSettings { get; }

        /// <summary>
        /// Gets the list of recently opened files
        /// </summary>
        IReadOnlyList<string> RecentFiles { get; }

        /// <summary>
        /// Gets the maximum number of recent files to keep
        /// </summary>
        int MaxRecentFiles { get; set; }

        /// <summary>
        /// Gets or sets the default search options
        /// </summary>
        SearchOptions DefaultSearchOptions { get; set; }

        /// <summary>
        /// Gets or sets whether to show confirmation dialogs
        /// </summary>
        bool ShowConfirmationDialogs { get; set; }

        /// <summary>
        /// Gets or sets whether to automatically check for updates
        /// </summary>
        bool AutoCheckForUpdates { get; set; }

        /// <summary>
        /// Gets or sets the application theme
        /// </summary>
        ApplicationTheme Theme { get; set; }

        /// <summary>
        /// Gets or sets the log verbosity level
        /// </summary>
        LogVerbosity LogVerbosity { get; set; }

        /// <summary>
        /// Loads settings from storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task LoadSettingsAsync();

        /// <summary>
        /// Saves current settings to storage
        /// </summary>
        /// <returns>Task representing the async operation</returns>
        Task SaveSettingsAsync();

        /// <summary>
        /// Updates view settings
        /// </summary>
        /// <param name="viewSettings">New view settings</param>
        void UpdateViewSettings(ViewSettings viewSettings);

        /// <summary>
        /// Updates window settings
        /// </summary>
        /// <param name="windowSettings">New window settings</param>
        void UpdateWindowSettings(WindowSettings windowSettings);

        /// <summary>
        /// Adds a file to the recent files list
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        void AddRecentFile(string filePath);

        /// <summary>
        /// Removes a file from the recent files list
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        void RemoveRecentFile(string filePath);

        /// <summary>
        /// Clears the recent files list
        /// </summary>
        void ClearRecentFiles();

        /// <summary>
        /// Gets a setting value by key
        /// </summary>
        /// <typeparam name="T">Type of the setting value</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="defaultValue">Default value if setting doesn't exist</param>
        /// <returns>Setting value</returns>
        T GetSetting<T>(string key, T defaultValue = default!);

        /// <summary>
        /// Sets a setting value by key
        /// </summary>
        /// <typeparam name="T">Type of the setting value</typeparam>
        /// <param name="key">Setting key</param>
        /// <param name="value">Setting value</param>
        void SetSetting<T>(string key, T value);

        /// <summary>
        /// Resets all settings to default values
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Exports settings to a file
        /// </summary>
        /// <param name="filePath">Path to export to</param>
        /// <returns>Task representing the async operation</returns>
        Task ExportSettingsAsync(string filePath);

        /// <summary>
        /// Imports settings from a file
        /// </summary>
        /// <param name="filePath">Path to import from</param>
        /// <returns>Task representing the async operation</returns>
        Task ImportSettingsAsync(string filePath);
    }



    /// <summary>
    /// Event arguments for settings changed events
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The setting key that changed
        /// </summary>
        public string SettingKey { get; }

        /// <summary>
        /// The old value
        /// </summary>
        public object? OldValue { get; }

        /// <summary>
        /// The new value
        /// </summary>
        public object? NewValue { get; }

        public SettingsChangedEventArgs(string settingKey, object? oldValue, object? newValue)
        {
            SettingKey = settingKey;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}