using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

using VNLib.Utils.IO;
using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
using VNLib.Net.Http;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Net.Compression.Tests
{
    [TestClass()]
    public class CompressorManagerTests
    {
        private const int SmallTestDataSize = 64 * 1024;        // 64KB
        private const int LargeTestDataSize = 896 * 1024;       // 896KB - realistic web asset size
        private const int PerformanceTestDataSize = 10 * 1024 * 1024; // 10MB for perf tests
        [TestMethod]
        public void NativeLibApiTest()
        {
            using NativeCompressionLib lib = NativeCompressionLib.LoadDefault();
            using LibTestComp compressor = new(lib, CompressionLevel.Fastest);

            Debug.WriteLine("Testing native library API with compression level: Fastest");

            TestSupportedMethodsContract(compressor);
            TestCompressionForAllSupportedMethods(compressor, SmallTestDataSize);
        }

        [TestMethod()]
        public void CompressorManagerBasicTest()
        {
            CompressorManager manager = CreateTestCompressorManager();

            // Test null argument guards in DEBUG builds
#if DEBUG
            Assert.ThrowsExactly<ArgumentNullException>(() => _ = manager.InitCompressor(null!, CompressionMethod.Deflate));
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.DeinitCompressor(null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.CommitMemory(null!));
            Assert.ThrowsExactly<ArgumentNullException>(() => manager.DecommitMemory(null!));
#endif

            ManagerTestComp compressor = new(manager.AllocCompressor(), manager);

            TestSupportedMethodsContract(compressor);
            TestCompressionForAllSupportedMethods(compressor, SmallTestDataSize);
        }

        [TestMethod]
        public void CommitDecommitLifecycleTest()
        {
            CompressorManager manager = CreateTestCompressorManager();
            object compressorState = manager.AllocCompressor();

            // Test full commit/decommit lifecycle
            manager.CommitMemory(compressorState);

            try
            {
                // Test with each supported method
                CompressionMethod supportedMethods = manager.GetSupportedMethods();

                if ((supportedMethods & CompressionMethod.Gzip) != 0)
                {
                    TestCompressorMethodWithCommittedState(manager, compressorState, CompressionMethod.Gzip, LargeTestDataSize);
                }

                if ((supportedMethods & CompressionMethod.Deflate) != 0)
                {
                    TestCompressorMethodWithCommittedState(manager, compressorState, CompressionMethod.Deflate, LargeTestDataSize);
                }

                if ((supportedMethods & CompressionMethod.Brotli) != 0)
                {
                    TestCompressorMethodWithCommittedState(manager, compressorState, CompressionMethod.Brotli, LargeTestDataSize);
                }

                if ((supportedMethods & CompressionMethod.Zstd) != 0)
                {
                    // Todo support zstd
                    // TestCompressorMethodWithCommittedState(manager, compressorState, CompressionMethod.Zstd, LargeTestDataSize);
                    Assert.Inconclusive("Zstd is not yet supported in commit mode");
                }
            }
            finally
            {
                manager.DecommitMemory(compressorState);
            }

        }

        [TestMethod]
        public void ReuseCommittedStateAcrossMethodsTest()
        {
            CompressorManager manager = CreateTestCompressorManager();
            object compressorState = manager.AllocCompressor();
            CompressionMethod supportedMethods = manager.GetSupportedMethods();

            manager.CommitMemory(compressorState);

            try
            {
                byte[] testData = CreateTestData(LargeTestDataSize);

                // Test reusing state across different methods

                foreach (CompressionMethod method in GetSupportedMethodsArray(supportedMethods))
                {
                    if (method == CompressionMethod.Zstd)
                    {
                        Assert.Inconclusive("Zstd is not yet supported in commit mode");
                        continue;
                    }

                    TestSingleCompressionCycle(manager, compressorState, method, testData);
                }               
            }
            finally
            {
                manager.DecommitMemory(compressorState);
            }
        }

        [TestMethod]
        public void CommitUnsupportedMethodRecoveryTest()
        {
            CompressorManager manager = CreateTestCompressorManager();
            object compressorState = manager.AllocCompressor();
            CompressionMethod supportedMethods = manager.GetSupportedMethods();

            manager.CommitMemory(compressorState);

            try
            {
                // Attempt to initialize with unsupported method
                Assert.ThrowsExactly<NotSupportedException>(() => 
                    manager.InitCompressor(compressorState, (CompressionMethod)4500)
                );

                // State should still be usable with supported method
                CompressionMethod[] supportedArray = GetSupportedMethodsArray(supportedMethods);

                if (supportedArray.Length > 0)
                {
                    byte[] testData = CreateTestData(SmallTestDataSize);

                    TestSingleCompressionCycle(manager, compressorState, supportedArray[0], testData);
                }
            }
            finally
            {
                manager.DecommitMemory(compressorState);
            }
        }

        [TestMethod]
        public void LegacyVsCommitOutputParityTest()
        {
            CompressorManager manager = CreateTestCompressorManager();
            CompressionMethod supportedMethods = manager.GetSupportedMethods();
        
            byte[] testData = CreateTestData(LargeTestDataSize);          

            foreach (CompressionMethod method in GetSupportedMethodsArray(supportedMethods))
            {
                if (method == CompressionMethod.Zstd)
                {
                    Assert.Inconclusive("Zstd is not yet supported in commit mode");
                    continue;
                }

                // Test legacy path (no explicit commit)
                byte[] legacyCompressed = CompressDataWithManager(manager, method, testData, useCommitApi: false);
                byte[] legacyDecompressed = DecompressData(legacyCompressed, method);

                // Test commit path 
                byte[] commitCompressed = CompressDataWithManager(manager, method, testData, useCommitApi: true);
                byte[] commitDecompressed = DecompressData(commitCompressed, method);

                // Both should decompress to original data
                Assert.IsTrue(
                    testData.SequenceEqual(legacyDecompressed), 
                    $"Legacy path failed to preserve data for {method}"
                );
                
                Assert.IsTrue(
                    testData.SequenceEqual(commitDecompressed), 
                    $"Commit path failed to preserve data for {method}"
                );               
            }
        }

        [TestMethod]
        public void DoubleCommitIsSafeTest()
        {
            CompressorManager manager = CreateTestCompressorManager();
            object compressorState = manager.AllocCompressor();

            // First commit should succeed
            manager.CommitMemory(compressorState);

            // Second commit should be safe (idempotent)
            manager.CommitMemory(compressorState);

            try
            {
                // State should still be usable
                CompressionMethod supportedMethods = manager.GetSupportedMethods();
                CompressionMethod[] methodsArray = GetSupportedMethodsArray(supportedMethods);

                if (methodsArray.Length > 0)
                {
                    byte[] testData = CreateTestData(SmallTestDataSize);
                    TestSingleCompressionCycle(manager, compressorState, methodsArray[0], testData);
                }
            }
            finally
            {
                manager.DecommitMemory(compressorState);

                // After decommit, subsequent operations should also be safe
                manager.DecommitMemory(compressorState);
            }
        }

        [TestMethod()]
        public void CompressorPerformanceTest()
        {
            const CompressionLevel testLevel = CompressionLevel.Fastest;
            const int testIterations = 5;

            PrintSystemInformation();

            using NativeCompressionLib lib = NativeCompressionLib.LoadDefault();

            byte[] testData = CreateTestData(PerformanceTestDataSize);

            using LibTestComp compressor = new(lib, testLevel);

            CompressionMethod supported = compressor.GetSupportedMethods();

            for (int iteration = 0; iteration < testIterations; iteration++)
            {
                if ((supported & CompressionMethod.Gzip) > 0)
                {
                    TestSingleCompressorPerformance(compressor, CompressionMethod.Gzip, testLevel, testData);
                }

                if ((supported & CompressionMethod.Deflate) > 0)
                {
                    TestSingleCompressorPerformance(compressor, CompressionMethod.Deflate, testLevel, testData);
                }

                if ((supported & CompressionMethod.Brotli) > 0)
                {
                    TestSingleCompressorPerformance(compressor, CompressionMethod.Brotli, testLevel, testData);
                }

                if ((supported & CompressionMethod.Zstd) > 0)
                {
                    Assert.Inconclusive("Zstd is not yet supported in commit mode");
                }
            }
        }

        #region Helper Methods

        /// <summary>
        /// Creates a test data buffer of the specified size filled with random data
        /// </summary>
        private static byte[] CreateTestData(int size) => RandomNumberGenerator.GetBytes(size);

        /// <summary>
        /// Gets the JSON configuration for the compressor manager
        /// </summary>
        private static string GetCompressorConfig()
        {
            using VnMemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms))
            {
                writer.WriteStartObject();
                writer.WriteStartObject("vnlib.net.compression");
                writer.WriteNumber("level", 1);
                // Note: lib_path is intentionally commented out to use default discovery
                // writer.WriteString("lib_path", "vnlib_compress.dll");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(ms.AsSpan());
        }

        /// <summary>
        /// Tests single compressor performance comparing native vs managed implementations
        /// </summary>
        private static void TestSingleCompressorPerformance(LibTestComp compressor,  CompressionMethod method, CompressionLevel level, byte[] testData)
        {
            byte[] outputBuffer = new byte[8 * 1024];
            long nativeTicks;

            Stopwatch stopwatch = new();
            {
                stopwatch.Start();

                compressor.InitCompressor(method);

                try
                {
                    ForwardOnlyMemoryReader<byte> reader = new(testData);

                    while (reader.WindowSize > 0)
                    {
                        CompressionResult result = compressor.CompressBlock(reader.Window, outputBuffer);
                        reader.Advance(result.BytesRead);
                    }

                    // Flush all remaining data
                    while (compressor.Flush(outputBuffer) != 0)
                    { }
                }
                finally
                {
                    compressor.DeinitCompressor();

                    stopwatch.Stop();
                }
            }

            nativeTicks = stopwatch.ElapsedTicks;

            // Test managed implementation for comparison
            using (Stream compStream = CreateCompressionStream(Stream.Null, method, level))
            {
                stopwatch.Restart();
                try
                {
                    compStream.Write(testData, 0, testData.Length);
                }
                finally
                {
                    stopwatch.Stop();
                }
            }

            long streamMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            long nativeMicroseconds = TicksToMicroseconds(nativeTicks);
            string winner = nativeMicroseconds < streamMicroseconds ? "native" : "managed";

            Debug.WriteLine($"{method}: {testData.Length} bytes, {nativeMicroseconds}μs vs {streamMicroseconds}μs. Winner: {winner}");
        }

        /// <summary>
        /// Converts ticks to microseconds for performance measurement
        /// </summary>
        private static long TicksToMicroseconds(long ticks) => ticks / (TimeSpan.TicksPerMillisecond / 1000);

        /// <summary>
        /// Creates and configures a CompressorManager for testing
        /// </summary>
        private static CompressorManager CreateTestCompressorManager()
        {
            CompressorManager manager = new();
            string config = GetCompressorConfig();

            using JsonDocument doc = JsonDocument.Parse(config);
            manager.OnLoad(null, doc.RootElement);

            return manager;
        }

        /// <summary>
        /// Gets an array of supported compression methods from the flags enum
        /// </summary>
        private static CompressionMethod[] GetSupportedMethodsArray(CompressionMethod supportedMethods)
        {
            List<CompressionMethod> methods = [];

            if ((supportedMethods & CompressionMethod.Gzip) != 0)
            {
                methods.Add(CompressionMethod.Gzip);
            }

            if ((supportedMethods & CompressionMethod.Deflate) != 0)
            {
                methods.Add(CompressionMethod.Deflate);
            }

            if ((supportedMethods & CompressionMethod.Brotli) != 0)
            {
                methods.Add(CompressionMethod.Brotli);
            }

            if ((supportedMethods & CompressionMethod.Zstd) != 0)
            {
                methods.Add(CompressionMethod.Zstd);
            }

            return [.. methods];
        }

        /// <summary>
        /// Tests a single compression cycle with committed state
        /// </summary>
        private static void TestCompressorMethodWithCommittedState(
            CompressorManager manager, 
            object compressorState, 
            CompressionMethod method, 
            int dataSize
        )
        {
            byte[] testData = CreateTestData(dataSize);
            TestSingleCompressionCycle(manager, compressorState, method, testData);
        }

        /// <summary>
        /// Performs a complete compression cycle with the given state and method
        /// </summary>
        private static void TestSingleCompressionCycle(
            CompressorManager manager, 
            object compressorState, 
            CompressionMethod method, 
            byte[] testData
        )
        {
            int blockSize = manager.InitCompressor(compressorState, method);
            Assert.AreEqual(0, blockSize, "Block size should be 0 for current implementation");

            try
            {
                byte[] compressedData = CompressDataWithManager(manager, compressorState, testData);
                byte[] decompressedData = DecompressData(compressedData, method);

                Assert.IsTrue(
                    testData.SequenceEqual(decompressedData), 
                    $"Data integrity failed for method {method}"
                );
            }
            finally
            {
                manager.DeinitCompressor(compressorState);
            }
        }

        /// <summary>
        /// Compresses data using the manager and compressor state
        /// </summary>
        private static byte[] CompressDataWithManager(CompressorManager manager, object compressorState, byte[] inputData)
        {
            using VnMemoryStream outputStream = new();
            byte[] outputBuffer = new byte[4096];

            ForwardOnlyMemoryReader<byte> reader = new(inputData);

            // Compress data in chunks
            while (reader.WindowSize > 0)
            {
                CompressionResult result = manager.CompressBlock(
                    compressorState, 
                    reader.Window, 
                    outputBuffer
                );

                outputStream.Write(outputBuffer, 0, result.BytesWritten);

                reader.Advance(result.BytesRead);
            }

            // Flush remaining data
            int flushed;
            do
            {
                flushed = manager.Flush(compressorState, outputBuffer);
                if (flushed > 0)
                {
                    outputStream.Write(outputBuffer.AsSpan()[0..flushed]);
                }
            } while (flushed > 0);

            return outputStream.ToArray();
        }

        /// <summary>
        /// Compresses data using legacy path (no explicit commit)
        /// </summary>
        private static byte[] CompressDataWithManager(
            CompressorManager manager, 
            CompressionMethod method, 
            byte[] testData,
            bool useCommitApi
        )
        {
            object compressorState = manager.AllocCompressor();

            if (useCommitApi) manager.CommitMemory(compressorState);

            manager.InitCompressor(compressorState, method);

            try
            {
                return CompressDataWithManager(manager, compressorState, testData);
            }
            finally
            {
                manager.DeinitCompressor(compressorState);

                if (useCommitApi) manager.DecommitMemory(compressorState);
            }
        }
      
        /// <summary>
        /// <summary>
        /// Decompresses data from a memory stream using the specified method
        /// </summary>
        private static byte[] DecompressData(VnMemoryStream inputStream, CompressionMethod method)
        {
            inputStream.Position = 0;

            using VnMemoryStream outputStream = new();
            using (Stream decompressionStream = CreateDecompressionStream(inputStream, method))
            {
                decompressionStream.CopyTo(outputStream);
            }

            return outputStream.ToArray();
        }

        /// <summary>
        /// Decompresses data from a byte array using the specified method
        /// </summary>
        private static byte[] DecompressData(byte[] compressedData, CompressionMethod method)
        {
            using VnMemoryStream inputStream = new();
            inputStream.Write(compressedData);
            return DecompressData(inputStream, method);
        }

        /// <summary>
        /// Creates a decompression stream for the specified method
        /// </summary>
        private static Stream CreateDecompressionStream(Stream input, CompressionMethod method)
        {
            return method switch
            {
                CompressionMethod.Gzip => new GZipStream(input, CompressionMode.Decompress, true),
                CompressionMethod.Deflate => new DeflateStream(input, CompressionMode.Decompress, true),
                CompressionMethod.Brotli => new BrotliStream(input, CompressionMode.Decompress, true),
                _ => throw new ArgumentException($"Unsupported compression method: {method}", nameof(method)),
            };
        }

        /// <summary>
        /// Creates a compression stream for the specified method and level
        /// </summary>
        private static Stream CreateCompressionStream(Stream output, CompressionMethod method, CompressionLevel level)
        {
            return method switch
            {
                CompressionMethod.Gzip => new GZipStream(output, level, true),
                CompressionMethod.Deflate => new DeflateStream(output, level, true),
                CompressionMethod.Brotli => new BrotliStream(output, level, true),
                _ => throw new ArgumentException($"Unsupported compression method: {method}", nameof(method)),
            };
        }

        /// <summary>
        /// Prints system information for performance test context
        /// </summary>
        private static void PrintSystemInformation()
        {
            string systemInfo = $@"
OS: {RuntimeInformation.OSDescription}
Framework: {RuntimeInformation.FrameworkDescription}
Processor: {RuntimeInformation.ProcessArchitecture}
Platform ID: {Environment.OSVersion.Platform}
Is 64 bit: {Environment.Is64BitOperatingSystem}
Is 64 bit process: {Environment.Is64BitProcess}
Processor Count: {Environment.ProcessorCount}
Page Size: {Environment.SystemPageSize}
";
            Debug.WriteLine(systemInfo);
        }

        #endregion

        /// <summary>
        /// Tests the contract for supported methods and basic state validation
        /// </summary>
        private static void TestSupportedMethodsContract(ITestCompressor compressor)
        {
            // Verify operations fail when not initialized
            Assert.ThrowsExactly<InvalidOperationException>(() => compressor.CompressBlock(default, default));
            Assert.ThrowsExactly<InvalidOperationException>(() => compressor.Flush(default));
            Assert.ThrowsExactly<InvalidOperationException>(compressor.DeinitCompressor);

            // Test unsupported methods
            Assert.ThrowsExactly<NotSupportedException>(() => compressor.InitCompressor(CompressionMethod.None));
            Assert.ThrowsExactly<NotSupportedException>(() => compressor.InitCompressor((CompressionMethod)4500));

            CompressionMethod supported = compressor.GetSupportedMethods();
            Assert.AreNotEqual(CompressionMethod.None, supported, "At least one compression method must be supported");

            // Test basic init/deinit cycle for each supported method
            TestBasicInitDeinitCycle(compressor, supported);

            Debug.WriteLine($"Compressor supports: {supported}");
        }

        /// <summary>
        /// Tests basic initialization and deinitialization for supported methods
        /// </summary>
        private static void TestBasicInitDeinitCycle(ITestCompressor compressor, CompressionMethod supportedMethods)
        {
            CompressionMethod[] methods = GetSupportedMethodsArray(supportedMethods);

            foreach (CompressionMethod method in methods)
            {
                compressor.InitCompressor(method);
                compressor.DeinitCompressor();

                // Second deinit should fail
                Assert.ThrowsExactly<InvalidOperationException>(compressor.DeinitCompressor);
            }
        }

        /// <summary>
        /// Tests compression for all supported methods
        /// </summary>
        private static void TestCompressionForAllSupportedMethods(ITestCompressor compressor, int dataSize)
        {
            CompressionMethod supportedMethods = compressor.GetSupportedMethods();

            foreach (CompressionMethod method in GetSupportedMethodsArray(supportedMethods))
            {
                if (method == CompressionMethod.Zstd)
                {
                    Assert.Inconclusive("Zstd is not yet supported in commit mode");
                    continue;
                }

                TestCompressorMethod(compressor, method, dataSize);
            }
        }

        /// <summary>
        /// Tests a complete compression/decompression cycle for a specific method
        /// </summary>
        private static void TestCompressorMethod(ITestCompressor compressor, CompressionMethod method, int dataSize)
        {
            int blockSize = compressor.InitCompressor(method);
            Assert.AreEqual(0, blockSize, "Block size should be 0 for current implementation");

            try
            {
                using VnMemoryStream outputStream = new();
                byte[] testData = CreateTestData(dataSize);
                byte[] outputBuffer = new byte[4096];

                ForwardOnlyMemoryReader<byte> reader = new(testData);

                // Compress data in chunks
                while (reader.WindowSize > 0)
                {
                    CompressionResult result = compressor.CompressBlock(reader.Window, outputBuffer);

                    outputStream.Write(outputBuffer, 0, result.BytesWritten);

                    reader.Advance(result.BytesRead);
                }

                // Flush remaining data
                int flushed;
                do
                {
                    flushed = compressor.Flush(outputBuffer);

                    if (flushed > 0)
                    {
                        outputStream.Write(outputBuffer.AsSpan()[0..flushed]);
                    }

                } while (flushed > 0);

                // Verify compressed data can be decompressed correctly
                byte[] decompressedData = DecompressData(outputStream, method);
                
                Assert.IsTrue(
                    testData.SequenceEqual(decompressedData), 
                    $"Compression method {method} failed to preserve data integrity"
                );
            }
            finally
            {
                compressor.DeinitCompressor();
            }
        }

        #region Test Adapter Classes

        /// <summary>
        /// Test compressor interface for uniform testing
        /// </summary>
        interface ITestCompressor
        {
            int InitCompressor(CompressionMethod method);

            void DeinitCompressor();

            CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output);

            int Flush(Memory<byte> buffer);

            CompressionMethod GetSupportedMethods();
        }

        /// <summary>
        /// Test adapter for CompressorManager
        /// </summary>
        sealed class ManagerTestComp(object Compressor, CompressorManager Manager) : ITestCompressor
        {
            public CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output)
                => Manager.CompressBlock(Compressor, input, output);

            public void DeinitCompressor()
                => Manager.DeinitCompressor(Compressor);

            public int Flush(Memory<byte> buffer)
                => Manager.Flush(Compressor, buffer);

            public CompressionMethod GetSupportedMethods()
                => Manager.GetSupportedMethods();

            public int InitCompressor(CompressionMethod method)
                => Manager.InitCompressor(Compressor, method);
        }

        /// <summary>
        /// Test adapter for NativeCompressionLib
        /// </summary>
        sealed class LibTestComp(NativeCompressionLib Library, CompressionLevel Level) : ITestCompressor, IDisposable
        {
            /*
             * This test compressor is disposable because it's possible during testing that the deinit function
             * is not called while testing other aspects of the API. When using a native compressor (which is diposable)
             * it's assumed that a normal dispose pattern will clean things up. So I think disposable is appropriate here.
             */

            private INativeCompressor? _compressor;

            public CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output)
            {
                _ = _compressor ?? throw new InvalidOperationException("Test compressor was not initialized yet");
                return _compressor.Compress(input, output);
            }

            public void DeinitCompressor()
            {
                _ = _compressor ?? throw new InvalidOperationException("Test compressor was not initialized yet");

                _compressor.Dispose();
                _compressor = null;
            }

            public int Flush(Memory<byte> buffer)
            {
                _ = _compressor ?? throw new InvalidOperationException("Test compressor was not initialized yet");
                return _compressor.Flush(buffer);
            }

            public CompressionMethod GetSupportedMethods() 
                => Library.GetSupportedMethods();

            public int InitCompressor(CompressionMethod method)
            {
                if (_compressor != null)
                {
                    throw new InvalidOperationException("Test compressor has already been allocated");
                }

                _compressor = Library.AllocCompressor(method, Level);
                return (int)_compressor.GetBlockSize();
            }

            public void Dispose()
            {
                if (_compressor is not null)
                {
                    _compressor.Dispose();
                    _compressor = null;
                }    
            }
        }

        #endregion
    }
}
