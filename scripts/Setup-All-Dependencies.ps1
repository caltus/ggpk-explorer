#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Complete dependency setup script for GGPK Explorer.

.DESCRIPTION
    This script automatically sets up all required dependencies for GGPK Explorer:
    - Verifies existing dependencies first
    - Automatically clones and compiles LibGGPK3, LibBundle3, LibBundledGGPK3 if missing
    - Automatically clones and compiles SystemExtensions if missing
    - Provides guidance for oo2core.dll setup

.PARAMETER Configuration
    Build configuration for libraries (Debug or Release). Default is Release.

.PARAMETER Force
    Force re-setup of all dependencies even if they exist.

.PARAMETER SkipVerification
    Skip initial dependency verification.

.EXAMPLE
    .\scripts\Setup-All-Dependencies.ps1
    
.EXAMPLE
    .\scripts\Setup-All-Dependencies.ps1 -Configuration Debug -Force
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Force = $false,
    [switch]$SkipVerification = $false
)

# Colors for output
$ColorInfo = "Cyan"
$ColorSuccess = "Green"
$ColorWarning = "Yellow"
$ColorError = "Red"

function Write-Step {
    param([string]$Message)
    Write-Host "🔧 $Message" -ForegroundColor $ColorInfo
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ $Message" -ForegroundColor $ColorSuccess
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  $Message" -ForegroundColor $ColorWarning
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ $Message" -ForegroundColor $ColorError
}

function Test-ScriptExists {
    param([string]$ScriptPath)
    
    if (Test-Path $ScriptPath) {
        return $true
    } else {
        Write-Error "Required script not found: $ScriptPath"
        return $false
    }
}

function Invoke-SetupScript {
    param(
        [string]$ScriptPath,
        [string]$ScriptName,
        [string[]]$Arguments = @()
    )
    
    Write-Step "Running $ScriptName setup..."
    
    try {
        $result = & $ScriptPath @Arguments
        if ($LASTEXITCODE -eq 0) {
            Write-Success "$ScriptName setup completed successfully"
            return $true
        } else {
            Write-Error "$ScriptName setup failed with exit code $LASTEXITCODE"
            return $false
        }
    }
    catch {
        Write-Error "$ScriptName setup failed with exception: $($_.Exception.Message)"
        return $false
    }
}

