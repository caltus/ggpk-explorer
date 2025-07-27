using System;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Represents a memory resource for tracking purposes
    /// </summary>
    public class MemoryResource : IDisposable
    {
        private readonly byte[] _data;
        private bool _disposed;

        /// <summary>
        /// Gets the size of the memory resource in bytes
        /// </summary>
        public long Size => _data?.Length ?? 0;

        /// <summary>
        /// Initializes a new instance of the MemoryResource class
        /// </summary>
        /// <param name="data">The data to track</param>
        public MemoryResource(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <summary>
        /// Disposes the memory resource
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // The data will be garbage collected
                _disposed = true;
            }
        }
    }
}