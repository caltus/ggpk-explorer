# Build-Deployment.ps1
# PowerShell script to build deployment packages for GGPK Explorer

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = ".\publish",
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildMSI,
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildClickOnce,
    
    [Parameter(Mandatory=$false)]
    [switch]$Clean
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir
$ProjectDir = Join-Path $RootDir "src\GGPKExplorer"
$SolutionFile = Join-Path $RootDir "GGPKExplorer.sln"

Write-Host "GGPK Explorer Deployment Builder" -ForegroundColor Green
Write-Host "=================================" -ForegroundColor Green
Write-Host "Configuration: $Configuration"
Write-Host "Output Path: $OutputPath"
Write-Host "Root Directory: $RootDir"
Write-Host ""

# Clean previous builds if requested
if ($Clean) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    
    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
        Write-Host "Cleaned output directory: $OutputPath"
    }
    
    # Clean bin and obj directories
    Get-ChildItem -Path $RootDir -Recurse -Directory -Name "bin" | ForEach-Object {
        $binPath = Join-Path $RootDir $_
        if (Test-Path $binPath) {
            Remove-Item $binPath -Recurse -Force
            Write-Host "Cleaned: $binPath"
        }
    }
    
    Get-ChildItem -Path $RootDir -Recurse -Directory -Name "obj" | ForEach-Object {
        $objPath = Join-Path $RootDir $_
        if (Test-Path $objPath) {
            Remove-Item $objPath -Recurse -Force
            Write-Host "Cleaned: $objPath"
        }
    }
    
    Write-Host "Clean completed." -ForegroundColor Green
    Write-Host ""
}