function Test-Dependencies {
    param([switch]$Detailed = $false)
    
    Write-Step "Verifying existing dependencies..."
    
    # Define required files
    $requiredFiles = @{
        "libs/LibGGPK3.dll" = "Core GGPK file handling library"
        "libs/LibBundle3.dll" = "Bundle file operations library"
        "libs/LibBundledGGPK3.dll" = "Unified GGPK+Bundle access library"
        "libs/SystemExtensions.dll" = "SystemExtensions library"
        "libs/oo2core.dll" = "Oodle compression library"
    }

    $requiredDirectories = @{}

    $allPresent = $true
    $missingCount = 0
    $missingItems = @()

    # Check required files
    Write-Host "`n📁 Required Files:" -ForegroundColor Yellow
    foreach ($file in $requiredFiles.Keys) {
        $description = $requiredFiles[$file]
        if (Test-Path $file) {
            Write-Host "  ✅ $file" -ForegroundColor Green
            if ($Detailed) {
                $fileInfo = Get-Item $file
                Write-Host "     Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
                Write-Host "     Modified: $($fileInfo.LastWriteTime)" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ❌ $file - MISSING" -ForegroundColor Red
            Write-Host "     Description: $description" -ForegroundColor Gray
            $allPresent = $false
            $missingCount++
            $missingItems += $file
        }
    }

    # Check required directories
    Write-Host "`n📂 Required Directories:" -ForegroundColor Yellow
    foreach ($dir in $requiredDirectories.Keys) {
        $description = $requiredDirectories[$dir]
        if (Test-Path $dir -PathType Container) {
            Write-Host "  ✅ $dir" -ForegroundColor Green
            if ($Detailed) {
                $itemCount = (Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue).Count
                Write-Host "     Files: $itemCount" -ForegroundColor Gray
            }
        } else {
            Write-Host "  ❌ $dir - MISSING" -ForegroundColor Red
            Write-Host "     Description: $description" -ForegroundColor Gray
            $allPresent = $false
            $missingCount++
            $missingItems += $dir
        }
    }

    return @{
        AllPresent = $allPresent
        MissingCount = $missingCount
        MissingItems = $missingItems
    }
}

function Setup-LibGGPK3Libraries {
    param([string]$Configuration = "Release")
    
    Write-Step "Setting up LibGGPK3 libraries..."
    
    $tempDir = "tmp/LibGGPK3"
    $repoUrl = "https://github.com/aianlinb/LibGGPK3.git"
    
    try {
        # Ensure tmp directory exists
        if (-not (Test-Path "tmp")) {
            New-Item -ItemType Directory -Path "tmp" | Out-Null
        }
        
        # Remove existing temp directory if it exists
        if (Test-Path $tempDir) {
            Write-Warning "Removing existing temporary directory..."
            Remove-Item $tempDir -Recurse -Force
        }
        
        # Clone repository
        Write-Host "  📥 Cloning LibGGPK3 repository..." -ForegroundColor Gray
        git clone $repoUrl $tempDir 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to clone LibGGPK3 repository"
        }
        
        Push-Location $tempDir
        
        try {
            # Build projects
            $projects = @("LibGGPK3/LibGGPK3.csproj", "LibBundle3/LibBundle3.csproj", "LibBundledGGPK3/LibBundledGGPK3.csproj")
            
            foreach ($project in $projects) {
                $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
                Write-Host "  🔨 Building $projectName..." -ForegroundColor Gray
                
                dotnet build $project -c $Configuration --verbosity quiet
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to build $projectName"
                }
            }
            
            # Copy DLLs to libs folder - try different target frameworks
            $targetFrameworks = @("net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0")
            $projectDlls = @("LibGGPK3", "LibBundle3", "LibBundledGGPK3")
            
            foreach ($projectName in $projectDlls) {
                $dllCopied = $false
                
                foreach ($framework in $targetFrameworks) {
                    $sourcePath = "${projectName}/bin/$Configuration/$framework/${projectName}.dll"
                    if (Test-Path $sourcePath) {
                        $destPath = "../../libs/${projectName}.dll"
                        Copy-Item $sourcePath $destPath -Force
                        Write-Success "Copied ${projectName}.dll to libs folder (from $framework)"
                        $dllCopied = $true
                        break
                    }
                }
                
                if (-not $dllCopied) {
                    # List available files for debugging
                    $binPath = "${projectName}/bin/$Configuration"
                    if (Test-Path $binPath) {
                        $availableFrameworks = Get-ChildItem $binPath -Directory | Select-Object -ExpandProperty Name
                        Write-Warning "Available target frameworks for ${projectName}: $($availableFrameworks -join ', ')"
                        
                        # Try to find any DLL in any framework
                        $foundDll = Get-ChildItem "$binPath" -Recurse -Filter "${projectName}.dll" | Select-Object -First 1
                        if ($foundDll) {
                            Copy-Item $foundDll.FullName "../../libs/${projectName}.dll" -Force
                            Write-Success "Copied ${projectName}.dll to libs folder (from $($foundDll.Directory.Name))"
                            $dllCopied = $true
                        }
                    }
                    
                    if (-not $dllCopied) {
                        throw "Built DLL not found for ${projectName} in any target framework"
                    }
                }
            }
            
        } finally {
            Pop-Location
        }
        
        # Cleanup
        Remove-Item $tempDir -Recurse -Force
        Write-Success "LibGGPK3 libraries setup completed"
        return $true
        
    } catch {
        Write-Error "Failed to setup LibGGPK3 libraries: $($_.Exception.Message)"
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        return $false
    }
}

