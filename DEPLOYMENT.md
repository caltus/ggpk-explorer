# GGPK Explorer Deployment Guide

This document provides instructions for building and deploying GGPK Explorer.

## Prerequisites

### Required Software
- .NET 8 SDK or later
- PowerShell 5.1 or PowerShell Core 6+
- Git (for downloading dependency source code)
- Windows 10/11 (for deployment testing)
- Path of Exile installation (for oo2core.dll)

### Optional Software (for MSI creation)
- WiX Toolset 3.11+ (for advanced MSI creation)
- Visual Studio 2022 (for ClickOnce publishing)

## Quick Start

### Simple Release Build
Run the provided batch file:
```batch
Build-Release.bat
```

This will:
1. Set up all required dependencies (download and compile libraries)
2. Clean previous builds
3. Restore NuGet packages
4. Build the solution in Release configuration
5. Publish the application
6. Create an MSI installer (if WiX is available)

### Manual Build Process

#### 1. Build Application
```powershell
# Navigate to project root
cd path\to\ggpk-explorer

# Setup dependencies first (required before first build)
.\scripts\Setup-All-Dependencies.ps1

# Run deployment script
.\scripts\Build-Deployment.ps1 -Configuration Release -Clean
```

#### 2. Build with MSI Installer
```powershell
.\scripts\Build-Deployment.ps1 -Configuration Release -Clean -BuildMSI
```

#### 3. Build with ClickOnce Deployment
```powershell
.\scripts\Build-Deployment.ps1 -Configuration Release -Clean -BuildClickOnce
```

## Deployment Options

### 1. Standalone Application
The published application in `publish\app\` can be distributed as a standalone package:

**Files included:**
- `GGPKExplorer.exe` - Main application
- `LibGGPK3.dll` - Core GGPK file handling
- `LibBundle3.dll` - Bundle file operations
- `LibBundledGGPK3.dll` - Unified GGPK+Bundle access
- `SystemExtensions.dll` - System extensions
- `oo2core.dll` - Oodle compression library
- Various .NET runtime libraries and NuGet packages
- Application configuration files

**Distribution:**
- Zip the entire `publish\app\` directory
- Users extract and run `GGPKExplorer.exe`
- Requires .NET 8 runtime to be installed on target system

### 2. MSI Installer
The MSI installer provides a professional installation experience:

**Features:**
- Automatic installation to Program Files
- Start Menu shortcuts
- Desktop shortcut (optional)
- File association for .ggpk files
- Proper uninstall support
- Windows Installer database integration

**Usage:**
```powershell
# Build MSI
.\scripts\Build-Deployment.ps1 -BuildMSI

# Test MSI
.\scripts\Test-Deployment.ps1 -TestMSI
```

### 3. ClickOnce Deployment
ClickOnce provides automatic updates and easy deployment:

**Features:**
- Automatic updates
- No administrator privileges required
- Isolated application storage
- Easy rollback to previous versions

**Usage:**
```powershell
# Build ClickOnce
.\scripts\Build-Deployment.ps1 -BuildClickOnce

# Test ClickOnce
.\scripts\Test-Deployment.ps1 -TestClickOnce
```

## File Associations

The deployment automatically configures file associations for `.ggpk` files:

- Double-clicking a `.ggpk` file opens it in GGPK Explorer
- Right-click context menu includes "Open with GGPK Explorer"
- Proper file type icons and descriptions

## Testing Deployment

Use the test script to validate deployment packages:

```powershell
# Test basic functionality
.\scripts\Test-Deployment.ps1

# Test all deployment types
.\scripts\Test-Deployment.ps1 -TestClickOnce -TestMSI -TestFileAssociations -Verbose
```

### Test Coverage
- Application startup and basic functionality
- Native library loading (oo2core.dll, SystemExtensions.dll)
- File associations (.ggpk files)
- ClickOnce manifest validation
- MSI package validation

## Troubleshooting

### Common Issues

#### Missing Native Libraries
**Problem:** Application fails to start with "DLL not found" error
**Solution:** 
- Run `.\scripts\Setup-All-Dependencies.ps1` to ensure all DLLs are present
- Manually copy `oo2core.dll` from Path of Exile installation if missing
- Check that the deployment script copied all DLLs from libs/ folder correctly
- Verify the libraries are not blocked by Windows security
- Use `.\scripts\Verify-Dependencies.ps1` to check dependency status

#### File Association Issues
**Problem:** .ggpk files don't open with GGPK Explorer
**Solution:**
- Run the MSI installer as administrator
- Manually register file associations using the installer
- Check Windows Default Apps settings

#### ClickOnce Security Issues
**Problem:** ClickOnce deployment blocked by security settings
**Solution:**
- Sign the ClickOnce manifest with a code signing certificate
- Configure trusted sites in Internet Explorer/Edge
- Use MSI deployment for enterprise environments

#### MSI Creation Fails
**Problem:** WiX Toolset not found or MSI build fails
**Solution:**
- Install WiX Toolset from https://wixtoolset.org/
- Add WiX bin directory to PATH environment variable
- Use Visual Studio Installer Projects as alternative

## Advanced Configuration

### Custom Deployment Settings
Edit `src/GGPKExplorer/Deployment/DeploymentConfig.cs` to customize:
- Application name and version
- Publisher information
- File associations
- Installation directories

### MSI Customization
Modify `src/GGPKExplorer/Deployment/InstallerBuilder.cs` to customize:
- Installation UI
- Custom actions
- Registry entries
- Shortcuts and file associations

### ClickOnce Configuration
Update project properties in `GGPKExplorer.csproj`:
- Update URL and settings
- Certificate configuration
- Minimum required version
- Update check frequency

## Distribution Checklist

Before distributing GGPK Explorer:

- [ ] Build and test in Release configuration
- [ ] Verify all native libraries are included
- [ ] Test on clean Windows system
- [ ] Validate file associations work correctly
- [ ] Check application startup and basic functionality
- [ ] Verify uninstall process works properly
- [ ] Test with different .ggpk file versions
- [ ] Validate error handling and user experience

## Support

For deployment issues:
1. Check the build output for specific error messages
2. Run the test script with `-Verbose` flag for detailed information
3. Verify all prerequisites are installed
4. Test on a clean Windows system to isolate environment issues

## Version History

- **1.0.0** - Initial deployment configuration
  - Standalone application deployment
  - MSI installer with file associations
  - ClickOnce deployment support
  - Automated testing scripts