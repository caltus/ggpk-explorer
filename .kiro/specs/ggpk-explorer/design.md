# Design Document

## Overview

The GGPK Explorer is a Windows Explorer-style file browser application built with C# WPF and WPF-UI library that provides intuitive navigation and exploration of Path of Exile's GGPK (Game Game Pack) files. The application leverages the LibGGPK3 library ecosystem to handle both traditional GGPK files and modern bundle-based assets, with special emphasis on single-threaded operations for data integrity.

The design follows the familiar dual-pane Windows Explorer layout with a TreeView for hierarchical navigation on the left and a detailed ListView for file contents on the right. The application uses WPF-UI's Fluent Design components to provide a modern Windows 11-style interface while maintaining the responsive user experience expected from contemporary Windows applications.

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    WPF + WPF-UI Application                │
├─────────────────────────────────────────────────────────────┤
│  Presentation Layer (MVVM)                                 │
│  ├── Views (XAML with WPF-UI Controls)                     │
│  ├── ViewModels (CommunityToolkit.Mvvm)                    │
│  └── Converters & Behaviors                                │
├─────────────────────────────────────────────────────────────┤
│  Business Logic Layer                                      │
│  ├── GGPK Service (Single-threaded)                       │
│  ├── File Operations Service                               │
│  ├── Search Service                                        │
│  └── Settings Service                                      │
├─────────────────────────────────────────────────────────────┤
│  Data Access Layer                                         │
│  ├── LibGGPK3 Wrapper                                      │
│  ├── LibBundle3 Wrapper                                    │
│  └── LibBundledGGPK3 Wrapper                              │
├─────────────────────────────────────────────────────────────┤
│  External Dependencies                                      │
│  ├── WPF-UI (Fluent Design Controls)                       │
│  ├── LibGGPK3 (libs/LibGGPK3)                             │
│  ├── LibBundle3 (libs/LibBundle3)                         │
│  ├── LibBundledGGPK3 (libs/LibBundledGGPK3)               │
│  └── oo2core.dll (Oodle compression)                      │
└─────────────────────────────────────────────────────────────┘
```

### Threading Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   UI Thread     │    │  GGPK Thread    │    │ Background      │
│  (.NET 8 STA)   │    │  (Single)       │    │ Threads         │
├─────────────────┤    ├─────────────────┤    ├─────────────────┤
│ • UI Updates    │◄──►│ • GGPK Ops      │    │ • File I/O      │
│ • User Input    │    │ • Bundle Ops    │    │ • Settings      │
│ • WinUI 3       │    │ • Index Parsing │    │ • Logging       │
│ • Events        │    │ • Decompression │    │ • Async Ops     │
│ • MVVM Binding  │    │ • oo2core.dll   │    │                 │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## Components and Interfaces

### Core Components

#### 1. MainWindow (WPF-UI FluentWindow)
**Purpose**: Primary application window hosting the dual-pane explorer interface
**Key Features**:
- WPF-UI FluentWindow with modern Fluent Design styling
- TitleBar control with file path display and window controls
- Menu bar using WPF-UI NavigationView or traditional Menu
- Status bar showing file count, selection info, and operation progress using StatusBar
- Keyboard shortcuts and accessibility support

#### 2. ExplorerView (UserControl)
**Purpose**: Main dual-pane explorer interface
**Components**:
- `NavigationTreeView`: Left pane hierarchical navigation using WPF TreeView
- `FileListView`: Right pane detailed file listing using WPF ListView with GridView
- `GridSplitter`: Resizable divider between panes
- `AutoSuggestBox`: Integrated search functionality using WPF-UI control

#### 3. NavigationTreeView (WPF TreeView)
**Purpose**: Hierarchical navigation of GGPK structure
**Key Features**:
- Lazy loading of directory nodes
- Custom TreeViewItem styling with WPF-UI SymbolIcon and FontIcon
- Context menu support using ContextMenu
- Keyboard navigation
- Drag & drop support

#### 4. FileListView (WPF ListView)
**Purpose**: Detailed file listing with multiple view modes
**View Modes**:
- Details (default): GridView with Name, Size, Type, Modified, Compression columns
- Large Icons: File type icons with names using WPF-UI SymbolIcon
- Small Icons: Compact icon view
- List: Simple name listing

#### 5. GGPK Services Layer

##### GGPKService (Singleton)
```csharp
public sealed class GGPKService : IDisposable
{
    private readonly SemaphoreSlim _operationSemaphore = new(1, 1);
    private readonly Thread _ggpkThread;
    private readonly ConcurrentQueue<GGPKOperation> _operationQueue;
    private BundledGGPK _currentGGPK;
    private CancellationTokenSource _cancellationTokenSource;
    