function Setup-SystemExtensions {
    param([string]$Configuration = "Release")
    
    Write-Step "Setting up SystemExtensions..."
    
    $tempDir = "tmp/SystemExtensions"
    $repoUrl = "https://github.com/aianlinb/SystemExtensions.git"
    
    try {
        # Ensure tmp directory exists
        if (-not (Test-Path "tmp")) {
            New-Item -ItemType Directory -Path "tmp" | Out-Null
        }
        
        # Remove existing temp directory if it exists
        if (Test-Path $tempDir) {
            Write-Warning "Removing existing temporary SystemExtensions directory..."
            Remove-Item $tempDir -Recurse -Force
        }
        
        # Clone repository
        Write-Host "  📥 Cloning SystemExtensions repository..." -ForegroundColor Gray
        git clone $repoUrl $tempDir 2>&1 | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to clone SystemExtensions repository"
        }
        
        Push-Location $tempDir
        
        try {
            # Find the SystemExtensions project file
            $projectFiles = Get-ChildItem -Recurse -Filter "SystemExtensions.csproj" | Select-Object -First 1
            if (-not $projectFiles) {
                throw "SystemExtensions.csproj not found in repository"
            }
            
            $projectPath = $projectFiles.FullName
            $projectDir = $projectFiles.Directory.FullName
            
            Write-Host "  🔨 Building SystemExtensions..." -ForegroundColor Gray
            
            # Build the project without using our Directory.Build.props
            Write-Host "    Building project: $projectPath" -ForegroundColor Gray
            $buildOutput = dotnet build $projectPath -c $Configuration --verbosity normal -p:ImportDirectoryBuildProps=false 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Build output:" -ForegroundColor Red
                Write-Host $buildOutput -ForegroundColor Red
                throw "Failed to build SystemExtensions"
            }
            
            # Find and copy the built DLL
            $targetFrameworks = @("net8.0", "net7.0", "net6.0", "netstandard2.1", "netstandard2.0")
            $dllCopied = $false
            
            foreach ($framework in $targetFrameworks) {
                $sourcePath = Join-Path $projectDir "bin/$Configuration/$framework/SystemExtensions.dll"
                if (Test-Path $sourcePath) {
                    $destPath = "../../libs/SystemExtensions.dll"
                    Copy-Item $sourcePath $destPath -Force
                    Write-Success "Copied SystemExtensions.dll to libs folder (from $framework)"
                    $dllCopied = $true
                    break
                }
            }
            
            if (-not $dllCopied) {
                # Try to find any SystemExtensions.dll in any framework
                $binPath = Join-Path $projectDir "bin/$Configuration"
                if (Test-Path $binPath) {
                    $availableFrameworks = Get-ChildItem $binPath -Directory | Select-Object -ExpandProperty Name
                    Write-Warning "Available target frameworks for SystemExtensions: $($availableFrameworks -join ', ')"
                    
                    $foundDll = Get-ChildItem $binPath -Recurse -Filter "SystemExtensions.dll" | Select-Object -First 1
                    if ($foundDll) {
                        Copy-Item $foundDll.FullName "../../libs/SystemExtensions.dll" -Force
                        Write-Success "Copied SystemExtensions.dll to libs folder (from $($foundDll.Directory.Name))"
                        $dllCopied = $true
                    }
                }
                
                if (-not $dllCopied) {
                    throw "Built SystemExtensions.dll not found in any target framework"
                }
            }
            
        } finally {
            Pop-Location
        }
        
        # Cleanup
        Remove-Item $tempDir -Recurse -Force
        Write-Success "SystemExtensions setup completed"
        return $true
        
    } catch {
        Write-Error "Failed to setup SystemExtensions: $($_.Exception.Message)"
        if (Test-Path $tempDir) {
            Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        return $false
    }
}