# Create output directory
if (!(Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
    Write-Host "Created output directory: $OutputPath"
}

# Restore NuGet packages
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
try {
    dotnet restore $SolutionFile
    Write-Host "Package restoration completed." -ForegroundColor Green
} catch {
    Write-Error "Failed to restore packages: $_"
    exit 1
}

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
try {
    dotnet build $SolutionFile -c $Configuration --no-restore
    Write-Host "Build completed successfully." -ForegroundColor Green
} catch {
    Write-Error "Build failed: $_"
    exit 1
}

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Yellow
$PublishDir = Join-Path $OutputPath "app"
try {
    dotnet publish $ProjectDir -c $Configuration -o $PublishDir --no-build --self-contained false -r win-x64
    Write-Host "Application published to: $PublishDir" -ForegroundColor Green
} catch {
    Write-Error "Publish failed: $_"
    exit 1
}

# Verify native libraries are present
Write-Host "Verifying native libraries..." -ForegroundColor Yellow
$Oo2CorePath = Join-Path $PublishDir "oo2core.dll"
$SystemExtensionsPath = Join-Path $PublishDir "SystemExtensions.dll"

if (!(Test-Path $Oo2CorePath)) {
    Write-Warning "oo2core.dll not found in publish directory. Copying from libs..."
    $SourceOo2Core = Join-Path $RootDir "libs\oo2core.dll"
    if (Test-Path $SourceOo2Core) {
        Copy-Item $SourceOo2Core $Oo2CorePath
        Write-Host "Copied oo2core.dll to publish directory."
    } else {
        Write-Error "oo2core.dll not found in libs directory!"
        exit 1
    }
}

if (!(Test-Path $SystemExtensionsPath)) {
    Write-Warning "SystemExtensions.dll not found in publish directory. Copying from libs..."
    $SourceSystemExtensions = Join-Path $RootDir "libs\SystemExtensions.dll"
    if (Test-Path $SourceSystemExtensions) {
        Copy-Item $SourceSystemExtensions $SystemExtensionsPath
        Write-Host "Copied SystemExtensions.dll to publish directory."
    } else {
        Write-Error "SystemExtensions.dll not found in libs directory!"
        exit 1
    }
}

Write-Host "Native libraries verified." -ForegroundColor Green

# Build ClickOnce deployment if requested
if ($BuildClickOnce) {
    Write-Host "Building ClickOnce deployment..." -ForegroundColor Yellow
    $ClickOnceDir = Join-Path $OutputPath "clickonce"
    
    try {
        # Use MSBuild to create ClickOnce deployment
        dotnet msbuild $ProjectDir -p:Configuration=$Configuration -p:PublishUrl=$ClickOnceDir\ -p:PublishProtocol=ClickOnce -p:UpdateEnabled=true -p:UpdateMode=Foreground -p:ApplicationRevision=1 -t:Publish
        
        Write-Host "ClickOnce deployment created in: $ClickOnceDir" -ForegroundColor Green
    } catch {
        Write-Warning "ClickOnce deployment failed: $_"
        Write-Host "ClickOnce deployment requires additional configuration in Visual Studio or advanced MSBuild setup."
    }
}

# Build MSI installer if requested
if ($BuildMSI) {
    Write-Host "Building MSI installer..." -ForegroundColor Yellow
    
    try {
        # Check if WiX Toolset is available
        $WixPath = Get-Command "candle.exe" -ErrorAction SilentlyContinue
        if (!$WixPath) {
            Write-Warning "WiX Toolset not found in PATH. MSI creation requires WiX Toolset to be installed."
            Write-Host "Please install WiX Toolset from: https://wixtoolset.org/"
        } else {
            # Create a simple WiX file for the installer
            $WixFile = Join-Path $OutputPath "GGPKExplorer.wxs"
            $MsiDir = Join-Path $OutputPath "msi"
            
            if (!(Test-Path $MsiDir)) {
                New-Item -ItemType Directory -Path $MsiDir -Force | Out-Null
            }
            
            # Generate WiX source file
            $WixContent = @"
<?xml version="1.0" encoding="UTF-8"?>
<Wix xmlns="http://schemas.microsoft.com/wix/2006/wi">
  <Product Id="*" Name="GGPK Explorer" Language="1033" Version="1.0.0.0" Manufacturer="GGPK Explorer Team" UpgradeCode="12345678-1234-1234-1234-123456789012">
    <Package InstallerVersion="200" Compressed="yes" InstallScope="perMachine" />
    
    <MajorUpgrade DowngradeErrorMessage="A newer version of [ProductName] is already installed." />
    <MediaTemplate EmbedCab="yes" />
    
    <Feature Id="ProductFeature" Title="GGPK Explorer" Level="1">
      <ComponentGroupRef Id="ProductComponents" />
    </Feature>
    
    <Directory Id="TARGETDIR" Name="SourceDir">
      <Directory Id="ProgramFilesFolder">
        <Directory Id="INSTALLFOLDER" Name="GGPK Explorer" />
      </Directory>
      <Directory Id="ProgramMenuFolder">
        <Directory Id="ApplicationProgramsFolder" Name="GGPK Explorer"/>
      </Directory>
      <Directory Id="DesktopFolder" Name="Desktop" />
    </Directory>
    
    <ComponentGroup Id="ProductComponents" Directory="INSTALLFOLDER">
      <Component Id="MainExecutable" Guid="*">
        <File Id="GGPKExplorerExe" Source="$PublishDir\GGPKExplorer.exe" KeyPath="yes">
          <Shortcut Id="ApplicationStartMenuShortcut" Directory="ApplicationProgramsFolder" Name="GGPK Explorer" WorkingDirectory="INSTALLFOLDER" Icon="GGPKExplorer.exe" IconIndex="0" Advertise="yes" />
          <Shortcut Id="ApplicationDesktopShortcut" Directory="DesktopFolder" Name="GGPK Explorer" WorkingDirectory="INSTALLFOLDER" Icon="GGPKExplorer.exe" IconIndex="0" Advertise="yes" />
        </File>
        <ProgId Id="GGPKExplorer.ggpkfile" Description="Path of Exile GGPK File">
          <Extension Id="ggpk" ContentType="application/x-ggpk">
            <Verb Id="open" Command="Open" TargetFile="GGPKExplorerExe" Argument='"%1"' />
          </Extension>
        </ProgId>
      </Component>
      <Component Id="NativeLibraries" Guid="*">
        <File Id="Oo2CoreDll" Source="$PublishDir\oo2core.dll" KeyPath="yes" />
        <File Id="SystemExtensionsDll" Source="$PublishDir\SystemExtensions.dll" />
      </Component>
    </ComponentGroup>
    
    <Icon Id="GGPKExplorer.exe" SourceFile="$PublishDir\GGPKExplorer.exe" />
    <Property Id="ARPPRODUCTICON" Value="GGPKExplorer.exe" />
    <Property Id="ARPHELPLINK" Value="https://github.com/caltus/ggpk-explorer" />
    <Property Id="ARPURLINFOABOUT" Value="https://github.com/caltus/ggpk-explorer" />
  </Product>
</Wix>
"@
            
            Set-Content -Path $WixFile -Value $WixContent -Encoding UTF8
            
            # Compile WiX file
            $WixObjFile = Join-Path $MsiDir "GGPKExplorer.wixobj"
            $MsiFile = Join-Path $MsiDir "GGPKExplorer-Setup.msi"
            
            & candle.exe -out $WixObjFile $WixFile
            & light.exe -out $MsiFile $WixObjFile
            
            if (Test-Path $MsiFile) {
                Write-Host "MSI installer created: $MsiFile" -ForegroundColor Green
            } else {
                Write-Warning "MSI creation may have failed. Check WiX output for errors."
            }
        }
    } catch {
        Write-Warning "MSI creation failed: $_"
        Write-Host "For advanced MSI creation, consider using WixSharp or Visual Studio Installer Projects."
    }
}

# Create deployment summary
Write-Host ""
Write-Host "Deployment Summary" -ForegroundColor Green
Write-Host "==================" -ForegroundColor Green
Write-Host "Published Application: $PublishDir"

if ($BuildClickOnce) {
    $ClickOnceDir = Join-Path $OutputPath "clickonce"
    if (Test-Path $ClickOnceDir) {
        Write-Host "ClickOnce Deployment: $ClickOnceDir"
    }
}

if ($BuildMSI) {
    $MsiFile = Join-Path $OutputPath "msi\GGPKExplorer-Setup.msi"
    if (Test-Path $MsiFile) {
        Write-Host "MSI Installer: $MsiFile"
    }
}

Write-Host ""
Write-Host "Deployment build completed successfully!" -ForegroundColor Green

# Test the published application
Write-Host "Testing published application..." -ForegroundColor Yellow
$TestExe = Join-Path $PublishDir "GGPKExplorer.exe"
if (Test-Path $TestExe) {
    try {
        # Quick test to see if the executable can start (will exit quickly due to no GGPK file)
        $TestProcess = Start-Process -FilePath $TestExe -ArgumentList "--version" -PassThru -WindowStyle Hidden -Wait
        if ($TestProcess.ExitCode -eq 0 -or $TestProcess.ExitCode -eq 1) {
            Write-Host "Application test passed - executable can start." -ForegroundColor Green
        } else {
            Write-Warning "Application test returned exit code: $($TestProcess.ExitCode)"
        }
    } catch {
        Write-Warning "Could not test application startup: $_"
    }
} else {
    Write-Error "Published executable not found: $TestExe"
}

Write-Host ""
Write-Host "Build script completed!" -ForegroundColor Green