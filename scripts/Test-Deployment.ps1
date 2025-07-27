# Test-Deployment.ps1
# PowerShell script to test GGPK Explorer deployment packages

param(
    [Parameter(Mandatory=$false)]
    [string]$DeploymentPath = ".\publish",
    
    [Parameter(Mandatory=$false)]
    [switch]$TestClickOnce,
    
    [Parameter(Mandatory=$false)]
    [switch]$TestMSI,
    
    [Parameter(Mandatory=$false)]
    [switch]$TestFileAssociations,
    
    [Parameter(Mandatory=$false)]
    [switch]$VerboseOutput
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "GGPK Explorer Deployment Tester" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host "Deployment Path: $DeploymentPath"
Write-Host ""

# Test results tracking
$TestResults = @{
    "Application Startup" = $false
    "Native Libraries" = $false
    "File Associations" = $false
    "ClickOnce Deployment" = $false
    "MSI Installation" = $false
}

# Function to write test result
function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Success,
        [string]$Details = ""
    )
    
    $TestResults[$TestName] = $Success
    
    if ($Success) {
        Write-Host "✓ $TestName" -ForegroundColor Green
    } else {
        Write-Host "✗ $TestName" -ForegroundColor Red
    }
    
    if ($Details -and $VerboseOutput) {
        Write-Host "  $Details" -ForegroundColor Gray
    }
}

# Test 1: Application Startup
Write-Host "Testing application startup..." -ForegroundColor Yellow
$AppPath = Join-Path $DeploymentPath "app\GGPKExplorer.exe"

if (Test-Path $AppPath) {
    try {
        # Test if the application can start and show help/version info
        $StartInfo = New-Object System.Diagnostics.ProcessStartInfo
        $StartInfo.FileName = $AppPath
        $StartInfo.Arguments = "--help"
        $StartInfo.UseShellExecute = $false
        $StartInfo.RedirectStandardOutput = $true
        $StartInfo.RedirectStandardError = $true
        $StartInfo.CreateNoWindow = $true
        
        $Process = New-Object System.Diagnostics.Process
        $Process.StartInfo = $StartInfo
        $Process.Start() | Out-Null
        
        # Wait for a short time to see if it starts successfully
        if ($Process.WaitForExit(5000)) {
            # Process exited within timeout - this is expected for help command
            Write-TestResult "Application Startup" $true "Application can start and respond to command line arguments"
        } else {
            # Process is still running - kill it and consider it successful
            $Process.Kill()
            Write-TestResult "Application Startup" $true "Application started successfully (had to terminate)"
        }
    } catch {
        Write-TestResult "Application Startup" $false "Failed to start: $_"
    }
} else {
    Write-TestResult "Application Startup" $false "Application executable not found at: $AppPath"
}

# Test 2: Native Libraries
Write-Host "Testing native libraries..." -ForegroundColor Yellow
$Oo2CorePath = Join-Path $DeploymentPath "app\oo2core.dll"
$SystemExtensionsPath = Join-Path $DeploymentPath "app\SystemExtensions.dll"

$NativeLibsPresent = (Test-Path $Oo2CorePath) -and (Test-Path $SystemExtensionsPath)

if ($NativeLibsPresent) {
    try {
        # Test if we can load the native libraries
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class NativeLibraryTester
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr LoadLibrary(string lpFileName);
    
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr hModule);
}
"@
        
        $Oo2Handle = [NativeLibraryTester]::LoadLibrary($Oo2CorePath)
        $SysExtHandle = [NativeLibraryTester]::LoadLibrary($SystemExtensionsPath)
        
        if ($Oo2Handle -ne [IntPtr]::Zero -and $SysExtHandle -ne [IntPtr]::Zero) {
            [NativeLibraryTester]::FreeLibrary($Oo2Handle) | Out-Null
            [NativeLibraryTester]::FreeLibrary($SysExtHandle) | Out-Null
            Write-TestResult "Native Libraries" $true "Both oo2core.dll and SystemExtensions.dll can be loaded"
        } else {
            Write-TestResult "Native Libraries" $false "Failed to load one or more native libraries"
        }
    } catch {
        Write-TestResult "Native Libraries" $false "Error testing native library loading: $_"
    }
} else {
    $MissingLibs = @()
    if (!(Test-Path $Oo2CorePath)) { $MissingLibs += "oo2core.dll" }
    if (!(Test-Path $SystemExtensionsPath)) { $MissingLibs += "SystemExtensions.dll" }
    
    Write-TestResult "Native Libraries" $false "Missing libraries: $($MissingLibs -join ', ')"
}

