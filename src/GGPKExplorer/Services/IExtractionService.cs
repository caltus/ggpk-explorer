using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for managing file extraction operations with UI dialogs
    /// </summary>
    public interface IExtractionService
    {
        /// <summary>
        /// Shows the extraction dialog for the specified files and handles the extraction process
        /// </summary>
        /// <param name="filesToExtract">Files and directories to extract</param>
        /// <param name="owner">Owner window for the dialog</param>
        /// <returns>Extraction results, or null if cancelled</returns>
        Task<ExtractionResults?> ShowExtractionDialogAsync(IEnumerable<TreeNodeInfo> filesToExtract, Window? owner = null);

        /// <summary>
        /// Shows a progress dialog for a long-running extraction operation
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="operationTitle">Operation description</param>
        /// <param name="owner">Owner window for the dialog</param>
        /// <returns>Progress dialog that can be updated during the operation</returns>
        Task<IProgressDialog> ShowProgressDialogAsync(string title, string operationTitle, Window? owner = null);

        /// <summary>
        /// Extracts files with a simple progress dialog (no configuration options)
        /// </summary>
        /// <param name="filesToExtract">Files to extract</param>
        /// <param name="destinationPath">Destination folder</param>
        /// <param name="owner">Owner window for the dialog</param>
        /// <returns>Extraction results</returns>
        Task<ExtractionResults> ExtractWithProgressAsync(IEnumerable<TreeNodeInfo> filesToExtract, string destinationPath, Window? owner = null);
    }

    /// <summary>
    /// Interface for controlling a progress dialog during operations
    /// </summary>
    public interface IProgressDialog : IDisposable
    {
        /// <summary>
        /// Updates the progress information
        /// </summary>
        void UpdateProgress(ProgressInfo progressInfo);

        /// <summary>
        /// Completes the operation and updates the dialog
        /// </summary>
        void CompleteOperation(bool success, string? message = null);

        /// <summary>
        /// Adds an error message to the dialog
        /// </summary>
        void AddError(string error);

        /// <summary>
        /// Adds a detail message to the dialog log
        /// </summary>
        void AddDetail(string detail);

        /// <summary>
        /// Closes the progress dialog
        /// </summary>
        void Close();

        /// <summary>
        /// Event raised when the user cancels the operation
        /// </summary>
        event EventHandler? Cancelled;
    }
}