using System.Windows;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// About dialog for GGPK Explorer
    /// </summary>
    public partial class AboutDialog : FluentWindow
    {
        public AboutDialog()
        {
            InitializeComponent();
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}