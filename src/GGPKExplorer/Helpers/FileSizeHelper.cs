using System;

namespace GGPKExplorer.Helpers
{
    /// <summary>
    /// Helper class for formatting file sizes in human-readable format
    /// </summary>
    public static class FileSizeHelper
    {
        private static readonly string[] SizeUnits = { "bytes", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="sizeInBytes">Size in bytes</param>
        /// <returns>Formatted size string (e.g., "1.5 MB", "2.3 KB")</returns>
        public static string FormatFileSize(long sizeInBytes)
        {
            if (sizeInBytes == 0)
                return "0 bytes";

            if (sizeInBytes < 0)
                return "Unknown";

            int unitIndex = 0;
            double size = sizeInBytes;

            while (size >= 1024 && unitIndex < SizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            // Format with appropriate decimal places
            string formattedSize = unitIndex == 0 
                ? size.ToString("0") 
                : size.ToString("0.#");

            return $"{formattedSize} {SizeUnits[unitIndex]}";
        }

        /// <summary>
        /// Formats a file size with additional byte count information
        /// </summary>
        /// <param name="sizeInBytes">Size in bytes</param>
        /// <returns>Formatted size string with byte count (e.g., "1.5 MB (1,572,864 bytes)")</returns>
        public static string FormatFileSizeWithBytes(long sizeInBytes)
        {
            if (sizeInBytes == 0)
                return "0 bytes";

            if (sizeInBytes < 0)
                return "Unknown";

            string humanReadable = FormatFileSize(sizeInBytes);
            
            // If already in bytes, don't duplicate
            if (sizeInBytes < 1024)
                return humanReadable;

            return $"{humanReadable} ({sizeInBytes:N0} bytes)";
        }
    }
}