    public event EventHandler<GGPKLoadedEventArgs> GGPKLoaded;
    public event EventHandler<ProgressEventArgs> ProgressChanged;
    public event EventHandler<ErrorEventArgs> ErrorOccurred;
    
    public Task<bool> OpenGGPKAsync(string filePath);
    public Task<IEnumerable<TreeNodeInfo>> GetChildrenAsync(string path);
    public Task<FileInfo> GetFileInfoAsync(string path);
    public Task<byte[]> ReadFileAsync(string path);
    public Task<bool> ExtractFileAsync(string sourcePath, string destinationPath);
    public Task<bool> ExtractDirectoryAsync(string sourcePath, string destinationPath);
    public void CancelCurrentOperation();
}
```

##### FileOperationsService
```csharp
public class FileOperationsService
{
    public Task<bool> ExtractToAsync(IEnumerable<string> sourcePaths, string destinationPath, IProgress<ProgressInfo> progress);
    public Task<SearchResults> SearchAsync(string query, SearchOptions options);
    public Task<FileProperties> GetPropertiesAsync(string path);
    public bool CanExtract(string path);
    public string GetFileTypeDescription(string extension);
}
```

### Data Models

#### TreeNodeInfo
```csharp
public class TreeNodeInfo
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public NodeType Type { get; set; } // Directory, File, Bundle
    public long Size { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public CompressionInfo Compression { get; set; }
    public bool HasChildren { get; set; }
    public string IconPath { get; set; }
}

public enum NodeType
{
    Directory,
    File,
    BundleFile,
    CompressedFile
}
```

#### FileListItem
```csharp
public class FileListItem : INotifyPropertyChanged
{
    public string Name { get; set; }
    public string FullPath { get; set; }
    public NodeType Type { get; set; }
    public string SizeFormatted { get; set; }
    public long SizeBytes { get; set; }
    public string TypeDescription { get; set; }
    public DateTime? ModifiedDate { get; set; }
    public string CompressionInfo { get; set; }
    public BitmapImage Icon { get; set; }
    public bool IsSelected { get; set; }
}
```

### ViewModels

#### MainViewModel
```csharp
public partial class MainViewModel : ObservableObject
{
    private readonly GGPKService _ggpkService;
    private readonly FileOperationsService _fileOperationsService;
    private readonly SettingsService _settingsService;
    
    public ObservableCollection<string> RecentFiles { get; }
    
    [ObservableProperty]
    private string currentFilePath;
    
    [ObservableProperty]
    private string statusText;
    
    [ObservableProperty]
    private bool isLoading;
    
    [ObservableProperty]
    private double progressValue;
    
    [RelayCommand]
    private async Task OpenFileAsync();
    
    [RelayCommand]
    private void CloseFile();
    
    [RelayCommand]
    private void Exit();
    
    [RelayCommand]
    private void ShowAbout();
}
```

#### ExplorerViewModel
```csharp
public partial class ExplorerViewModel : ObservableObject
{
    public NavigationTreeViewModel TreeViewModel { get; }
    public FileListViewModel ListViewModel { get; }
    public SearchViewModel SearchViewModel { get; }
    
    [ObservableProperty]
    private string currentPath;
    
    [ObservableProperty]
    private bool isSplitViewOpen;
    
