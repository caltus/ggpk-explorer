using System;
using System.IO;
using System.Reflection;

namespace GGPKExplorer.Deployment
{
    /// <summary>
    /// Configuration class for deployment settings and native library loading
    /// </summary>
    public static class DeploymentConfig
    {
        /// <summary>
        /// Application name for deployment
        /// </summary>
        public const string ApplicationName = "GGPK Explorer";
        
        /// <summary>
        /// Application version
        /// </summary>
        public const string ApplicationVersion = "1.0.0";
        
        /// <summary>
        /// Publisher name
        /// </summary>
        public const string Publisher = "GGPK Explorer Team";
        
        /// <summary>
        /// Application description
        /// </summary>
        public const string Description = "Windows Explorer-style file browser for Path of Exile GGPK files";
        
        /// <summary>
        /// Gets the application directory path
        /// </summary>
        public static string ApplicationDirectory => 
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
        
        /// <summary>
        /// Gets the path to the oo2core.dll native library
        /// </summary>
        public static string Oo2CoreDllPath => Path.Combine(ApplicationDirectory, "oo2core.dll");
        
        /// <summary>
        /// Gets the path to the SystemExtensions.dll native library
        /// </summary>
        public static string SystemExtensionsDllPath => Path.Combine(ApplicationDirectory, "SystemExtensions.dll");
        
        /// <summary>
        /// Verifies that all required native libraries are present
        /// </summary>
        /// <returns>True if all libraries are found, false otherwise</returns>
        public static bool VerifyNativeLibraries()
        {
            return File.Exists(Oo2CoreDllPath) && File.Exists(SystemExtensionsDllPath);
        }
        
        /// <summary>
        /// Gets the missing native libraries
        /// </summary>
        /// <returns>Array of missing library names</returns>
        public static string[] GetMissingLibraries()
        {
            var missing = new List<string>();
            
            if (!File.Exists(Oo2CoreDllPath))
                missing.Add("oo2core.dll");
                
            if (!File.Exists(SystemExtensionsDllPath))
                missing.Add("SystemExtensions.dll");
                
            return missing.ToArray();
        }
    }
}