using System;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Base exception class for GGPK-related errors
    /// </summary>
    public class GGPKException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the GGPKException class
        /// </summary>
        public GGPKException() : base() { }

        /// <summary>
        /// Initializes a new instance of the GGPKException class with a specified error message
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public GGPKException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the GGPKException class with a specified error message and inner exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public GGPKException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when GGPK file corruption is detected
    /// </summary>
    public class GGPKCorruptedException : GGPKException
    {
        /// <summary>
        /// The offset in the file where corruption was detected
        /// </summary>
        public long CorruptedOffset { get; }

        /// <summary>
        /// Initializes a new instance of the GGPKCorruptedException class
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="offset">The offset where corruption was detected</param>
        public GGPKCorruptedException(string message, long offset) : base(message)
        {
            CorruptedOffset = offset;
        }

        /// <summary>
        /// Initializes a new instance of the GGPKCorruptedException class with inner exception
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="offset">The offset where corruption was detected</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public GGPKCorruptedException(string message, long offset, Exception innerException) : base(message, innerException)
        {
            CorruptedOffset = offset;
        }
    }

    /// <summary>
    /// Exception thrown when bundle decompression fails
    /// </summary>
    public class BundleDecompressionException : GGPKException
    {
        /// <summary>
        /// The name of the bundle that failed to decompress
        /// </summary>
        public string BundleName { get; }

        /// <summary>
        /// Initializes a new instance of the BundleDecompressionException class
        /// </summary>
        /// <param name="bundleName">The name of the bundle that failed</param>
        /// <param name="message">The message that describes the error</param>
        public BundleDecompressionException(string bundleName, string message) : base(message)
        {
            BundleName = bundleName;
        }

        /// <summary>
        /// Initializes a new instance of the BundleDecompressionException class with inner exception
        /// </summary>
        /// <param name="bundleName">The name of the bundle that failed</param>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public BundleDecompressionException(string bundleName, string message, Exception innerException) : base(message, innerException)
        {
            BundleName = bundleName;
        }
    }

    /// <summary>
    /// Exception thrown when file operations fail
    /// </summary>
    public class FileOperationException : GGPKException
    {
        /// <summary>
        /// The path of the file that caused the operation to fail
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// The type of operation that failed
        /// </summary>
        public FileOperationType OperationType { get; }

        /// <summary>
        /// Initializes a new instance of the FileOperationException class
        /// </summary>
        /// <param name="filePath">The path of the file that caused the failure</param>
        /// <param name="operationType">The type of operation that failed</param>
        /// <param name="message">The message that describes the error</param>
        public FileOperationException(string filePath, FileOperationType operationType, string message) : base(message)
        {
            FilePath = filePath;
            OperationType = operationType;
        }

        /// <summary>
        /// Initializes a new instance of the FileOperationException class with inner exception
        /// </summary>
        /// <param name="filePath">The path of the file that caused the failure</param>
        /// <param name="operationType">The type of operation that failed</param>
        /// <param name="message">The message that describes the error</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public FileOperationException(string filePath, FileOperationType operationType, string message, Exception innerException) : base(message, innerException)
        {
            FilePath = filePath;
            OperationType = operationType;
        }
    }

    /// <summary>
    /// Types of file operations that can fail
    /// </summary>
    public enum FileOperationType
    {
        /// <summary>
        /// Reading file content
        /// </summary>
        Read,

        /// <summary>
        /// Extracting file to disk
        /// </summary>
        Extract,

        /// <summary>
        /// Getting file properties
        /// </summary>
        GetProperties,

        /// <summary>
        /// Searching files
        /// </summary>
        Search
    }
}