function Show-Oo2CoreInstructions {
    Write-Host "`n" + "=" * 60 -ForegroundColor $ColorInfo
    Write-Host "📋 oo2core.dll Setup Instructions" -ForegroundColor $ColorInfo
    Write-Host "=" * 60 -ForegroundColor $ColorInfo
    
    Write-Host "`n🎯 You need to manually copy oo2core.dll from your Path of Exile installation:" -ForegroundColor $ColorWarning
    
    Write-Host "`n📂 Typical Path of Exile locations:" -ForegroundColor $ColorInfo
    Write-Host "  • Steam: C:\Program Files (x86)\Steam\steamapps\common\Path of Exile\" -ForegroundColor Gray
    Write-Host "  • Standalone: C:\Program Files (x86)\Grinding Gear Games\Path of Exile\" -ForegroundColor Gray
    Write-Host "  • Epic Games: C:\Program Files\Epic Games\PathOfExile\" -ForegroundColor Gray
    
    Write-Host "`n💻 PowerShell commands to copy oo2core.dll:" -ForegroundColor $ColorInfo
    Write-Host "  # For Steam installation:" -ForegroundColor Gray
    Write-Host '  Copy-Item "C:\Program Files (x86)\Steam\steamapps\common\Path of Exile\oo2core_8_win64.dll" libs\oo2core.dll' -ForegroundColor Yellow
    
    Write-Host "`n  # For Standalone installation:" -ForegroundColor Gray
    Write-Host '  Copy-Item "C:\Program Files (x86)\Grinding Gear Games\Path of Exile\oo2core_8_win64.dll" libs\oo2core.dll' -ForegroundColor Yellow
    
    Write-Host "`n  # Or search for it:" -ForegroundColor Gray
    Write-Host '  Get-ChildItem -Path "C:\" -Name "oo2core_8_win64.dll" -Recurse -ErrorAction SilentlyContinue' -ForegroundColor Yellow
    
    Write-Host "`n⚠️  Important Notes:" -ForegroundColor $ColorWarning
    Write-Host "  • The file is usually named 'oo2core_8_win64.dll' in Path of Exile" -ForegroundColor Gray
    Write-Host "  • Rename it to 'oo2core.dll' when copying to libs folder" -ForegroundColor Gray
    Write-Host "  • This file is required for bundle decompression" -ForegroundColor Gray
    
    Write-Host "`n" + "=" * 60 -ForegroundColor $ColorInfo
}

# Main execution
Write-Host "🚀 GGPK Explorer Complete Dependency Setup" -ForegroundColor $ColorInfo
Write-Host "=" * 50

# Check if we're in the right directory
if (-not (Test-Path "src\GGPKExplorer\GGPKExplorer.csproj")) {
    Write-Error "This script must be run from the GGPK Explorer root directory"
    Write-Host "Expected to find: src\GGPKExplorer\GGPKExplorer.csproj" -ForegroundColor $ColorError
    exit 1
}

# Ensure libs directory exists
if (-not (Test-Path "libs")) {
    Write-Step "Creating libs directory..."
    New-Item -ItemType Directory -Path "libs" | Out-Null
    Write-Success "Created libs directory"
}

$setupSuccess = $true

# Step 1: Initial dependency verification
if (-not $SkipVerification) {
    $verificationResult = Test-Dependencies -Detailed
    
    if ($verificationResult.AllPresent -and -not $Force) {
        Write-Success "🎉 All dependencies are already present!"
        Write-Host "Use -Force to re-setup dependencies anyway" -ForegroundColor Gray
        exit 0
    }
    
    if ($verificationResult.MissingCount -gt 0) {
        Write-Warning "Found $($verificationResult.MissingCount) missing dependencies"
        Write-Host "Will automatically set up missing dependencies..." -ForegroundColor $ColorInfo
    }
}

