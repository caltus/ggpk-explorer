using System.Windows;
using Wpf.Ui.Controls;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for ExtractionDialog.xaml
    /// </summary>
    public partial class ExtractionDialog : ContentDialog
    {
        public ExtractionDialog()
        {
            InitializeComponent();
        }

        public ExtractionDialog(ExtractionDialogViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
}