    [ObservableProperty]
    private double splitterPosition;
    
    [RelayCommand]
    private async Task NavigateToAsync(string path);
    
    [RelayCommand]
    private async Task RefreshAsync();
    
    [RelayCommand]
    private void GoBack();
    
    [RelayCommand]
    private void GoForward();
}
```

#### NavigationTreeViewModel
```csharp
public class NavigationTreeViewModel : ObservableObject
{
    private readonly GGPKService _ggpkService;
    
    public ObservableCollection<TreeNodeViewModel> RootNodes { get; }
    public TreeNodeViewModel SelectedNode { get; set; }
    
    public ICommand ExpandNodeCommand { get; }
    public ICommand SelectNodeCommand { get; }
    public ICommand RefreshNodeCommand { get; }
}

public class TreeNodeViewModel : ObservableObject
{
    public TreeNodeInfo NodeInfo { get; }
    public ObservableCollection<TreeNodeViewModel> Children { get; }
    public bool IsExpanded { get; set; }
    public bool IsSelected { get; set; }
    public bool IsLoading { get; set; }
    public BitmapImage Icon { get; set; }
    
    public ICommand ExpandCommand { get; }
    public ICommand ExtractCommand { get; }
    public ICommand PropertiesCommand { get; }
}
```

#### FileListViewModel
```csharp
public class FileListViewModel : ObservableObject
{
    private readonly GGPKService _ggpkService;
    private readonly FileOperationsService _fileOperationsService;
    
    public ObservableCollection<FileListItem> Items { get; }
    public ICollectionView ItemsView { get; }
    public FileListItem SelectedItem { get; set; }
    public IList<FileListItem> SelectedItems { get; }
    
    public ViewMode CurrentViewMode { get; set; }
    public string SortColumn { get; set; }
    public ListSortDirection SortDirection { get; set; }
    
    public ICommand ItemDoubleClickCommand { get; }
    public ICommand ExtractSelectedCommand { get; }
    public ICommand PropertiesCommand { get; }
    public ICommand SelectAllCommand { get; }
}
```

## Data Models

### GGPK Integration Models

#### GGPKWrapper
```csharp
public class GGPKWrapper : IDisposable
{
    private BundledGGPK _bundledGGPK;
    private GGPK _ggpk;
    private readonly object _lockObject = new object();
    
    public bool IsBundled { get; private set; }
    public uint Version { get; private set; }
    public DirectoryRecord Root { get; private set; }
    
    public bool Open(string filePath);
    public IEnumerable<TreeNode> GetChildren(DirectoryRecord directory);
    public byte[] ReadFile(FileRecord file);
    public FileRecord FindFile(string path);
    public DirectoryRecord FindDirectory(string path);
}
```

#### IndexDecompressor
```csharp
public class IndexDecompressor
{
    private readonly GGPKWrapper _ggpkWrapper;
    
    public TreeNodeCollection DecompressIndex();
    public bool HasIndexFile();
    public TreeNode ParseIndexEntry(byte[] data);
}
```

### UI Models

#### SearchOptions
```csharp
public class SearchOptions
{
    public string Query { get; set; }
    public bool MatchCase { get; set; }
    public bool UseRegex { get; set; }
    public SearchScope Scope { get; set; } // CurrentDirectory, AllDirectories
    public FileTypeFilter TypeFilter { get; set; }
}

public enum SearchScope
{
    CurrentDirectory,
    AllDirectories
}
```

#### ViewSettings
```csharp
public class ViewSettings
{
    public ViewMode ViewMode { get; set; } = ViewMode.Details;
    public bool ShowHiddenFiles { get; set; } = false;
    public bool ShowFileExtensions { get; set; } = true;
    public SortSettings SortSettings { get; set; }
    public ColumnSettings ColumnSettings { get; set; }
}