# Step 2: Check prerequisites
Write-Step "Checking prerequisites..."

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK not found. Please install .NET 8 SDK first."
    exit 1
}

$dotnetVersion = dotnet --version
Write-Host "  ✅ .NET SDK version: $dotnetVersion" -ForegroundColor Gray

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Error "Git not found. Please install Git first."
    exit 1
}

Write-Success "Prerequisites check passed"

# Step 3: Setup LibGGPK3 libraries if missing
$libggpk3Dlls = @("libs/LibGGPK3.dll", "libs/LibBundle3.dll", "libs/LibBundledGGPK3.dll")
$needsLibGGPK3Setup = $Force -or ($libggpk3Dlls | Where-Object { -not (Test-Path $_) }).Count -gt 0

if ($needsLibGGPK3Setup) {
    if (-not (Setup-LibGGPK3Libraries -Configuration $Configuration)) {
        $setupSuccess = $false
    }
} else {
    Write-Success "LibGGPK3 libraries are already present"
}

# Step 4: Setup SystemExtensions if missing
$needsSystemExtensionsSetup = $Force -or (-not (Test-Path "libs/SystemExtensions.dll"))

if ($needsSystemExtensionsSetup) {
    if (-not (Setup-SystemExtensions -Configuration $Configuration)) {
        $setupSuccess = $false
    }
} else {
    Write-Success "SystemExtensions.dll is already present"
}

# Step 5: Check oo2core.dll
Write-Step "Checking oo2core.dll..."
$oo2corePath = "libs\oo2core.dll"
if (Test-Path $oo2corePath) {
    $fileInfo = Get-Item $oo2corePath
    Write-Success "oo2core.dll found ($([math]::Round($fileInfo.Length / 1MB, 2)) MB)"
} else {
    Write-Warning "oo2core.dll not found - manual setup required"
    Show-Oo2CoreInstructions
}

# Step 6: Final dependency verification
Write-Step "Running final dependency verification..."
$finalVerification = Test-Dependencies -Detailed

if ($finalVerification.AllPresent) {
    Write-Success "🎉 All dependencies verified successfully!"
    $verifyResult = $true
} else {
    Write-Error "Final verification failed - $($finalVerification.MissingCount) dependencies still missing"
    $verifyResult = $false
}

# Final status and instructions
Write-Host "`n" + "=" * 50
if ($setupSuccess -and $verifyResult) {
    Write-Success "🎉 All dependencies set up successfully!"
    Write-Host "`n🚀 Next Steps:" -ForegroundColor $ColorInfo
    Write-Host "  1. Ensure oo2core.dll is in the libs folder (see instructions above if missing)" -ForegroundColor Gray
    Write-Host "  2. Build GGPK Explorer: dotnet build" -ForegroundColor Gray
    Write-Host "  3. Run GGPK Explorer: dotnet run --project src\GGPKExplorer" -ForegroundColor Gray
    
    Write-Host "`n📚 Additional Commands:" -ForegroundColor $ColorInfo
    Write-Host "  • Verify dependencies: .\scripts\Verify-Dependencies.ps1" -ForegroundColor Gray
    Write-Host "  • Build release version: dotnet build -c Release" -ForegroundColor Gray
    Write-Host "  • Clean build: dotnet clean && dotnet build" -ForegroundColor Gray
    
} elseif ($setupSuccess) {
    Write-Warning "Dependencies set up but verification had issues"
    Write-Host "Check the verification output above for details" -ForegroundColor $ColorWarning
    
} else {
    Write-Error "Setup completed with errors"
    Write-Host "Please check the error messages above and resolve any issues" -ForegroundColor $ColorError
    exit 1
}

Write-Host "`n💡 For help with any issues, check:" -ForegroundColor $ColorInfo
Write-Host "  • README.md - Complete setup instructions" -ForegroundColor Gray
Write-Host "  • libs\README.md - Library-specific setup details" -ForegroundColor Gray