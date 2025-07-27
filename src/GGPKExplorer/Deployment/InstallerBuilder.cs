using System;
using System.IO;
using System.Text;

namespace GGPKExplorer.Deployment
{
    /// <summary>
    /// Builds deployment packages for GGPK Explorer without external dependencies
    /// </summary>
    public static class InstallerBuilder
    {
        /// <summary>
        /// Creates a WiX source file for MSI installer creation
        /// </summary>
        /// <param name="outputPath">Path where the WiX file will be created</param>
        /// <param name="binariesPath">Path to the compiled application binaries</param>
        public static void CreateWixSourceFile(string outputPath, string binariesPath)
        {
            var wixFilePath = Path.Combine(outputPath, "GGPKExplorer.wxs");
            var wixContent = GenerateWixSourceContent(binariesPath);
            
            File.WriteAllText(wixFilePath, wixContent, Encoding.UTF8);
        }
        
        /// <summary>
        /// Creates a ClickOnce deployment manifest
        /// </summary>
        /// <param name="outputPath">Path where the deployment will be created</param>
        /// <param name="binariesPath">Path to the compiled application binaries</param>
        public static void CreateClickOnceManifest(string outputPath, string binariesPath)
        {
            var manifestPath = Path.Combine(outputPath, "GGPKExplorer.application");
            var deploymentManifest = GenerateClickOnceManifest();
            
            File.WriteAllText(manifestPath, deploymentManifest, Encoding.UTF8);
        }
        