public enum ViewMode
{
    Details,
    LargeIcons,
    SmallIcons,
    List
}
```

## Error Handling

### Exception Hierarchy
```csharp
public class GGPKException : Exception
{
    public GGPKException(string message) : base(message) { }
    public GGPKException(string message, Exception innerException) : base(message, innerException) { }
}

public class GGPKCorruptedException : GGPKException
{
    public long CorruptedOffset { get; }
    public GGPKCorruptedException(string message, long offset) : base(message) 
    {
        CorruptedOffset = offset;
    }
}

public class BundleDecompressionException : GGPKException
{
    public string BundleName { get; }
    public BundleDecompressionException(string bundleName, string message) : base(message)
    {
        BundleName = bundleName;
    }
}
```

### Error Handling Strategy
1. **UI Level**: Display user-friendly error messages with recovery options
2. **Service Level**: Log detailed errors and attempt graceful degradation
3. **Data Level**: Validate inputs and handle LibGGPK3 exceptions
4. **Global Handler**: Catch unhandled exceptions and provide crash reporting

### Error Recovery Mechanisms
- **File Corruption**: Attempt to read available portions and mark corrupted sections
- **Bundle Errors**: Fall back to GGPK-only mode if bundle decompression fails
- **Memory Issues**: Implement memory pressure handling and cleanup
- **Threading Issues**: Ensure proper synchronization and deadlock prevention

## Testing Strategy

### Unit Testing
- **ViewModels**: Test business logic, commands, and property changes
- **Services**: Mock dependencies and test core functionality
- **Data Models**: Validate data transformation and validation logic
- **Utilities**: Test helper functions and extension methods

### Integration Testing
- **GGPK Loading**: Test with various GGPK file formats and versions
- **Bundle Processing**: Verify bundle decompression and integration
- **File Operations**: Test extraction, search, and property retrieval
- **UI Integration**: Test ViewModel-View interactions

### Performance Testing
- **Large Files**: Test with multi-gigabyte GGPK files
- **Memory Usage**: Monitor memory consumption during operations
- **UI Responsiveness**: Ensure UI remains responsive during long operations
- **Threading**: Verify single-threaded GGPK operations work correctly

### User Acceptance Testing
- **Usability**: Verify Windows Explorer-like behavior
- **Accessibility**: Test keyboard navigation and screen reader support
- **Error Scenarios**: Test user experience with corrupted files
- **Performance**: Validate acceptable response times

## Performance Considerations

### Memory Management
- **Lazy Loading**: Load directory contents only when needed
- **Virtualization**: Use ListView virtualization for large directories
- **Disposal**: Proper disposal of GGPK resources and streams
- **Caching**: Smart caching of frequently accessed data

### UI Performance
- **Background Loading**: Perform GGPK operations on dedicated thread
- **Progress Reporting**: Provide visual feedback for long operations
- **Cancellation**: Allow users to cancel long-running operations
- **Debouncing**: Debounce search input and UI updates

### File System Performance
- **Streaming**: Use streaming for large file operations
- **Compression**: Leverage oo2core.dll efficiently
- **Batch Operations**: Batch multiple file operations when possible
- **Resource Pooling**: Pool expensive resources like decompression contexts

## Security Considerations

### File Access Security
- **Path Validation**: Validate all file paths to prevent directory traversal
- **Permission Checks**: Verify file system permissions before operations
- **Sandboxing**: Limit file system access to necessary directories
- **Input Sanitization**: Sanitize all user inputs and file names

### Memory Security
- **Buffer Overflows**: Use safe memory operations and bounds checking
- **Sensitive Data**: Clear sensitive data from memory after use
- **Exception Safety**: Ensure exceptions don't leak sensitive information
- **Resource Limits**: Implement limits to prevent resource exhaustion

### External Dependencies
- **DLL Loading**: Verify oo2core.dll integrity and source
- **Update Mechanism**: Secure update process for library dependencies
- **Logging**: Avoid logging sensitive information

This design provides a solid foundation for building a Windows Explorer-style GGPK file explorer that meets all the specified requirements while maintaining good architectural principles, performance, and user experience.