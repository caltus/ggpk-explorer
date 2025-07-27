# Project Structure

## Root Directory Layout

```
/
├── .gitignore                 # Git ignore rules for WinUI 3/.NET 8 project
├── .kiro/                     # Kiro IDE configuration
│   ├── settings/              # IDE settings and MCP configuration
│   ├── specs/                 # Feature specifications
│   └── steering/              # Project steering rules (this file)
├── docs/                      # Project documentation
│   ├── LibGGPK3_Deep_Research_Report.md
│   └── libggpk3_report.md
└── libs/                      # Auto-generated library dependencies (not in git)
    ├── LibGGPK3.dll           # Core GGPK file handling (auto-compiled)
    ├── LibBundle3.dll         # Bundle file operations (auto-compiled)
    ├── LibBundledGGPK3.dll    # Unified GGPK+Bundle access (auto-compiled)
    ├── SystemExtensions.dll   # System extensions (auto-compiled)
    ├── oo2core.dll            # Oodle compression (manual copy required)
    └── README.md              # Setup instructions (version controlled)
```

## Application Structure (To Be Created)

### Recommended Project Layout

```
src/
├── GGPKExplorer/              # Main WPF application project
│   ├── App.xaml               # Application definition with WPF-UI resources
│   ├── App.xaml.cs            # Application code-behind
│   ├── MainWindow.xaml        # Main FluentWindow
│   ├── MainWindow.xaml.cs     # Main window code-behind
│   ├── app.config             # Application configuration
│   ├── Views/                 # XAML views and user controls
│   │   ├── ExplorerView.xaml
│   │   ├── NavigationTreeView.xaml
│   │   ├── FileListView.xaml
│   │   └── Dialogs/
│   │       ├── PropertiesDialog.xaml
│   │       └── SettingsDialog.xaml
│   ├── ViewModels/            # MVVM view models
│   │   ├── MainViewModel.cs
│   │   ├── ExplorerViewModel.cs
│   │   ├── NavigationTreeViewModel.cs
│   │   └── FileListViewModel.cs
│   ├── Models/                # Data models and DTOs
│   │   ├── TreeNodeInfo.cs
│   │   ├── FileListItem.cs
│   │   └── ViewSettings.cs
│   ├── Services/              # Business logic services
│   │   ├── GGPKService.cs
│   │   ├── FileOperationsService.cs
│   │   ├── SettingsService.cs
│   │   └── Context7Service.cs
│   ├── Wrappers/              # LibGGPK3 integration layer
│   │   ├── GGPKWrapper.cs
│   │   └── IndexDecompressor.cs
│   ├── Converters/            # XAML value converters
│   │   ├── FileSizeConverter.cs
│   │   └── FileTypeIconConverter.cs
│   ├── Helpers/               # Utility classes
│   │   ├── FileTypeHelper.cs
│   │   └── IconHelper.cs
│   ├── Resources/             # Application resources
│   │   ├── Styles/
│   │   ├── Icons/
│   │   └── Strings/
│   └── Assets/                # Static assets (icons, images)
├── GGPKExplorer.Tests/        # Unit tests project
│   ├── ViewModels/
│   ├── Services/
│   └── Helpers/
└── GGPKExplorer.IntegrationTests/  # Integration tests
    ├── GGPKLoadingTests.cs
    └── FileOperationsTests.cs
```

## Library Integration

### Library Integration Approach
- **Auto-Generated Dependencies**: All DLLs are downloaded and compiled by setup scripts
- **No Source Code in Repository**: Library source code is not stored in the repository
- **Reference as DLL Files**: Project references compiled DLL files in libs/ folder
- **Copy to Output Directory**: Build system copies all DLLs to output directory
- **Version Control Exclusion**: All DLL files are excluded via .gitignore

### Dependency Setup Process
1. **Setup Scripts**: `.\scripts\Setup-All-Dependencies.ps1` downloads and compiles libraries
2. **Temporary Compilation**: Source code is downloaded to tmp/ folder, compiled, then cleaned up
3. **DLL Placement**: Compiled DLLs are placed in libs/ folder
4. **Manual oo2core.dll**: Must be manually copied from Path of Exile installation
5. **Build Integration**: MSBuild copies DLLs from libs/ to output directory

## Configuration Files

### Project Configuration
- `*.csproj` - MSBuild project files with WPF and WPF-UI NuGet references
- `app.config` - Application configuration and settings
- `Settings.settings` - User preferences and application settings

### IDE Configuration
- `.kiro/settings/mcp.json` - Model Context Protocol configuration for Context7
- `.kiro/specs/` - Feature specifications and requirements
- `.kiro/steering/` - Project steering rules and conventions

## Naming Conventions

### Files and Directories
- **PascalCase** for class files, XAML files, and directories
- **camelCase** for private fields and local variables
- **UPPERCASE** for constants and static readonly fields

### Namespaces
```csharp
GGPKExplorer                    // Root namespace
GGPKExplorer.Views              // UI views and controls
GGPKExplorer.ViewModels         // MVVM view models
GGPKExplorer.Services           // Business logic services
GGPKExplorer.Models             // Data models
GGPKExplorer.Wrappers           // Library integration wrappers
GGPKExplorer.Helpers            // Utility classes
```

## Build Artifacts

### Output Structure
```
bin/
├── Debug/                     # Debug build outputs
│   └── net8.0-windows/
│       ├── GGPKExplorer.exe
│       ├── LibGGPK3.dll      # Copied from libs/
│       ├── LibBundle3.dll    # Copied from libs/
│       ├── LibBundledGGPK3.dll # Copied from libs/
│       ├── SystemExtensions.dll # Copied from libs/
│       ├── oo2core.dll       # Copied from libs/
│       └── *.dll             # NuGet dependencies
└── Release/                   # Release build outputs
    └── net8.0-windows/
        ├── GGPKExplorer.exe
        ├── [All DLLs copied from libs/]
        └── Assets/
```

### Package Structure (Deployment)
```
Application/
├── GGPKExplorer.exe          # Main executable
├── WPF-UI.dll                # WPF-UI library
├── oo2core.dll               # Oodle compression
├── SystemExtensions.dll      # System extensions
├── Assets/                   # Application assets
└── GGPKExplorer.exe.config   # Application configuration
```

## Development Workflow

### File Organization Principles
1. **Separation of Concerns** - Keep UI, business logic, and data access separate
2. **Single Responsibility** - Each class should have one clear purpose
3. **Dependency Injection** - Use DI container for service management
4. **MVVM Pattern** - Maintain clear separation between View and ViewModel
5. **Resource Management** - Proper disposal of GGPK resources and streams

### Code Organization
- Group related functionality in the same namespace/folder
- Keep XAML and code-behind minimal, put logic in ViewModels
- Use partial classes for large ViewModels when appropriate
- Separate interface definitions from implementations