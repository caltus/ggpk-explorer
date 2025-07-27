using System;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for displaying toast notifications
    /// </summary>
    public interface IToastService
    {
        /// <summary>
        /// Shows a success toast notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title for the toast</param>
        /// <param name="timeout">Timeout in milliseconds (default: 4000)</param>
        void ShowSuccess(string message, string? title = null, int timeout = 4000);

        /// <summary>
        /// Shows an error toast notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title for the toast</param>
        /// <param name="timeout">Timeout in milliseconds (default: 6000)</param>
        void ShowError(string message, string? title = null, int timeout = 6000);

        /// <summary>
        /// Shows an info toast notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title for the toast</param>
        /// <param name="timeout">Timeout in milliseconds (default: 3000)</param>
        void ShowInfo(string message, string? title = null, int timeout = 3000);

        /// <summary>
        /// Shows a warning toast notification
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="title">Optional title for the toast</param>
        /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
        void ShowWarning(string message, string? title = null, int timeout = 5000);
    }
}