        /// <summary>
        /// Generates WiX source content for MSI creation
        /// </summary>
        private static string GenerateWixSourceContent(string binariesPath)
        {
            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Wix xmlns=""http://schemas.microsoft.com/wix/2006/wi"">
  <Product Id=""*"" Name=""{DeploymentConfig.ApplicationName}"" Language=""1033"" Version=""{DeploymentConfig.ApplicationVersion}.0"" 
           Manufacturer=""{DeploymentConfig.Publisher}"" UpgradeCode=""12345678-1234-1234-1234-123456789012"">
    <Package InstallerVersion=""200"" Compressed=""yes"" InstallScope=""perMachine"" />
    
    <MajorUpgrade DowngradeErrorMessage=""A newer version of [ProductName] is already installed."" />
    <MediaTemplate EmbedCab=""yes"" />
    
    <Feature Id=""ProductFeature"" Title=""{DeploymentConfig.ApplicationName}"" Level=""1"">
      <ComponentGroupRef Id=""ProductComponents"" />
    </Feature>
    
    <Directory Id=""TARGETDIR"" Name=""SourceDir"">
      <Directory Id=""ProgramFilesFolder"">
        <Directory Id=""INSTALLFOLDER"" Name=""{DeploymentConfig.ApplicationName}"" />
      </Directory>
      <Directory Id=""ProgramMenuFolder"">
        <Directory Id=""ApplicationProgramsFolder"" Name=""{DeploymentConfig.ApplicationName}""/>
      </Directory>
      <Directory Id=""DesktopFolder"" Name=""Desktop"" />
    </Directory>
    
    <ComponentGroup Id=""ProductComponents"" Directory=""INSTALLFOLDER"">
      <Component Id=""MainExecutable"" Guid=""*"">
        <File Id=""GGPKExplorerExe"" Source=""{Path.Combine(binariesPath, "GGPKExplorer.exe")}"" KeyPath=""yes"">
          <Shortcut Id=""ApplicationStartMenuShortcut"" Directory=""ApplicationProgramsFolder"" 
                    Name=""{DeploymentConfig.ApplicationName}"" WorkingDirectory=""INSTALLFOLDER"" 
                    Icon=""GGPKExplorer.exe"" IconIndex=""0"" Advertise=""yes"" />
          <Shortcut Id=""ApplicationDesktopShortcut"" Directory=""DesktopFolder"" 
                    Name=""{DeploymentConfig.ApplicationName}"" WorkingDirectory=""INSTALLFOLDER"" 
                    Icon=""GGPKExplorer.exe"" IconIndex=""0"" Advertise=""yes"" />
        </File>
        
        <!-- File Association Registry Entries -->
        <RegistryValue Root=""HKCR"" Key="".ggpk"" Value=""GGPKExplorer.ggpkfile"" Type=""string"" />
        <RegistryValue Root=""HKCR"" Key="".ggpk"" Name=""Content Type"" Value=""application/x-ggpk"" Type=""string"" />
        <RegistryValue Root=""HKCR"" Key=""GGPKExplorer.ggpkfile"" Value=""Path of Exile GGPK File"" Type=""string"" />
        <RegistryValue Root=""HKCR"" Key=""GGPKExplorer.ggpkfile\DefaultIcon"" Value=""[INSTALLFOLDER]GGPKExplorer.exe,0"" Type=""string"" />
        <RegistryValue Root=""HKCR"" Key=""GGPKExplorer.ggpkfile\shell\open\command"" Value=""&quot;[INSTALLFOLDER]GGPKExplorer.exe&quot; &quot;%1&quot;"" Type=""string"" />
        
        <!-- Application Path Registration -->
        <RegistryValue Root=""HKLM"" Key=""SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\GGPKExplorer.exe"" 
                       Value=""[INSTALLFOLDER]GGPKExplorer.exe"" Type=""string"" />
        <RegistryValue Root=""HKLM"" Key=""SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\GGPKExplorer.exe"" 
                       Name=""Path"" Value=""[INSTALLFOLDER]"" Type=""string"" />
      </Component>
      
      <Component Id=""NativeLibraries"" Guid=""*"">
        <File Id=""Oo2CoreDll"" Source=""{Path.Combine(binariesPath, "oo2core.dll")}"" KeyPath=""yes"" />
        <File Id=""SystemExtensionsDll"" Source=""{Path.Combine(binariesPath, "SystemExtensions.dll")}"" />
      </Component>
      
      <Component Id=""RuntimeLibraries"" Guid=""*"">
        <!-- Add other required DLLs here -->
        <File Id=""WpfUiDll"" Source=""{Path.Combine(binariesPath, "WPF-UI.dll")}"" KeyPath=""yes"" />
      </Component>
    </ComponentGroup>
    
    <Icon Id=""GGPKExplorer.exe"" SourceFile=""{Path.Combine(binariesPath, "GGPKExplorer.exe")}"" />
    <Property Id=""ARPPRODUCTICON"" Value=""GGPKExplorer.exe"" />
    <Property Id=""ARPHELPLINK"" Value=""https://github.com/ggpkexplorer/ggpkexplorer"" />
    <Property Id=""ARPURLINFOABOUT"" Value=""https://github.com/ggpkexplorer/ggpkexplorer"" />
  </Product>
</Wix>";
        }
        
