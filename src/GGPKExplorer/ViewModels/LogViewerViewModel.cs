using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the log viewer control
    /// </summary>
    public partial class LogViewerViewModel : ObservableObject
    {
        private readonly string _logsDirectory;

        [ObservableProperty]
        private ObservableCollection<LogFileInfo> logFiles = new();

        [ObservableProperty]
        private LogFileInfo? selectedLogFile;

        [ObservableProperty]
        private string logContent = string.Empty;

        [ObservableProperty]
        private string statusMessage = "Select a log file to view its contents";

        [ObservableProperty]
        private int logFileCount;

        [ObservableProperty]
        private bool hasSelectedLog;

        [ObservableProperty]
        private bool isJsonFile;

        partial void OnIsJsonFileChanged(bool value)
        {
            System.Diagnostics.Debug.WriteLine($"LogViewer: IsJsonFile changed to: {value}");
        }



        [ObservableProperty]
        private ObservableCollection<JsonNodeViewModel> jsonNodes = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isRegexSearch = false;

        [ObservableProperty]
        private string filteredLogContent = string.Empty;

        [ObservableProperty]
        private bool hasSearchResults = true;

        public LogViewerViewModel()
        {
            // Get logs directory from application base directory
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logsDirectory = Path.Combine(appDirectory, "logs");

            // Ensure logs directory exists
            Directory.CreateDirectory(_logsDirectory);

            // Load log files
            RefreshLogs();
        }

        partial void OnSelectedLogFileChanged(LogFileInfo? value)
        {
            HasSelectedLog = value != null;
            
            if (value != null)
            {
                LoadLogContent(value);
            }
            else
            {
                LogContent = string.Empty;
                FilteredLogContent = string.Empty;
                StatusMessage = "Select a log file to view its contents";
                JsonNodes.Clear();
                IsJsonFile = false;
            }
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplySearch();
        }

        partial void OnIsRegexSearchChanged(bool value)
        {
            ApplySearch();
        }

        private void ApplySearch()
        {
            if (string.IsNullOrEmpty(LogContent))
            {
                FilteredLogContent = string.Empty;
                HasSearchResults = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredLogContent = LogContent;
                HasSearchResults = true;
                return;
            }

            try
            {
                var lines = LogContent.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
                var filteredLines = new List<string>();

                if (IsRegexSearch)
                {
                    var regex = new System.Text.RegularExpressions.Regex(SearchText, 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | 
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    
                    foreach (var line in lines)
                    {
                        if (regex.IsMatch(line))
                        {
                            filteredLines.Add(line);
                        }
                    }
                }
                else
                {
                    foreach (var line in lines)
                    {
                        if (line.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                        {
                            filteredLines.Add(line);
                        }
                    }
                }

                FilteredLogContent = string.Join(Environment.NewLine, filteredLines);
                HasSearchResults = filteredLines.Count > 0;
                
                if (HasSearchResults)
                {
                    StatusMessage = $"Found {filteredLines.Count} matching lines";
                }
                else
                {
                    StatusMessage = "No matches found";
                }
            }
            catch (Exception ex)
            {
                FilteredLogContent = LogContent;
                HasSearchResults = true;
                StatusMessage = $"Search error: {ex.Message}";
            }
        }

        [RelayCommand]
        private void RefreshLogs()
        {
            try
            {
                LogFiles.Clear();
                
                if (!Directory.Exists(_logsDirectory))
                {
                    StatusMessage = "Logs directory not found";
                    LogFileCount = 0;
                    return;
                }

                var logFiles = Directory.GetFiles(_logsDirectory, "*.*")
                    .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || 
                               f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new LogFileInfo(f))
                    .OrderByDescending(f => f.LastModifiedDate)
                    .ToList();

                foreach (var logFile in logFiles)
                {
                    LogFiles.Add(logFile);
                }

                LogFileCount = LogFiles.Count;
                StatusMessage = LogFileCount > 0 ? "Log files loaded successfully" : "No log files found";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading log files: {ex.Message}";
                LogFileCount = 0;
            }
        }

        [RelayCommand]
        private void OpenLogsFolder()
        {
            try
            {
                if (Directory.Exists(_logsDirectory))
                {
                    Process.Start("explorer.exe", _logsDirectory);
                }
                else
                {
                    StatusMessage = "Logs directory does not exist";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error opening logs folder: {ex.Message}";
            }
        }

        [RelayCommand]
        private void ClearAllLogs()
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete all log files? This action cannot be undone.",
                    "Clear All Logs",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var files = Directory.GetFiles(_logsDirectory, "*.*")
                        .Where(f => f.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || 
                                   f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

                    int deletedCount = 0;
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch
                        {
                            // Continue with other files if one fails
                        }
                    }

                    StatusMessage = $"Deleted {deletedCount} log files";
                    RefreshLogs();
                    SelectedLogFile = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error clearing logs: {ex.Message}";
            }
        }

        [RelayCommand]
        private void CopyContent()
        {
            try
            {
                if (!string.IsNullOrEmpty(LogContent))
                {
                    Clipboard.SetText(LogContent);
                    StatusMessage = "Log content copied to clipboard";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error copying content: {ex.Message}";
            }
        }

        [RelayCommand]
        private void SaveAs()
        {
            try
            {
                if (SelectedLogFile == null || string.IsNullOrEmpty(LogContent))
                    return;

                var saveDialog = new SaveFileDialog
                {
                    FileName = SelectedLogFile.FileName,
                    Filter = "Log Files (*.log)|*.log|JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                    DefaultExt = Path.GetExtension(SelectedLogFile.FileName)
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveDialog.FileName, LogContent, Encoding.UTF8);
                    StatusMessage = $"Log saved to {Path.GetFileName(saveDialog.FileName)}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving file: {ex.Message}";
            }
        }

        /// <summary>
        /// Determines if the content is valid JSON or JSONL format
        /// </summary>
        private bool IsValidJsonContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                System.Diagnostics.Debug.WriteLine("IsValidJsonContent: Content is null or whitespace");
                return false;
            }

            // Trim whitespace and check if it starts with JSON indicators
            var trimmed = content.Trim();
            System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: Content length: {content.Length}, Trimmed length: {trimmed.Length}");
            System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: First 50 chars: '{(trimmed.Length > 50 ? trimmed.Substring(0, 50) : trimmed)}'");
            
            // Check if it starts with JSON indicators
            bool startsWithJson = trimmed.StartsWith("{") || trimmed.StartsWith("[");
            System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: Starts with JSON indicator: {startsWithJson}");
            
            if (!startsWithJson)
            {
                System.Diagnostics.Debug.WriteLine("IsValidJsonContent: Content doesn't start with { or [");
                return false;
            }

            try
            {
                // Try to parse as regular JSON first
                var jToken = Newtonsoft.Json.Linq.JToken.Parse(content);
                System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: Successfully parsed as JSON, token type: {jToken.Type}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: Regular JSON parsing failed: {ex.Message}");
                
                // Try to parse as JSONL (JSON Lines) format
                try
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    bool allLinesAreJson = true;
                    int validJsonLines = 0;
                    
                    foreach (var line in lines.Take(5)) // Check first 5 lines
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            try
                            {
                                Newtonsoft.Json.Linq.JToken.Parse(trimmedLine);
                                validJsonLines++;
                            }
                            catch
                            {
                                allLinesAreJson = false;
                                break;
                            }
                        }
                    }
                    
                    if (allLinesAreJson && validJsonLines > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: Successfully detected as JSONL format with {validJsonLines} valid JSON lines");
                        return true;
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"IsValidJsonContent: JSONL parsing also failed: {ex2.Message}");
                }
                
                return false;
            }
        }

        private async void LoadLogContent(LogFileInfo logFile)
        {
            try
            {
                StatusMessage = "Loading log content...";
                
                await Task.Run(() =>
                {
                    var content = File.ReadAllText(logFile.FilePath, Encoding.UTF8);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        LogContent = content;
                        
                        // Apply any active search filter
                        ApplySearch();
                        
                        // Check if content is valid JSON (regardless of file extension)
                        bool hasJsonExtension = logFile.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
                        bool isValidJson = IsValidJsonContent(content);
                        
                        System.Diagnostics.Debug.WriteLine($"LogViewer: File extension check - hasJsonExtension: {hasJsonExtension}");
                        System.Diagnostics.Debug.WriteLine($"LogViewer: Content validation - isValidJson: {isValidJson}");
                        
                        // Use JSON viewer if content is valid JSON
                        IsJsonFile = isValidJson;
                        
                        System.Diagnostics.Debug.WriteLine($"LogViewer: File {logFile.FileName}, IsValidJson: {isValidJson}");
                        
                        if (isValidJson)
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("LogViewer: Parsing JSON content directly...");
                                LoadJsonNodes(content);
                                StatusMessage = $"Loaded {logFile.FileName} ({logFile.FileSize}) - JSON format detected";
                            }
                            catch (Exception ex)
                            {
                                // If JSON parsing fails, fall back to plain text
                                IsJsonFile = false;
                                JsonNodes.Clear();
                                System.Diagnostics.Debug.WriteLine($"LogViewer: JSON parsing failed: {ex.Message}");
                                StatusMessage = $"Loaded {logFile.FileName} ({logFile.FileSize}) - JSON parsing failed, showing as text";
                            }
                        }
                        else
                        {
                            JsonNodes.Clear();
                            StatusMessage = $"Loaded {logFile.FileName} ({logFile.FileSize}) - Plain text format";
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogContent = $"Error loading log file: {ex.Message}";
                StatusMessage = $"Error loading {logFile.FileName}";
                IsJsonFile = false;
                JsonNodes.Clear();
            }
        }

        /// <summary>
        /// Loads JSON or JSONL content and builds the tree structure directly
        /// </summary>
        private void LoadJsonNodes(string jsonContent)
        {
            JsonNodes.Clear();
            
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return;
            }

            try
            {
                // Try to parse as regular JSON first
                var jToken = Newtonsoft.Json.Linq.JToken.Parse(jsonContent);
                var rootNode = CreateNodeFromJToken("root", jToken, null);
                JsonNodes.Add(rootNode);
                System.Diagnostics.Debug.WriteLine($"LogViewer: JSON nodes loaded as single JSON, count: {JsonNodes.Count}");
            }
            catch
            {
                // If regular JSON parsing fails, try JSONL format
                try
                {
                    var lines = jsonContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var jsonArray = new Newtonsoft.Json.Linq.JArray();
                    
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                        {
                            try
                            {
                                var lineToken = Newtonsoft.Json.Linq.JToken.Parse(trimmedLine);
                                jsonArray.Add(lineToken);
                            }
                            catch
                            {
                                // Skip invalid lines
                            }
                        }
                    }
                    
                    var rootNode = CreateNodeFromJToken("JSONL Log Entries", jsonArray, null);
                    JsonNodes.Add(rootNode);
                    System.Diagnostics.Debug.WriteLine($"LogViewer: JSONL nodes loaded, count: {JsonNodes.Count}, entries: {jsonArray.Count}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LogViewer: Failed to parse as JSONL: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Creates a JsonNodeViewModel from a JToken
        /// </summary>
        private JsonNodeViewModel CreateNodeFromJToken(string key, Newtonsoft.Json.Linq.JToken token, JsonNodeViewModel? parent)
        {
            var node = new JsonNodeViewModel(key, parent);

            switch (token.Type)
            {
                case Newtonsoft.Json.Linq.JTokenType.Object:
                    node.ValueType = JsonValueType.Object;
                    node.DisplayValue = $"{{ {((Newtonsoft.Json.Linq.JObject)token).Count} properties }}";
                    node.TypeIndicator = "object";
                    
                    foreach (var property in ((Newtonsoft.Json.Linq.JObject)token).Properties())
                    {
                        var childNode = CreateNodeFromJToken(property.Name, property.Value, node);
                        node.Children.Add(childNode);
                    }
                    break;

                case Newtonsoft.Json.Linq.JTokenType.Array:
                    node.ValueType = JsonValueType.Array;
                    var array = (Newtonsoft.Json.Linq.JArray)token;
                    node.DisplayValue = $"[ {array.Count} items ]";
                    node.TypeIndicator = "array";
                    
                    for (int i = 0; i < array.Count; i++)
                    {
                        var childNode = CreateNodeFromJToken($"[{i}]", array[i], node);
                        node.Children.Add(childNode);
                    }
                    break;

                case Newtonsoft.Json.Linq.JTokenType.String:
                    node.ValueType = JsonValueType.String;
                    node.DisplayValue = $"\"{token.ToString()}\"";
                    node.TypeIndicator = "string";
                    break;

                case Newtonsoft.Json.Linq.JTokenType.Integer:
                case Newtonsoft.Json.Linq.JTokenType.Float:
                    node.ValueType = JsonValueType.Number;
                    node.DisplayValue = token.ToString();
                    node.TypeIndicator = token.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? "integer" : "number";
                    break;

                case Newtonsoft.Json.Linq.JTokenType.Boolean:
                    node.ValueType = JsonValueType.Boolean;
                    node.DisplayValue = token.ToString().ToLower();
                    node.TypeIndicator = "boolean";
                    break;

                case Newtonsoft.Json.Linq.JTokenType.Null:
                    node.ValueType = JsonValueType.Null;
                    node.DisplayValue = "null";
                    node.TypeIndicator = "null";
                    break;

                case Newtonsoft.Json.Linq.JTokenType.Date:
                    node.ValueType = JsonValueType.String;
                    node.DisplayValue = $"\"{token.ToString()}\"";
                    node.TypeIndicator = "datetime";
                    break;

                default:
                    node.ValueType = JsonValueType.String;
                    node.DisplayValue = token.ToString();
                    node.TypeIndicator = token.Type.ToString().ToLower();
                    break;
            }

            return node;
        }
    }

    /// <summary>
    /// Represents a node in the JSON tree
    /// </summary>
    public partial class JsonNodeViewModel : ObservableObject
    {
        [ObservableProperty]
        private string key;

        [ObservableProperty]
        private string displayValue = string.Empty;

        [ObservableProperty]
        private JsonValueType valueType;

        [ObservableProperty]
        private string typeIndicator = string.Empty;

        [ObservableProperty]
        private bool isExpanded;

        [ObservableProperty]
        private ObservableCollection<JsonNodeViewModel> children = new();

        public JsonNodeViewModel? Parent { get; }
        public bool HasKey => !string.IsNullOrEmpty(Key) && Key != "root";

        public JsonNodeViewModel(string key, JsonNodeViewModel? parent)
        {
            Key = key;
            Parent = parent;
        }
    }

    /// <summary>
    /// Types of JSON values
    /// </summary>
    public enum JsonValueType
    {
        Object,
        Array,
        String,
        Number,
        Boolean,
        Null
    }

    /// <summary>
    /// Information about a log file
    /// </summary>
    public class LogFileInfo
    {
        public string FilePath { get; }
        public string FileName { get; }
        public string FileSize { get; }
        public string LastModified { get; }
        public DateTime LastModifiedDate { get; }
        public string FileType { get; }

        public LogFileInfo(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            
            var fileInfo = new FileInfo(filePath);
            LastModifiedDate = fileInfo.LastWriteTime;
            LastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Format file size
            long bytes = fileInfo.Length;
            if (bytes < 1024)
                FileSize = $"{bytes} B";
            else if (bytes < 1024 * 1024)
                FileSize = $"{bytes / 1024:F1} KB";
            else
                FileSize = $"{bytes / (1024 * 1024):F1} MB";
            
            FileType = Path.GetExtension(filePath).ToUpperInvariant();
        }
    }
}