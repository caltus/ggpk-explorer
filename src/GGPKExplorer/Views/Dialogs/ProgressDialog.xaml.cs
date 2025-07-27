using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for ProgressDialog.xaml
    /// </summary>
    public partial class ProgressDialog : FluentWindow
    {
        public ProgressDialog()
        {
            InitializeComponent();
        }

        public ProgressDialog(ProgressDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;
            
            // Handle close events
            if (viewModel != null)
            {
                viewModel.CloseRequested += (s, e) => Close();
                
                // Prevent closing during operation unless cancelled
                Closing += OnClosing;
            }
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (DataContext is ProgressDialogViewModel viewModel)
            {
                // Allow closing if operation is complete or cancelled
                if (!viewModel.IsOperationInProgress)
                    return;

                // If operation is in progress, ask for confirmation
                var result = System.Windows.MessageBox.Show(
                    "An operation is currently in progress. Do you want to cancel it?",
                    "Cancel Operation",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    viewModel.CancelCommand.Execute(null);
                    // Allow the operation to cancel, then close
                }
                else
                {
                    e.Cancel = true;
                }
            }
        }
    }
}