using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the sidebar navigation
    /// </summary>
    public partial class SidebarViewModel : ObservableObject
    {
        private readonly ICommand? _externalSettingsCommand;
        private readonly ICommand? _externalLogsCommand;

        public SidebarViewModel(ICommand? settingsCommand = null, ICommand? logsCommand = null)
        {
            _externalSettingsCommand = settingsCommand;
            _externalLogsCommand = logsCommand;
        }

        [ObservableProperty]
        private string _selectedView = "Patcher";

        /// <summary>
        /// Command to show the game patcher view
        /// </summary>
        [RelayCommand]
        private void ShowPatcher()
        {
            SelectedView = "Patcher";
            // TODO: Implement navigation to patcher view
        }

        /// <summary>
        /// Command to show the logs view
        /// </summary>
        [RelayCommand]
        private void ShowLogs()
        {
            SelectedView = "Logs";
            
            // Execute the external logs command if available
            if (_externalLogsCommand?.CanExecute(null) == true)
            {
                _externalLogsCommand.Execute(null);
            }
        }

        /// <summary>
        /// Command to show the settings view
        /// </summary>
        [RelayCommand]
        private void ShowSettings()
        {
            SelectedView = "Settings";
            
            // Execute the external settings command if available
            if (_externalSettingsCommand?.CanExecute(null) == true)
            {
                _externalSettingsCommand.Execute(null);
            }
        }
    }
}