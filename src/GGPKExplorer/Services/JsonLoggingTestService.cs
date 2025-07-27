using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Test service to demonstrate JSON logging functionality
    /// </summary>
    public class JsonLoggingTestService
    {
        private readonly IJsonLoggingService _jsonLogger;

        public JsonLoggingTestService()
        {
            _jsonLogger = new JsonLoggingService(Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonLoggingService>.Instance);
        }

        /// <summary>
        /// Creates sample JSON log entries to demonstrate the logging system
        /// </summary>
        public void CreateSampleLogs()
        {
            // Sample GGPK load operation
            var correlationId = _jsonLogger.BeginOperationScope("LoadGGPK", new Dictionary<string, object>
            {
                ["FilePath"] = "C:\\Games\\PathOfExile\\Content.ggpk",
                ["FileSize"] = 15_000_000_000L, // 15GB
                ["ExpectedBundles"] = true
            });

            _jsonLogger.LogGGPKOperation("LoadGGPK_Start", "C:\\Games\\PathOfExile\\Content.ggpk", new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["LoadMethod"] = "BundledGGPK",
                ["ThreadId"] = Environment.CurrentManagedThreadId
            });

            // Sample decompression operation
            _jsonLogger.LogDecompressionOperation(
                "BundleDecompression", 
                "_.index.bin", 
                originalSize: 50_000_000, 
                decompressedSize: 150_000_000, 
                duration: TimeSpan.FromSeconds(2.5), 
                compressionType: "Oodle_Kraken",
                context: new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["BundleCount"] = 1250,
                    ["IndexVersion"] = 4
                });

            // Sample file extraction
            var extractCorrelationId = _jsonLogger.BeginOperationScope("ExtractFile", new Dictionary<string, object>
            {
                ["SourcePath"] = "Art/2DItems/Currency/CurrencyRerollRare.dds",
                ["DestinationPath"] = "C:\\Temp\\extracted\\CurrencyRerollRare.dds"
            });

            _jsonLogger.LogExtractionOperation(
                "ExtractFile_Complete",
                "Art/2DItems/Currency/CurrencyRerollRare.dds",
                "C:\\Temp\\extracted\\CurrencyRerollRare.dds",
                fileSize: 87_432,
                duration: TimeSpan.FromMilliseconds(125),
                context: new Dictionary<string, object>
                {
                    ["CorrelationId"] = extractCorrelationId,
                    ["CompressionType"] = "DDS_DXT5",
                    ["ImageDimensions"] = "256x256"
                });

            _jsonLogger.EndOperationScope(extractCorrelationId, true, new Dictionary<string, object>
            {
                ["TotalFiles"] = 1,
                ["TotalSize"] = 87_432
            });

            // Sample performance metrics
            _jsonLogger.LogPerformanceMetric("FileReadThroughput", 45.7, "MB/s", new Dictionary<string, object>
            {
                ["Operation"] = "BulkExtraction",
                ["FileCount"] = 150,
                ["TotalSize"] = 125_000_000
            });

            // Sample error
            try
            {
                throw new InvalidOperationException("Sample error for JSON logging demonstration");
            }
            catch (Exception ex)
            {
                _jsonLogger.LogError("SampleError", ex, new Dictionary<string, object>
                {
                    ["Operation"] = "DemoError",
                    ["Context"] = "JSON logging test"
                });
            }

            _jsonLogger.EndOperationScope(correlationId, true, new Dictionary<string, object>
            {
                ["TotalBundles"] = 1250,
                ["LoadDuration"] = 8500, // 8.5 seconds
                ["MemoryUsed"] = 450_000_000 // 450MB
            });
        }
    }
}