        /// <summary>
        /// Generates ClickOnce manifest content
        /// </summary>
        private static string GenerateClickOnceManifest()
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<asmv1:assembly xsi:schemaLocation=""urn:schemas-microsoft-com:asm.v1 assembly.adaptive.xsd"" 
                manifestVersion=""1.0"" 
                xmlns:asmv1=""urn:schemas-microsoft-com:asm.v1"" 
                xmlns=""urn:schemas-microsoft-com:asm.v2"" 
                xmlns:asmv2=""urn:schemas-microsoft-com:asm.v2"" 
                xmlns:xrml=""urn:mpeg:mpeg21:2003:01-REL-R-NS"" 
                xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                xmlns:asmv3=""urn:schemas-microsoft-com:asm.v3"" 
                xmlns:dsig=""http://www.w3.org/2000/09/xmldsig#"" 
                xmlns:co.v1=""urn:schemas-microsoft-com:clickonce.v1"" 
                xmlns:co.v2=""urn:schemas-microsoft-com:clickonce.v2"">
  <assemblyIdentity name=""GGPKExplorer.application"" 
                    version=""{DeploymentConfig.ApplicationVersion}.0"" 
                    publicKeyToken=""0000000000000000"" 
                    language=""neutral"" 
                    processorArchitecture=""msil"" 
                    xmlns=""urn:schemas-microsoft-com:asm.v1"" />
  <description asmv2:publisher=""{DeploymentConfig.Publisher}"" 
               asmv2:product=""{DeploymentConfig.ApplicationName}"" 
               xmlns=""urn:schemas-microsoft-com:asm.v1"" />
  <deployment install=""true"" mapFileExtensions=""true"">
    <subscription>
      <update>
        <beforeApplicationStartup />
      </update>
    </subscription>
    <deploymentProvider codebase=""GGPKExplorer.application"" />
  </deployment>
  <compatibleFrameworks xmlns=""urn:schemas-microsoft-com:clickonce.v2"">
    <framework targetVersion=""8.0"" profile=""Full"" supportedRuntime=""8.0.0"" />
  </compatibleFrameworks>
  <dependency>
    <dependentAssembly dependencyType=""install"" codebase=""GGPKExplorer.exe.manifest"" size=""7168"">
      <assemblyIdentity name=""GGPKExplorer.exe"" 
                        version=""{DeploymentConfig.ApplicationVersion}.0"" 
                        publicKeyToken=""0000000000000000"" 
                        language=""neutral"" 
                        processorArchitecture=""msil"" 
                        type=""win32"" />
      <hash>
        <dsig:Transforms>
          <dsig:Transform Algorithm=""urn:schemas-microsoft-com:HashTransforms.Identity"" />
        </dsig:Transforms>
        <dsig:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1"" />
        <dsig:DigestValue>PLACEHOLDER_HASH</dsig:DigestValue>
      </hash>
    </dependentAssembly>
  </dependency>
</asmv1:assembly>";
        }
        
        /// <summary>
        /// Creates a registry file for manual file association registration
        /// </summary>
        /// <param name="outputPath">Path where the registry file will be created</param>
        public static void CreateFileAssociationRegistry(string outputPath)
        {
            var regFilePath = Path.Combine(outputPath, "RegisterFileAssociations.reg");
            var regContent = GenerateRegistryContent();
            
            File.WriteAllText(regFilePath, regContent, Encoding.UTF8);
        }
        
        /// <summary>
        /// Generates registry file content for file associations
        /// </summary>
        private static string GenerateRegistryContent()
        {
            var installPath = @"C:\Program Files\GGPK Explorer"; // Default installation path
            
            return $@"Windows Registry Editor Version 5.00

; Register .ggpk file extension
[HKEY_CLASSES_ROOT\.ggpk]
@=""GGPKExplorer.ggpkfile""
""Content Type""=""application/x-ggpk""

; Register file type
[HKEY_CLASSES_ROOT\GGPKExplorer.ggpkfile]
@=""Path of Exile GGPK File""

[HKEY_CLASSES_ROOT\GGPKExplorer.ggpkfile\DefaultIcon]
@=""{installPath}\\GGPKExplorer.exe,0""

[HKEY_CLASSES_ROOT\GGPKExplorer.ggpkfile\shell\open\command]
@=""\""${installPath}\\GGPKExplorer.exe\"" \""%1\""""

; Add to ""Open with"" context menu
[HKEY_CLASSES_ROOT\*\shell\GGPKExplorer]
@=""Open with GGPK Explorer""

[HKEY_CLASSES_ROOT\*\shell\GGPKExplorer\command]
@=""\""${installPath}\\GGPKExplorer.exe\"" \""%1\""""

; Application registration
[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\GGPKExplorer.exe]
@=""{installPath}\\GGPKExplorer.exe""
""Path""=""{installPath}""
";
        }
    }
    
    /// <summary>
    /// Native methods for shell notifications
    /// </summary>
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
        
        /// <summary>
        /// Notifies the system that file associations have changed
        /// </summary>
        public static void NotifyFileAssociationChange()
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
    }
}