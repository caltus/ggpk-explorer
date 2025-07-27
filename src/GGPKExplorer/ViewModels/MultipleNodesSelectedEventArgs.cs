using System;
using System.Collections.Generic;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// Event arguments for multiple tree node selection events
    /// </summary>
    public class MultipleNodesSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the collection of selected tree node view models
        /// </summary>
        public IReadOnlyList<TreeNodeViewModel> SelectedNodes { get; }

        /// <summary>
        /// Gets the number of selected nodes
        /// </summary>
        public int Count => SelectedNodes.Count;

        /// <summary>
        /// Initializes a new instance of the MultipleNodesSelectedEventArgs class
        /// </summary>
        /// <param name="selectedNodes">The collection of selected tree node view models</param>
        public MultipleNodesSelectedEventArgs(IReadOnlyList<TreeNodeViewModel> selectedNodes)
        {
            SelectedNodes = selectedNodes ?? throw new ArgumentNullException(nameof(selectedNodes));
        }
    }
}