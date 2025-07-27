using System.Windows;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Accessibility;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// Properties dialog for displaying file and directory metadata
    /// </summary>
    public partial class PropertiesDialog : FluentWindow
    {
        /// <summary>
        /// Gets the view model for the properties dialog
        /// </summary>
        public PropertiesDialogViewModel ViewModel { get; }

        /// <summary>
        /// Initializes a new instance of the PropertiesDialog
        /// </summary>
        /// <param name="viewModel">The view model containing property data</param>
        public PropertiesDialog(PropertiesDialogViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
            SetupAccessibility();
            
            // Set the owner to the main window if available
            if (Application.Current?.MainWindow != null)
            {
                Owner = Application.Current.MainWindow;
            }
        }

        /// <summary>
        /// Sets up accessibility features for the properties dialog
        /// </summary>
        private void SetupAccessibility()
        {
            // Set up high contrast support
            AccessibilityHelper.SetupHighContrastSupport(this);
            
            // Announce dialog opening
            Loaded += (s, e) => AccessibilityHelper.AnnounceToScreenReader("Properties dialog opened");
        }

        /// <summary>
        /// Handles OK button click
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Shows the properties dialog for the specified file or directory
        /// </summary>
        /// <param name="viewModel">View model with property data</param>
        /// <returns>Dialog result</returns>
        public static bool? ShowDialog(PropertiesDialogViewModel viewModel)
        {
            var dialog = new PropertiesDialog(viewModel);
            return dialog.ShowDialog();
        }
    }
}