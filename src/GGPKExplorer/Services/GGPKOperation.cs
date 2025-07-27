using System;
using System.Threading;
using System.Threading.Tasks;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Base class for GGPK operations that can be queued and executed on the GGPK thread
    /// </summary>
    internal abstract class GGPKOperation
    {
        /// <summary>
        /// Cancellation token for the operation
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Initializes a new instance of the GGPKOperation class
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        protected GGPKOperation(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Executes the operation
        /// </summary>
        /// <returns>Result of the operation</returns>
        public abstract object Execute();

        /// <summary>
        /// Sets the result of the operation
        /// </summary>
        /// <param name="result">Operation result</param>
        public abstract void SetResult(object result);

        /// <summary>
        /// Sets the operation as canceled
        /// </summary>
        public abstract void SetCanceled();

        /// <summary>
        /// Sets an exception for the operation
        /// </summary>
        /// <param name="exception">Exception that occurred</param>
        public abstract void SetException(Exception exception);
    }

    /// <summary>
    /// Generic GGPK operation with typed result
    /// </summary>
    /// <typeparam name="T">Type of the operation result</typeparam>
    internal class GGPKOperation<T> : GGPKOperation
    {
        private readonly Func<T> _operation;

        /// <summary>
        /// Task completion source for the operation
        /// </summary>
        public TaskCompletionSource<T>? TaskCompletionSource { get; set; }

        /// <summary>
        /// Initializes a new instance of the GGPKOperation class
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public GGPKOperation(Func<T> operation, CancellationToken cancellationToken)
            : base(cancellationToken)
        {
            _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        }

        /// <summary>
        /// Executes the operation
        /// </summary>
        /// <returns>Result of the operation</returns>
        public override object Execute()
        {
            CancellationToken.ThrowIfCancellationRequested();
            return _operation()!;
        }

        /// <summary>
        /// Sets the result of the operation
        /// </summary>
        /// <param name="result">Operation result</param>
        public override void SetResult(object result)
        {
            TaskCompletionSource?.SetResult((T)result);
        }

        /// <summary>
        /// Sets the operation as canceled
        /// </summary>
        public override void SetCanceled()
        {
            TaskCompletionSource?.SetCanceled();
        }

        /// <summary>
        /// Sets an exception for the operation
        /// </summary>
        /// <param name="exception">Exception that occurred</param>
        public override void SetException(Exception exception)
        {
            TaskCompletionSource?.SetException(exception);
        }
    }
}