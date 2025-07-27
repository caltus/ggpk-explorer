# Context7 and LibGGPK3 Documentation Guidelines

## Context7 Integration (IDE Development Assistance Only)

**Note**: Context7 is used exclusively for development assistance within the IDE. The GGPK Explorer application itself does not include Context7 integration features.

**Dependency Management**: All LibGGPK3 libraries are automatically downloaded and compiled by setup scripts. No library source code is stored in the repository.

### When to Use Context7 (During Development)
Context7 should be consulted during development for:
- **Library Documentation**: Getting up-to-date documentation for external libraries
- **API References**: Understanding method signatures and usage patterns
- **Best Practices**: Learning recommended approaches for specific technologies
- **Troubleshooting**: Resolving issues with third-party libraries

### Context7 Usage Pattern (Development Only)
When working with external libraries or needing documentation during development:

1. **Identify the Library**: Determine the exact library name and version
2. **Query Context7**: Use the Context7 service to get current documentation
3. **Apply Knowledge**: Integrate the documentation insights into your implementation
4. **Reference Sources**: Include Context7 findings in code comments when relevant

### Example Context7 Workflow (Development)
```csharp
// Before implementing GGPK operations, consult Context7 for LibGGPK3 documentation
// Context7 Query: "LibGGPK3 FileRecord read operations"
// Apply findings to ensure proper usage patterns
```

## LibGGPK3 Documentation Resources

### Primary Documentation Sources
When designing GGPK-related functionality, ALWAYS consult these resources in order:

1. **docs/LibGGPK3_Deep_Research_Report.md** - Comprehensive technical analysis
2. **docs/libggpk3_report.md** - User-friendly implementation guide
3. **Context7 LibGGPK3 queries** - Up-to-date API documentation

### LibGGPK3 Implementation Guidelines

#### File Reading Operations
```csharp
// ✅ Correct pattern based on documentation
using (var ggpk = new GGPK(filePath))
{
    var fileRecord = (FileRecord)ggpk.Root.Children
        .FirstOrDefault(x => x.Name == "targetfile.dat");
    
    if (fileRecord != null)
    {
        // Use documented read methods
        byte[] data = fileRecord.Read();
        // or for partial reads
        byte[] partialData = fileRecord.Read(100..500);
    }
}
```

#### Stream Management
```csharp
// ✅ Always use proper disposal patterns
using (var ggpk = new GGPK(stream, leaveOpen: false))
{
    // Operations here
    // Stream will be properly disposed
}
```

#### Thread Safety Considerations
```csharp
// ⚠️ IMPORTANT: LibGGPK3 is NOT thread-safe
// Always use single-threaded access or proper synchronization
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

### Bundle Operations
When working with bundle files, reference the documentation for:

#### Bundle File Structure
- Understand compression types (Kraken_6, Mermaid_A, Leviathan_C)
- Proper header parsing
- Chunk size calculations

#### BundledGGPK Usage
```csharp
// ✅ Unified access pattern for standalone clients
using (var bundledGgpk = new BundledGGPK(filePath, parsePathsInIndex: true))
{
    var index = bundledGgpk.Index;
    // Access both GGPK and bundle data
}
```

## Documentation Integration Workflow

### Before Implementing GGPK Features
1. **Read Documentation**: Review relevant sections in the research reports
2. **Query Context7**: Get latest API documentation for specific operations
3. **Plan Implementation**: Design based on documented patterns and best practices
4. **Code with References**: Include documentation references in code comments

### Code Documentation Standards
```csharp
/// <summary>
/// Reads file data from GGPK using LibGGPK3 FileRecord.Read() method
/// Reference: docs/LibGGPK3_Deep_Research_Report.md - FileRecord Class section
/// Context7: LibGGPK3 FileRecord read operations
/// </summary>
/// <param name="filePath">Path within GGPK structure</param>
/// <returns>File content as byte array</returns>
public async Task<byte[]> ReadGGPKFileAsync(string filePath)
{
    // Implementation following documented patterns
}
```

### Error Handling Patterns
Based on documentation, implement proper error handling:

```csharp
try
{
    var ggpk = new GGPK(filePath);
    // Operations
}
catch (FileNotFoundException ex)
{
    // Handle missing GGPK file - documented exception type
    throw new GGPKException("GGPK file not found", ex);
}
catch (ArgumentException ex)
{
    // Handle invalid arguments - documented exception type
    throw new GGPKException("Invalid GGPK operation parameters", ex);
}
```

## Performance Optimization Guidelines

### Memory Management
Following documentation recommendations:

```csharp
// ✅ Use Span<T> for efficient memory operations (documented pattern)
Span<byte> buffer = stackalloc byte[bufferSize];
fileRecord.Read(buffer);

// ✅ Avoid repeated file reads (performance best practice)
byte[] data = fileRecord.Read();
ProcessData(data);
AnalyzeData(data); // Reuse data instead of re-reading
```

### Stream Optimization
```csharp
// ✅ Optimize stream positioning (documented recommendation)
// Use documented methods for efficient file access
```

## Validation Requirements

### Before Committing GGPK-Related Code
1. **Documentation Compliance**: Verify implementation follows documented patterns
2. **Context7 Validation**: Confirm usage aligns with latest API documentation
3. **Error Handling**: Ensure proper exception handling as documented
4. **Resource Management**: Verify proper disposal patterns
5. **Thread Safety**: Confirm single-threaded access or proper synchronization

### Code Review Checklist
- [ ] Consulted docs/LibGGPK3_Deep_Research_Report.md for technical details
- [ ] Reviewed docs/libggpk3_report.md for implementation patterns
- [ ] Queried Context7 for latest API documentation
- [ ] Implemented proper error handling
- [ ] Used documented disposal patterns
- [ ] Considered thread safety implications
- [ ] Added appropriate code documentation references

## Integration with Development Workflow

### Task Implementation Process
1. **Research Phase**: Read documentation and query Context7
2. **Design Phase**: Plan implementation based on documented patterns
3. **Implementation Phase**: Code following documented best practices
4. **Validation Phase**: Verify against documentation requirements
5. **Commit Phase**: Include documentation references in commit messages

### Commit Message Enhancement
```
[TASK-XX] GGPK File Reading: Implement FileRecord operations

- Implemented file reading using LibGGPK3 FileRecord.Read() method
- Added proper disposal patterns following documentation guidelines
- Integrated thread-safe access using SemaphoreSlim
- Added error handling for documented exception types

Documentation References:
- docs/LibGGPK3_Deep_Research_Report.md - FileRecord Class section
- docs/libggpk3_report.md - Usage Patterns section
- Context7: LibGGPK3 FileRecord API documentation

Addresses: Requirements X.X, X.X
```

This approach ensures that all GGPK-related implementations are well-documented, follow best practices, and maintain consistency with the library's intended usage patterns.