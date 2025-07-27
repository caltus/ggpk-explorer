using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Deployment
{
    /// <summary>
    /// Handles loading of native libraries for deployment scenarios
    /// Ensures proper loading of oo2core.dll and SystemExtensions.dll
    /// </summary>
    public static class NativeLibraryLoader
    {
        private static readonly ILogger Logger = 
            Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger(typeof(NativeLibraryLoader));
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        
        private static IntPtr _oo2CoreHandle = IntPtr.Zero;
        private static IntPtr _systemExtensionsHandle = IntPtr.Zero;
        
        /// <summary>
        /// Initializes and loads all required native libraries
        /// </summary>
        /// <returns>True if all libraries loaded successfully</returns>
        public static bool InitializeNativeLibraries()
        {
            try
            {
                Logger.LogInformation("Initializing native libraries...");
                
                // Load oo2core.dll
                if (!LoadOo2Core())
                {
                    Logger.LogError("Failed to load oo2core.dll");
                    return false;
                }
                
                // Load SystemExtensions.dll
                if (!LoadSystemExtensions())
                {
                    Logger.LogError("Failed to load SystemExtensions.dll");
                    return false;
                }
                
                Logger.LogInformation("All native libraries loaded successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing native libraries");
                return false;
            }
        }
        
        /// <summary>
        /// Loads the oo2core.dll library
        /// </summary>
        private static bool LoadOo2Core()
        {
            var dllPath = DeploymentConfig.Oo2CoreDllPath;
            
            if (!File.Exists(dllPath))
            {
                Logger.LogError($"oo2core.dll not found at: {dllPath}");
                return false;
            }
            
            _oo2CoreHandle = LoadLibrary(dllPath);
            if (_oo2CoreHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.LogError($"Failed to load oo2core.dll. Error code: {error}");
                return false;
            }
            
            Logger.LogInformation($"Successfully loaded oo2core.dll from: {dllPath}");
            return true;
        }
        
        /// <summary>
        /// Loads the SystemExtensions.dll library
        /// </summary>
        private static bool LoadSystemExtensions()
        {
            var dllPath = DeploymentConfig.SystemExtensionsDllPath;
            
            if (!File.Exists(dllPath))
            {
                Logger.LogError($"SystemExtensions.dll not found at: {dllPath}");
                return false;
            }
            
            _systemExtensionsHandle = LoadLibrary(dllPath);
            if (_systemExtensionsHandle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.LogError($"Failed to load SystemExtensions.dll. Error code: {error}");
                return false;
            }
            
            Logger.LogInformation($"Successfully loaded SystemExtensions.dll from: {dllPath}");
            return true;
        }
        
        /// <summary>
        /// Verifies that a specific function exists in oo2core.dll
        /// </summary>
        /// <param name="functionName">Name of the function to verify</param>
        /// <returns>True if function exists</returns>
        public static bool VerifyOo2CoreFunction(string functionName)
        {
            if (_oo2CoreHandle == IntPtr.Zero)
                return false;
                
            var procAddress = GetProcAddress(_oo2CoreHandle, functionName);
            return procAddress != IntPtr.Zero;
        }
        
        /// <summary>
        /// Cleans up loaded native libraries
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_oo2CoreHandle != IntPtr.Zero)
                {
                    FreeLibrary(_oo2CoreHandle);
                    _oo2CoreHandle = IntPtr.Zero;
                    Logger.LogInformation("Unloaded oo2core.dll");
                }
                
                if (_systemExtensionsHandle != IntPtr.Zero)
                {
                    FreeLibrary(_systemExtensionsHandle);
                    _systemExtensionsHandle = IntPtr.Zero;
                    Logger.LogInformation("Unloaded SystemExtensions.dll");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error during native library cleanup");
            }
        }
    }
}