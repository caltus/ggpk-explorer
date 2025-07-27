# Technology Stack

## Framework & Platform

- **Target Framework**: .NET 8
- **UI Framework**: WPF with WPF-UI library (Fluent Design)
- **Platform**: Windows 10/11 (x64)
- **Package Manager**: NuGet
- **Architecture Pattern**: MVVM with CommunityToolkit.Mvvm

## Core Dependencies

### UI & MVVM
- `WPF-UI` - Modern Fluent Design controls for WPF
- `CommunityToolkit.Mvvm` - MVVM helpers, ObservableProperty, RelayCommand
- `Microsoft.Extensions.DependencyInjection` - Dependency injection container

### GGPK Libraries (Auto-Generated Dependencies)
- `LibGGPK3.dll` - Core GGPK file handling (auto-compiled from GitHub)
- `LibBundle3.dll` - Bundle file operations (auto-compiled from GitHub)
- `LibBundledGGPK3.dll` - Unified GGPK+Bundle access (auto-compiled from GitHub)

**Documentation Resources:**
- `docs/LibGGPK3_Deep_Research_Report.md` - Comprehensive technical analysis
- `docs/libggpk3_report.md` - User-friendly implementation guide
- **Context7 Integration** - Query for up-to-date LibGGPK3 API documentation
- See `.kiro/steering/context7-libggpk3.md` for detailed usage guidelines

### Native Dependencies (Auto-Generated)
- `oo2core.dll` - Oodle compression library (manual copy from Path of Exile required)
- `SystemExtensions.dll` - System extensions library (auto-compiled from GitHub)

**Setup**: All dependencies except oo2core.dll are automatically downloaded and compiled by `.\scripts\Setup-All-Dependencies.ps1`

## Build System

### Project Configuration
- **Project Type**: WPF Application (.NET 8)
- **Target OS**: Windows 10 or higher
- **Package Type**: ClickOnce or MSI installer
- **Threading Model**: Single-threaded apartment (STA)

### Common Build Commands

```powershell
# Setup dependencies (required before first build)
.\scripts\Setup-All-Dependencies.ps1

# Verify dependencies are present
.\scripts\Verify-Dependencies.ps1

# Restore packages
dotnet restore

# Build solution
dotnet build

# Build for release
dotnet build -c Release

# Run application
dotnet run --project src\GGPKExplorer

# Create MSIX package
dotnet publish -c Release -r win-x64 --self-contained false

# Run tests
dotnet test
```

## Threading Architecture

### Critical Threading Rules
- **All GGPK operations MUST run on a single dedicated thread**
- **LibGGPK3 is NOT thread-safe** - documented limitation
- Use `SemaphoreSlim` to ensure sequential GGPK access
- Marshal UI updates back to UI thread using `DispatcherQueue`
- Never access GGPK objects from multiple threads simultaneously
- **Reference**: docs/LibGGPK3_Deep_Research_Report.md - Thread Safety section

### Threading Pattern
```csharp
// Correct pattern for GGPK operations
private readonly SemaphoreSlim _ggpkSemaphore = new(1, 1);

public async Task<T> ExecuteGGPKOperationAsync<T>(Func<T> operation)
{
    await _ggpkSemaphore.WaitAsync();
    try
    {
        return await Task.Run(operation);
    }
    finally
    {
        _ggpkSemaphore.Release();
    }
}
```

## Performance Considerations

### Memory Management
- Use `IDisposable` pattern for GGPK resources (documented requirement)
- Implement lazy loading for large directory structures
- Use `Span<T>` for efficient memory operations (LibGGPK3 supports this)
- Enable ListView virtualization for large file lists
- **Always use `using` statements** for GGPK objects
- **Reference**: docs/libggpk3_report.md - Integration Best Practices

### File Operations
- Stream-based reading for large files
- Progress reporting for long-running operations
- Cancellation token support for user-initiated cancellations
- Batch operations where possible

## Error Handling Strategy

### Exception Hierarchy
- `GGPKException` - Base exception for GGPK operations
- `GGPKCorruptedException` - File corruption errors
- `BundleDecompressionException` - Bundle processing errors

### Error Recovery
- Graceful degradation when bundle decompression fails
- Partial file reading for corrupted GGPK files
- User-friendly error messages with recovery options

## Development Tools

### Recommended IDE
- Visual Studio 2022 (17.8+) with Windows App SDK workload
- Or Visual Studio Code with C# extension

### Debugging
- Use WinUI 3 debugging tools
- Enable native code debugging for oo2core.dll issues
- Use Application Insights for telemetry (optional)

## Documentation and Context7 Integration

### Context7 Usage
- **Query Context7** for up-to-date library documentation
- **Consult before implementing** any GGPK-related functionality
- **Reference in code comments** when using Context7 insights
- **Include in commit messages** when Context7 was consulted

### LibGGPK3 Implementation Requirements
1. **Always consult documentation** before implementing GGPK operations:
   - `docs/LibGGPK3_Deep_Research_Report.md` for technical details
   - `docs/libggpk3_report.md` for implementation patterns
   - Context7 queries for latest API documentation
2. **Follow documented patterns** for file operations, error handling, and resource management
3. **Include documentation references** in code comments and commit messages
4. **Validate against documentation** before committing GGPK-related code

### Code Documentation Standards
```csharp
/// <summary>
/// Implementation following LibGGPK3 documented patterns
/// Reference: docs/LibGGPK3_Deep_Research_Report.md - [Section]
/// Context7: [Query used for additional documentation]
/// </summary>
```

## Deployment

### Application Packaging
- Package using ClickOnce or MSI installer
- Include native DLLs in application directory
- Configure file associations for .ggpk files
- Support direct installation and auto-updates