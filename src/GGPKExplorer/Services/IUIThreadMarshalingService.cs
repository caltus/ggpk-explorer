using System;
using System.Threading.Tasks;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for marshaling operations to the UI thread
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public interface IUIThreadMarshalingService
    {
        /// <summary>
        /// Gets whether the current thread is the UI thread
        /// </summary>
        bool IsUIThread { get; }

        /// <summary>
        /// Executes an action on the UI thread synchronously
        /// </summary>
        /// <param name="action">Action to execute</param>
        void InvokeOnUIThread(Action action);

        /// <summary>
        /// Executes an action on the UI thread asynchronously
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <returns>Task that completes when the action finishes</returns>
        Task InvokeOnUIThreadAsync(Action action);

        /// <summary>
        /// Executes a function on the UI thread synchronously
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <returns>Result of the function</returns>
        T InvokeOnUIThread<T>(Func<T> function);

        /// <summary>
        /// Executes a function on the UI thread asynchronously
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <returns>Task that completes with the function result</returns>
        Task<T> InvokeOnUIThreadAsync<T>(Func<T> function);

        /// <summary>
        /// Executes an async function on the UI thread
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Async function to execute</param>
        /// <returns>Task that completes with the function result</returns>
        Task<T> InvokeOnUIThreadAsync<T>(Func<Task<T>> function);

        /// <summary>
        /// Posts an action to be executed on the UI thread without waiting
        /// </summary>
        /// <param name="action">Action to execute</param>
        void PostToUIThread(Action action);

        /// <summary>
        /// Executes an action on the UI thread with a timeout
        /// </summary>
        /// <param name="action">Action to execute</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <returns>True if the action completed within the timeout</returns>
        bool TryInvokeOnUIThread(Action action, TimeSpan timeout);

        /// <summary>
        /// Executes a function on the UI thread with a timeout
        /// </summary>
        /// <typeparam name="T">Return type of the function</typeparam>
        /// <param name="function">Function to execute</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="result">Result of the function if successful</param>
        /// <returns>True if the function completed within the timeout</returns>
        bool TryInvokeOnUIThread<T>(Func<T> function, TimeSpan timeout, out T result);
    }
}