using System.Windows;
using System.Windows.Media;

namespace GGPKExplorer.Extensions
{
    /// <summary>
    /// Extension methods for visual tree operations
    /// </summary>
    public static class VisualTreeExtensions
    {
        /// <summary>
        /// Finds an ancestor of the specified type
        /// </summary>
        /// <typeparam name="T">Type of ancestor to find</typeparam>
        /// <param name="element">Starting element</param>
        /// <returns>Ancestor of specified type or null if not found</returns>
        public static T? FindAncestor<T>(this DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T ancestor)
                    return ancestor;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// Finds a visual child of the specified type
        /// </summary>
        /// <typeparam name="T">Type of child to find</typeparam>
        /// <param name="element">Starting element</param>
        /// <returns>Child of specified type or null if not found</returns>
        public static T? FindVisualChild<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is T result)
                    return result;

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }

        /// <summary>
        /// Finds all visual children of the specified type
        /// </summary>
        /// <typeparam name="T">Type of children to find</typeparam>
        /// <param name="element">Starting element</param>
        /// <returns>Collection of children of specified type</returns>
        public static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(this DependencyObject element) where T : DependencyObject
        {
            if (element == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                if (child is T result)
                    yield return result;

                foreach (var descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }
    }
}