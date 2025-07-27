using System;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Event arguments for GGPK loaded events
    /// </summary>
    public class GGPKLoadedEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the loaded GGPK file
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Whether the GGPK file contains bundles
        /// </summary>
        public bool HasBundles { get; }

        /// <summary>
        /// Version of the GGPK file
        /// </summary>
        public uint Version { get; }

        public GGPKLoadedEventArgs(string filePath, bool hasBundles = false, uint version = 0)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            HasBundles = hasBundles;
            Version = version;
        }
    }

    /// <summary>
    /// Event arguments for progress events
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double Percentage { get; }

        /// <summary>
        /// Current operation description
        /// </summary>
        public string Operation { get; }

        /// <summary>
        /// Current item being processed
        /// </summary>
        public string? CurrentItem { get; }

        /// <summary>
        /// Whether the operation can be cancelled
        /// </summary>
        public bool CanCancel { get; }

        public ProgressEventArgs(double percentage, string operation, string? currentItem = null, bool canCancel = true)
        {
            Percentage = Math.Max(0, Math.Min(100, percentage));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            CurrentItem = currentItem;
            CanCancel = canCancel;
        }
    }

    /// <summary>
    /// Event arguments for error events
    /// </summary>
    public class ErrorEventArgs : EventArgs
    {
        /// <summary>
        /// The exception that occurred
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Whether the error is recoverable
        /// </summary>
        public bool IsRecoverable { get; }

        /// <summary>
        /// Context information about when the error occurred
        /// </summary>
        public string? Context { get; }

        public ErrorEventArgs(Exception exception, bool isRecoverable = false, string? context = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            IsRecoverable = isRecoverable;
            Context = context;
        }
    }
}