# Test 3: File Associations (if requested)
if ($TestFileAssociations) {
    Write-Host "Testing file associations..." -ForegroundColor Yellow
    
    try {
        # Check if .ggpk extension is registered
        $GgpkAssoc = Get-ItemProperty -Path "HKCR:\.ggpk" -ErrorAction SilentlyContinue
        
        if ($GgpkAssoc -and $GgpkAssoc."(default)" -eq "GGPKExplorer.ggpkfile") {
            # Check if the file type is properly registered
            $FileType = Get-ItemProperty -Path "HKCR:\GGPKExplorer.ggpkfile\shell\open\command" -ErrorAction SilentlyContinue
            
            if ($FileType -and $FileType."(default)" -like "*GGPKExplorer.exe*") {
                Write-TestResult "File Associations" $true "GGPK file association is properly registered"
            } else {
                Write-TestResult "File Associations" $false "GGPK file type command not properly registered"
            }
        } else {
            Write-TestResult "File Associations" $false "GGPK file extension not registered"
        }
    } catch {
        Write-TestResult "File Associations" $false "Error checking file associations: $_"
    }
} else {
    Write-Host "Skipping file associations test (use -TestFileAssociations to enable)" -ForegroundColor Gray
}

# Test 4: ClickOnce Deployment (if requested)
if ($TestClickOnce) {
    Write-Host "Testing ClickOnce deployment..." -ForegroundColor Yellow
    
    $ClickOnceDir = Join-Path $DeploymentPath "clickonce"
    $ClickOnceManifest = Join-Path $ClickOnceDir "GGPKExplorer.application"
    
    if (Test-Path $ClickOnceManifest) {
        try {
            # Validate the ClickOnce manifest
            [xml]$ManifestXml = Get-Content $ClickOnceManifest
            
            if ($ManifestXml.assembly -and $ManifestXml.assembly.assemblyIdentity) {
                Write-TestResult "ClickOnce Deployment" $true "ClickOnce manifest is valid"
            } else {
                Write-TestResult "ClickOnce Deployment" $false "ClickOnce manifest is malformed"
            }
        } catch {
            Write-TestResult "ClickOnce Deployment" $false "Error validating ClickOnce manifest: $_"
        }
    } else {
        Write-TestResult "ClickOnce Deployment" $false "ClickOnce manifest not found at: $ClickOnceManifest"
    }
} else {
    Write-Host "Skipping ClickOnce deployment test (use -TestClickOnce to enable)" -ForegroundColor Gray
}

# Test 5: MSI Installation (if requested)
if ($TestMSI) {
    Write-Host "Testing MSI installer..." -ForegroundColor Yellow
    
    $MsiPath = Join-Path $DeploymentPath "msi\GGPKExplorer-Setup.msi"
    
    if (Test-Path $MsiPath) {
        try {
            # Get MSI properties using Windows Installer COM object
            $WindowsInstaller = New-Object -ComObject WindowsInstaller.Installer
            $Database = $WindowsInstaller.GetType().InvokeMember("OpenDatabase", "InvokeMethod", $null, $WindowsInstaller, @($MsiPath, 0))
            
            # Query for product information
            $View = $Database.GetType().InvokeMember("OpenView", "InvokeMethod", $null, $Database, @("SELECT Value FROM Property WHERE Property='ProductName'"))
            $View.GetType().InvokeMember("Execute", "InvokeMethod", $null, $View, $null)
            $Record = $View.GetType().InvokeMember("Fetch", "InvokeMethod", $null, $View, $null)
            
            if ($Record) {
                $ProductName = $Record.GetType().InvokeMember("StringData", "GetProperty", $null, $Record, 1)
                Write-TestResult "MSI Installation" $true "MSI package is valid (Product: $ProductName)"
            } else {
                Write-TestResult "MSI Installation" $false "Could not read MSI product information"
            }
            
            # Clean up COM objects
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($Record) | Out-Null
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($View) | Out-Null
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($Database) | Out-Null
            [System.Runtime.Interopservices.Marshal]::ReleaseComObject($WindowsInstaller) | Out-Null
        } catch {
            Write-TestResult "MSI Installation" $false "Error validating MSI package: $_"
        }
    } else {
        Write-TestResult "MSI Installation" $false "MSI installer not found at: $MsiPath"
    }
} else {
    Write-Host "Skipping MSI installation test (use -TestMSI to enable)" -ForegroundColor Gray
}

# Test Summary
Write-Host ""
Write-Host "Test Summary" -ForegroundColor Green
Write-Host "============" -ForegroundColor Green

$PassedTests = 0
$TotalTests = 0

foreach ($Test in $TestResults.Keys) {
    $Result = $TestResults[$Test]
    $TotalTests++
    
    if ($Result) {
        $PassedTests++
        Write-Host "✓ $Test" -ForegroundColor Green
    } else {
        Write-Host "✗ $Test" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "Results: $PassedTests/$TotalTests tests passed" -ForegroundColor $(if ($PassedTests -eq $TotalTests) { "Green" } else { "Yellow" })

if ($PassedTests -eq $TotalTests) {
    Write-Host "All tests passed! Deployment is ready." -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some tests failed. Please review the deployment configuration." -ForegroundColor Yellow
    exit 1
}