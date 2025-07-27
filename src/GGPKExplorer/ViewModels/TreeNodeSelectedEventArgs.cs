using System;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// Event arguments for tree node selection events
    /// </summary>
    public class TreeNodeSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the selected tree node view model
        /// </summary>
        public TreeNodeViewModel SelectedNode { get; }

        /// <summary>
        /// Initializes a new instance of the TreeNodeSelectedEventArgs class
        /// </summary>
        /// <param name="selectedNode">The selected tree node view model</param>
        public TreeNodeSelectedEventArgs(TreeNodeViewModel selectedNode)
        {
            SelectedNode = selectedNode ?? throw new ArgumentNullException(nameof(selectedNode));
        }
    }
}