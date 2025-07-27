using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for handling drag and drop operations in the GGPK Explorer
    /// </summary>
    public interface IDragDropService
    {
        void InitializeDragDrop(FrameworkElement element);
        Task<bool> HandleFileDropAsync(DragEventArgs e, string targetPath);
        void StartFileDrag(DragEventArgs e, IEnumerable<TreeNodeViewModel> selectedNodes);
        bool CanAcceptDrop(DragEventArgs e);
    }

    public class DragDropService : IDragDropService
    {
        private readonly IFileOperationsService _fileOperationsService;
        private readonly IErrorHandlingService _errorHandlingService;

        public DragDropService(
            IFileOperationsService fileOperationsService,
            IErrorHandlingService errorHandlingService)
        {
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
        }

        public void InitializeDragDrop(FrameworkElement element)
        {
            if (element == null) return;

            element.AllowDrop = true;
            element.DragEnter += OnDragEnter;
            element.DragOver += OnDragOver;
            element.DragLeave += OnDragLeave;
            element.Drop += OnDrop;
        }

        public Task<bool> HandleFileDropAsync(DragEventArgs e, string targetPath)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                    return Task.FromResult(false);

                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                    return Task.FromResult(false);

                // Handle GGPK file drops
                var ggpkFiles = files.Where(f => Path.GetExtension(f).Equals(".ggpk", StringComparison.OrdinalIgnoreCase)).ToArray();
                
                if (ggpkFiles.Length > 0)
                {
                    // GGPK file opening should be handled by the main application
                    // This service only validates the drop operation
                    return Task.FromResult(true);
                }

                // Handle extraction target drops
                if (!string.IsNullOrEmpty(targetPath))
                {
                    var extractionPath = files.FirstOrDefault(f => Directory.Exists(f));
                    if (!string.IsNullOrEmpty(extractionPath))
                    {
                        // File extraction should be handled by FileOperationsService
                        // This service only validates the drop operation
                        return Task.FromResult(true);
                    }
                }

                return Task.FromResult(false);
            }
            catch (Exception ex)
            {
                _ = Task.Run(async () => await _errorHandlingService.HandleExceptionAsync(ex, "Drag and drop operation", showDialog: false));
                return Task.FromResult(false);
            }
        }

        public void StartFileDrag(DragEventArgs e, IEnumerable<TreeNodeViewModel> selectedNodes)
        {
            try
            {
                if (selectedNodes == null || !selectedNodes.Any())
                    return;

                var dataObject = new DataObject();
                
                // Add file paths for internal drag operations
                var filePaths = selectedNodes.Select(n => n.DisplayName).ToArray();
                dataObject.SetData("GGPKFilePaths", filePaths);
                
                // Add file names for display
                var fileNames = selectedNodes.Select(n => n.DisplayName).ToArray();
                dataObject.SetData(DataFormats.Text, string.Join(Environment.NewLine, fileNames));

                // Start drag operation
                DragDrop.DoDragDrop((DependencyObject)e.Source, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                _ = Task.Run(async () => await _errorHandlingService.HandleExceptionAsync(ex, "Start drag operation", showDialog: false));
            }
        }

        public bool CanAcceptDrop(DragEventArgs e)
        {
            // Accept file drops (for opening GGPK files)
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                return files?.Any(f => Path.GetExtension(f).Equals(".ggpk", StringComparison.OrdinalIgnoreCase) || Directory.Exists(f)) == true;
            }

            // Accept internal GGPK file drops
            if (e.Data.GetDataPresent("GGPKFilePaths"))
                return true;

            return false;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            UpdateDragEffects(e);
            
            // Add visual feedback
            if (sender is FrameworkElement element)
            {
                element.Opacity = 0.8;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            UpdateDragEffects(e);
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            // Remove visual feedback
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }
        }

        private async void OnDrop(object sender, DragEventArgs e)
        {
            // Remove visual feedback
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }

            // Handle the drop
            string? targetPath = null;
            if (sender is FrameworkElement fe && fe.DataContext is TreeNodeViewModel node)
            {
                targetPath = node.DisplayName;
            }

            await HandleFileDropAsync(e, targetPath ?? "");
        }

        private void UpdateDragEffects(DragEventArgs e)
        {
            if (CanAcceptDrop(e))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }
    }
}