using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the preview pane that shows file content and metadata
    /// </summary>
    public partial class PreviewPaneViewModel : ObservableObject
    {
        private readonly IGGPKService _ggpkService;
        private readonly IFileOperationsService _fileOperationsService;

        [ObservableProperty]
        private bool _hasSelectedFile;

        [ObservableProperty]
        private string _fileName = string.Empty;

        [ObservableProperty]
        private string _fileType = string.Empty;

        [ObservableProperty]
        private string _fileSize = string.Empty;

        [ObservableProperty]
        private string _filePath = string.Empty;

        [ObservableProperty]
        private string _compressionType = string.Empty;

        [ObservableProperty]
        private bool _hasCompression;

        [ObservableProperty]
        private string _modifiedDate = string.Empty;

        [ObservableProperty]
        private bool _hasModifiedDate;

        [ObservableProperty]
        private string _fileTypeIcon = "Document24";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isTextFile;

        [ObservableProperty]
        private bool _isImageFile;

        [ObservableProperty]
        private bool _isBinaryFile;

        [ObservableProperty]
        private bool _isUnsupportedFile;

        [ObservableProperty]
        private string _textContent = string.Empty;

        [ObservableProperty]
        private string _hexContent = string.Empty;

        [ObservableProperty]
        private string _imageInfo = string.Empty;

        private TreeNodeInfo? _currentNode;

        public PreviewPaneViewModel(IGGPKService ggpkService, IFileOperationsService fileOperationsService)
        {
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
        }

        /// <summary>
        /// Updates the preview with the selected file
        /// </summary>
        public async Task UpdatePreviewAsync(TreeNodeInfo? nodeInfo)
        {
            System.Diagnostics.Debug.WriteLine($"PreviewPane: UpdatePreviewAsync called with {(nodeInfo != null ? nodeInfo.Name : "null")}");
            
            _currentNode = nodeInfo;

            if (nodeInfo == null)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Clearing preview - nodeInfo is null");
                ClearPreview();
                return;
            }

            if (nodeInfo.Type == NodeType.Directory)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Showing directory contents for {nodeInfo.Name}");
                await ShowDirectoryContentsAsync(nodeInfo);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"PreviewPane: Setting up preview for file {nodeInfo.Name} (Type: {nodeInfo.Type})");
            
            HasSelectedFile = true;
            FileName = nodeInfo.Name;
            FilePath = nodeInfo.FullPath;
            FileSize = FormatFileSize(nodeInfo.Size);
            
            // Set file type and icon
            var extension = Path.GetExtension(nodeInfo.Name).ToLowerInvariant();
            FileType = GetFileTypeDescription(extension);
            FileTypeIcon = GetFileTypeIcon(extension);

            System.Diagnostics.Debug.WriteLine($"PreviewPane: File extension: {extension}, Type: {FileType}, Icon: {FileTypeIcon}");

            // Set compression info
            HasCompression = !string.IsNullOrEmpty(nodeInfo.Compression?.ToString());
            CompressionType = nodeInfo.Compression?.ToString() ?? string.Empty;

            // Set modified date
            HasModifiedDate = nodeInfo.ModifiedDate.HasValue;
            ModifiedDate = nodeInfo.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

            // Load file content for preview
            await LoadFileContentAsync(nodeInfo, extension);
        }

        /// <summary>
        /// Loads and processes file content for preview
        /// </summary>
        private async Task LoadFileContentAsync(TreeNodeInfo nodeInfo, string extension)
        {
            IsLoading = true;
            ClearPreviewContent();

            try
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Loading file content for {nodeInfo.FullPath} (Size: {nodeInfo.Size}, Type: {nodeInfo.Type})");
                
                // Don't load very large files for preview
                if (nodeInfo.Size > 1024 * 1024) // 1MB limit
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewPane: File too large for preview: {nodeInfo.Size} bytes");
                    IsUnsupportedFile = true;
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"PreviewPane: Calling ReadFileAsync for {nodeInfo.FullPath}");
                var fileData = await _ggpkService.ReadFileAsync(nodeInfo.FullPath);
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Successfully read {fileData.Length} bytes from {nodeInfo.FullPath}");
                
                if (IsTextFileExtension(extension))
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewPane: Loading as text file (by extension)");
                    await LoadTextPreview(fileData);
                }
                else if (IsImageFileExtension(extension))
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewPane: Image file detected - showing not available message");
                    LoadImageNotAvailable(extension);
                }
                else if (ContainsReadableText(fileData))
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewPane: Loading as text file (detected readable content)");
                    await LoadTextPreview(fileData);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"PreviewPane: Loading as binary file");
                    LoadBinaryPreview(fileData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Error loading file preview for {nodeInfo.FullPath}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Exception details: {ex}");
                IsUnsupportedFile = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Shows directory contents when a folder is selected
        /// </summary>
        private async Task ShowDirectoryContentsAsync(TreeNodeInfo directoryInfo)
        {
            try
            {
                IsLoading = true;
                ClearPreviewContent();

                HasSelectedFile = true;
                FileName = directoryInfo.Name;
                FilePath = directoryInfo.FullPath;
                FileType = "Directory";
                FileTypeIcon = "Folder24";
                FileSize = "Directory";

                // Set modified date if available
                HasModifiedDate = directoryInfo.ModifiedDate.HasValue;
                ModifiedDate = directoryInfo.ModifiedDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;

                // Get directory contents
                var children = await _ggpkService.GetChildrenAsync(directoryInfo.FullPath);
                
                // Create a text summary of the directory contents
                var contentBuilder = new StringBuilder();
                contentBuilder.AppendLine($"Directory: {directoryInfo.Name}");
                contentBuilder.AppendLine($"Path: {directoryInfo.FullPath}");
                contentBuilder.AppendLine();
                
                var directories = children.Where(c => c.Type == NodeType.Directory).OrderBy(c => c.Name).ToList();
                var files = children.Where(c => c.Type != NodeType.Directory).OrderBy(c => c.Name).ToList();
                
                contentBuilder.AppendLine($"Contents: {directories.Count} folder(s), {files.Count} file(s)");
                contentBuilder.AppendLine();
                
                if (directories.Any())
                {
                    contentBuilder.AppendLine("📁 Folders:");
                    foreach (var dir in directories)
                    {
                        contentBuilder.AppendLine($"  📁 {dir.Name}");
                    }
                    contentBuilder.AppendLine();
                }
                
                if (files.Any())
                {
                    contentBuilder.AppendLine("📄 Files:");
                    foreach (var file in files.Take(50)) // Limit to first 50 files
                    {
                        var sizeStr = file.Size > 0 ? FormatFileSize(file.Size) : "0 B";
                        contentBuilder.AppendLine($"  📄 {file.Name} ({sizeStr})");
                    }
                    
                    if (files.Count > 50)
                    {
                        contentBuilder.AppendLine($"  ... and {files.Count - 50} more files");
                    }
                }

                TextContent = contentBuilder.ToString();
                IsTextFile = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Error loading directory contents: {ex.Message}");
                TextContent = $"Error loading directory contents:\n{ex.Message}";
                IsTextFile = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Loads text file preview
        /// </summary>
        private async Task LoadTextPreview(byte[] fileData)
        {
            await Task.Run(() =>
            {
                try
                {
                    string content = DecodeTextContent(fileData);

                    // Limit preview to first 10,000 characters
                    if (content.Length > 10000)
                    {
                        content = content.Substring(0, 10000) + "\n\n... (truncated)";
                    }

                    TextContent = content;
                    IsTextFile = true;
                }
                catch
                {
                    LoadBinaryPreview(fileData);
                }
            });
        }

        /// <summary>
        /// Shows image not available message for image files
        /// </summary>
        private void LoadImageNotAvailable(string extension)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"PreviewPane: Showing image not available message for {extension} file");
                
                string imageInfo = $"{extension.ToUpperInvariant()} Image\n\n";
                imageInfo += "📷 Image Preview Not Available\n\n";
                imageInfo += "Image preview functionality has been disabled.\n";
                imageInfo += "This file appears to be an image, but preview\n";
                imageInfo += "is not currently supported.\n\n";
                imageInfo += "You can extract this file to view it with\n";
                imageInfo += "external image viewing applications.";

                ImageInfo = imageInfo;
                IsImageFile = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in LoadImageNotAvailable: {ex.Message}");
                IsUnsupportedFile = true;
            }
        }

        /// <summary>
        /// Loads binary file preview as hex dump
        /// </summary>
        private void LoadBinaryPreview(byte[] fileData)
        {
            try
            {
                var sb = new StringBuilder();
                var bytesToShow = Math.Min(fileData.Length, 512); // Show first 512 bytes

                for (int i = 0; i < bytesToShow; i += 16)
                {
                    // Address
                    sb.AppendFormat("{0:X8}: ", i);

                    // Hex bytes
                    for (int j = 0; j < 16; j++)
                    {
                        if (i + j < bytesToShow)
                        {
                            sb.AppendFormat("{0:X2} ", fileData[i + j]);
                        }
                        else
                        {
                            sb.Append("   ");
                        }
                    }

                    sb.Append(" ");

                    // ASCII representation
                    for (int j = 0; j < 16 && i + j < bytesToShow; j++)
                    {
                        var b = fileData[i + j];
                        sb.Append(b >= 32 && b <= 126 ? (char)b : '.');
                    }

                    sb.AppendLine();
                }

                if (fileData.Length > 512)
                {
                    sb.AppendLine($"\n... ({fileData.Length - 512} more bytes)");
                }

                HexContent = sb.ToString();
                IsBinaryFile = true;
            }
            catch
            {
                IsUnsupportedFile = true;
            }
        }

        /// <summary>
        /// Clears the preview content
        /// </summary>
        private void ClearPreview()
        {
            HasSelectedFile = false;
            ClearPreviewContent();
            _currentNode = null;
        }

        /// <summary>
        /// Clears preview content flags and data
        /// </summary>
        private void ClearPreviewContent()
        {
            IsTextFile = false;
            IsImageFile = false;
            IsBinaryFile = false;
            IsUnsupportedFile = false;
            TextContent = string.Empty;
            HexContent = string.Empty;
            ImageInfo = string.Empty;
        }

        /// <summary>
        /// Determines if file extension is for text files
        /// </summary>
        private bool IsTextFileExtension(string extension)
        {
            var textExtensions = new[] { 
                ".txt", ".xml", ".json", ".lua", ".cfg", ".ini", ".log", ".md", ".csv", 
                ".html", ".css", ".js", ".py", ".cs", ".cpp", ".h", ".hpp", ".c", ".cc",
                ".hlsl", ".fx", ".shader", ".glsl", ".vert", ".frag", ".geom", ".tesc", ".tese",
                ".yml", ".yaml", ".toml", ".properties", ".conf", ".config", ".bat", ".sh",
                ".sql", ".php", ".rb", ".go", ".rs", ".kt", ".swift", ".java", ".scala",
                ".dat", ".fmt", ".ot", ".ao", ".sm", ".tgr", // Common GGPK file extensions that might contain text
                ".aoc", ".epk", ".mat", ".pet", ".trl" // Additional GGPK plaintext file extensions
            };
            return textExtensions.Contains(extension);
        }

        /// <summary>
        /// Determines if file extension is for image files
        /// </summary>
        private bool IsImageFileExtension(string extension)
        {
            var imageExtensions = new[] { ".dds", ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".gif", ".webp" };
            return imageExtensions.Contains(extension);
        }

        /// <summary>
        /// Decodes text content from byte array, handling various encodings and fixing spacing issues
        /// </summary>
        private string DecodeTextContent(byte[] fileData)
        {
            if (fileData == null || fileData.Length == 0)
                return string.Empty;

            string content;

            // Check for BOM (Byte Order Mark) to detect encoding
            if (fileData.Length >= 3 && fileData[0] == 0xEF && fileData[1] == 0xBB && fileData[2] == 0xBF)
            {
                // UTF-8 with BOM
                content = Encoding.UTF8.GetString(fileData, 3, fileData.Length - 3);
            }
            else if (fileData.Length >= 2 && fileData[0] == 0xFF && fileData[1] == 0xFE)
            {
                // UTF-16 Little Endian
                content = Encoding.Unicode.GetString(fileData, 2, fileData.Length - 2);
            }
            else if (fileData.Length >= 2 && fileData[0] == 0xFE && fileData[1] == 0xFF)
            {
                // UTF-16 Big Endian
                content = Encoding.BigEndianUnicode.GetString(fileData, 2, fileData.Length - 2);
            }
            else
            {
                // Try different encodings in order of preference
                try
                {
                    // First try UTF-8
                    content = Encoding.UTF8.GetString(fileData);
                    
                    // Check if the content has excessive null characters (indicating wrong encoding)
                    int nullCount = content.Count(c => c == '\0');
                    if (nullCount > content.Length * 0.1) // More than 10% null characters
                    {
                        throw new Exception("Too many null characters, try different encoding");
                    }
                }
                catch
                {
                    try
                    {
                        // Try UTF-16 (common in some game files)
                        content = Encoding.Unicode.GetString(fileData);
                        
                        // Check for excessive null characters again
                        int nullCount = content.Count(c => c == '\0');
                        if (nullCount > content.Length * 0.1)
                        {
                            throw new Exception("Too many null characters, try ASCII");
                        }
                    }
                    catch
                    {
                        // Fallback to ASCII
                        content = Encoding.ASCII.GetString(fileData);
                    }
                }
            }

            // Clean up the content to fix spacing issues
            content = CleanTextContent(content);
            
            return content;
        }

        /// <summary>
        /// Cleans text content by removing null characters and fixing spacing issues
        /// </summary>
        private string CleanTextContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            // Remove null characters that cause spacing issues
            content = content.Replace("\0", "");
            
            // Remove other problematic control characters but keep useful ones
            var cleanedContent = new StringBuilder();
            foreach (char c in content)
            {
                // Keep printable characters, tabs, newlines, and carriage returns
                if (c >= 32 || c == '\t' || c == '\n' || c == '\r')
                {
                    cleanedContent.Append(c);
                }
                // Skip other control characters that might cause display issues
            }

            return cleanedContent.ToString();
        }

        /// <summary>
        /// Determines if file data contains mostly readable text
        /// Treats all readable files as plaintext with aggressive detection
        /// </summary>
        private bool ContainsReadableText(byte[] fileData)
        {
            if (fileData == null || fileData.Length == 0)
                return false;

            // Check first 2048 bytes or entire file if smaller (increased from 1024)
            int bytesToCheck = Math.Min(fileData.Length, 2048);
            double readableCount = 0;
            int totalCount = 0;
            int nullBytes = 0;

            for (int i = 0; i < bytesToCheck; i++)
            {
                byte b = fileData[i];
                totalCount++;

                // Count null bytes (common in binary files)
                if (b == 0)
                {
                    nullBytes++;
                }
                // Count printable ASCII characters, tabs, newlines, and carriage returns
                else if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13)
                {
                    readableCount++;
                }
                // Allow extended ASCII characters (count as fully readable now)
                else if (b >= 128 && b <= 255)
                {
                    readableCount++;
                }
                // Allow some control characters that might be in text files
                else if (b >= 1 && b <= 31 && b != 9 && b != 10 && b != 13)
                {
                    // Count control characters as quarter-readable
                    readableCount += 0.25;
                }
            }

            // If more than 10% null bytes, likely binary
            double nullRatio = (double)nullBytes / totalCount;
            if (nullRatio > 0.1)
                return false;

            // More aggressive threshold: 50% readable characters (lowered from 70%)
            double readableRatio = readableCount / totalCount;
            return readableRatio >= 0.5;
        }

        /// <summary>
        /// Gets file type description based on extension
        /// </summary>
        private string GetFileTypeDescription(string extension)
        {
            return extension switch
            {
                ".dds" => "DirectDraw Surface Image",
                ".ogg" => "Ogg Audio File",
                ".txt" => "Text File",
                ".xml" => "XML Document",
                ".json" => "JSON Data",
                ".lua" => "Lua Script",
                ".dat" => "Data File",
                ".fmt" => "Format File",
                ".ot" => "Object Template",
                ".ao" => "Animated Object",
                ".aoc" => "Animated Object Configuration",
                ".sm" => "Static Mesh",
                ".tgr" => "Trigger File",
                ".epk" => "Effect Package",
                ".mat" => "Material File",
                ".pet" => "Pet Configuration",
                ".trl" => "Trail Configuration",
                ".png" => "PNG Image",
                ".jpg" or ".jpeg" => "JPEG Image",
                ".bmp" => "Bitmap Image",
                ".cfg" => "Configuration File",
                ".ini" => "Initialization File",
                ".log" => "Log File",
                _ => $"{extension.TrimStart('.').ToUpperInvariant()} File"
            };
        }

        /// <summary>
        /// Gets appropriate icon for file type
        /// </summary>
        private string GetFileTypeIcon(string extension)
        {
            return extension switch
            {
                ".dds" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga" or ".gif" or ".webp" => "Image24",
                ".ogg" or ".mp3" or ".wav" => "MusicNote124",
                ".txt" or ".log" => "Document24",
                ".xml" or ".json" => "DocumentCode24",
                ".lua" or ".py" or ".cs" or ".cpp" or ".js" => "Code24",
                ".cfg" or ".ini" => "Settings24",
                ".dat" => "Database24",
                ".zip" or ".rar" or ".7z" => "FolderZip24",
                _ => "Document24"
            };
        }

        /// <summary>
        /// Formats file size for display
        /// </summary>
        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }


    }
}