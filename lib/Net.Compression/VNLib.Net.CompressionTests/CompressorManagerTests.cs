using System;
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
        const string LIB_PATH = @"../../../../vnlib_compress/build/Debug/vnlib_compress.dll";

        [TestMethod]
        public void NativeLibApiTest()
        {
            //Load library
            using NativeCompressionLib lib = NativeCompressionLib.LoadLibrary(LIB_PATH, DllImportSearchPath.SafeDirectories);

            LibTestComp cp = new(lib, CompressionLevel.Fastest);

            Debug.WriteLine("Loading library with compression level: Fastest");

            TestSupportedMethods(cp);

            //Test for supported methods
            TestCompressionForSupportedMethods(cp);
        }       

        [TestMethod()]
        public void InitCompressorTest()
        {
            CompressorManager manager = InitCompressorUnderTest();

            Assert.ThrowsException<ArgumentNullException>(() => manager.InitCompressor(null!, CompressionMethod.Deflate));
            Assert.ThrowsException<ArgumentNullException>(() => manager.DeinitCompressor(null!));

            //Allocate a compressor instance
            object compressor = manager.AllocCompressor();

            Assert.IsNotNull(compressor);

            //Create a new testing wrapper
            ManagerTestComp cp = new(compressor, manager);

            //Test supported methods
            TestSupportedMethods(cp);

            //Test for supported methods
            TestCompressionForSupportedMethods(cp);
        }

        [TestMethod()]
        public void CompressorPerformanceTest()
        {
            //Set test level
            const CompressionLevel testLevel = CompressionLevel.Fastest;
            const int testItterations = 5;

            PrintSystemInformation();

            //Load native library
            using NativeCompressionLib lib = NativeCompressionLib.LoadLibrary(LIB_PATH, DllImportSearchPath.SafeDirectories);

            //Huge array of random data to compress
            byte[] testData = RandomNumberGenerator.GetBytes(10 * 1024 * 1024);

            LibTestComp cp = new(lib, testLevel);

            CompressionMethod supported = cp.GetSupportedMethods();

            for (int itterations = 0; itterations < testItterations; itterations++)
            {
                if ((supported & CompressionMethod.Gzip) > 0)
                {
                    TestSingleCompressor(cp, CompressionMethod.Gzip, testLevel, testData);
                }

                if ((supported & CompressionMethod.Deflate) > 0)
                {
                    TestSingleCompressor(cp, CompressionMethod.Deflate, testLevel, testData);
                }

                if ((supported & CompressionMethod.Brotli) > 0)
                {
                    TestSingleCompressor(cp, CompressionMethod.Brotli, testLevel, testData);
                }
            }
        }

        private static void TestSingleCompressor(LibTestComp comp, CompressionMethod method, CompressionLevel level, byte[] testData) 
        {
            byte[] outputBlock = new byte[8 * 1024];
            long nativeTicks;

            Stopwatch stopwatch = new ();
            {
                //Start sw
                stopwatch.Start();
                comp.InitCompressor(method);
                try
                {
                    ForwardOnlyReader<byte> reader = new(outputBlock);

                    while (true)
                    {
                        CompressionResult result = comp.CompressBlock(reader.Window, outputBlock);
                        reader.Advance(result.BytesRead);

                        if (reader.WindowSize == 0)
                        {
                            break;
                        }
                    }

                    //Flush all data
                    while (comp.Flush(outputBlock) != 0)
                    { }
                }
                finally
                {
                    //Include deinit
                    comp.DeinitCompressor();
                    stopwatch.Stop();                    
                }
            }

            nativeTicks = stopwatch.ElapsedTicks;

            //Switch to managed test
            using (Stream compStream = GetEncodeStream(Stream.Null, method, level))
            {
                stopwatch.Restart();
                try
                {
                    //Write the block to the compression stream
                    compStream.Write(testData, 0, testData.Length);
                }
                finally
                {
                    stopwatch.Stop();
                }
            }

            long streamMicroseconds = TicksToMicroseconds(stopwatch.ElapsedTicks);
            long nativeMicroseconds = TicksToMicroseconds(nativeTicks);

            string winner = nativeMicroseconds < streamMicroseconds ? "native" : "stream";

            Debug.WriteLine($"{method}: {testData.Length} bytes, {nativeMicroseconds}misec vs {streamMicroseconds}misec. Winner {winner}");
        }

        static long TicksToMicroseconds(long ticks) => ticks / (TimeSpan.TicksPerMillisecond / 1000);

        private static CompressorManager InitCompressorUnderTest()
        {
            CompressorManager manager = new();

            //Get the json config string
            string config = GetCompConfig();

            using JsonDocument doc = JsonDocument.Parse(config);

            //Attempt to load the native library
            manager.OnLoad(null, doc.RootElement);

            return manager;
        }

        private static string GetCompConfig()
        {
            using VnMemoryStream ms = new();
            using (Utf8JsonWriter writer = new(ms))
            {
                writer.WriteStartObject();

                writer.WriteStartObject("vnlib.net.compression");

                writer.WriteNumber("level", 1);
                writer.WriteString("lib_path", LIB_PATH);

                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(ms.AsSpan());
        }
       

        private static void TestCompressionForSupportedMethods(ITestCompressor testCompressor)
        {
            //Get the compressor's supported methods
            CompressionMethod methods = testCompressor.GetSupportedMethods();

            //Make sure at least on method is supported by the native lib
            Assert.IsFalse(methods == CompressionMethod.None);

            //Test for brotli support
            if ((methods & CompressionMethod.Brotli) > 0)
            {
                TestCompressorMethod(testCompressor, CompressionMethod.Brotli);
            }

            //Test for deflate support
            if ((methods & CompressionMethod.Deflate) > 0)
            {
                TestCompressorMethod(testCompressor, CompressionMethod.Deflate);
            }

            //Test for gzip support
            if ((methods & CompressionMethod.Gzip) > 0)
            {
                TestCompressorMethod(testCompressor, CompressionMethod.Gzip);
            }
        }

        private static void TestSupportedMethods(ITestCompressor compressor)
        {
            //Make sure error occurs with non-supported comp
            Assert.ThrowsException<NotSupportedException>(() => { compressor.InitCompressor(CompressionMethod.None); });

            //test out of range, this should be a native lib error
            Assert.ThrowsException<NotSupportedException>(() => { compressor.InitCompressor((CompressionMethod)4500); });

            //Test all 3 compression methods
            CompressionMethod supported = compressor.GetSupportedMethods();

            if ((supported & CompressionMethod.Gzip) > 0)
            {
                //Make sure no error occurs with supported comp
                compressor.InitCompressor(CompressionMethod.Gzip);
                compressor.DeinitCompressor();
            }

            if ((supported & CompressionMethod.Brotli) > 0)
            {
                //Make sure no error occurs with supported comp
                compressor.InitCompressor(CompressionMethod.Brotli);
                compressor.DeinitCompressor();
            }

            if ((supported & CompressionMethod.Deflate) > 0)
            {
                //Make sure no error occurs with supported comp
                compressor.InitCompressor(CompressionMethod.Deflate);
                compressor.DeinitCompressor();
            }

            Debug.WriteLine($"Compressor library supports {supported}");
        }

        /*
         * This test method initalizes a new compressor instance of the desired type
         * creates a test data buffer, compresses it using the compressor instance
         * then decompresses the compressed data using a managed decompressor as 
         * a reference and compares the results.
         * 
         * The decompression must be able to recover the original data.
         */

        private static void TestCompressorMethod(ITestCompressor compressor, CompressionMethod method)
        {           

            //Time to initialize the compressor
            int blockSize = compressor.InitCompressor(method);

            /*
             * Currently not worrying about block size in the native lib, so this 
             * should cause tests to fail when block size is supported later on
             */
            Assert.IsTrue(blockSize == 0);

            try
            {
                using VnMemoryStream outputStream = new();

                //Create a buffer to compress
                byte[] buffer = new byte[1024000];
                byte[] output = new byte[4096];

                //fill with random data
                RandomNumberGenerator.Fill(buffer);

                ForwardOnlyMemoryReader<byte> reader = new(buffer);

                //try to compress the data in chunks
                while(reader.WindowSize > 0)
                {
                    //Compress data
                    CompressionResult result = compressor.CompressBlock(reader.Window, output);

                    //Write the compressed data to the output stream
                    outputStream.Write(output, 0, result.BytesWritten);

                    //Advance reader
                    reader.Advance(result.BytesRead);
                }

                //Flush
                int flushed = 100;
                while(flushed > 0)
                { 
                    flushed = compressor.Flush(output);

                    //Write the compressed data to the output stream
                    outputStream.Write(output.AsSpan()[0..flushed]);
                }

                //Verify the original data matches the decompressed data
                byte[] decompressed = DecompressData(outputStream, method);

                Assert.IsTrue(buffer.SequenceEqual(decompressed));

                Debug.WriteLine($"Compressor type {method} successfully compressed valid data");
            }
            finally
            {
                //Always deinitialize the compressor when done
                compressor.DeinitCompressor();
            }
        }

        private static byte[] DecompressData(VnMemoryStream inputStream, CompressionMethod method)
        {
            inputStream.Position = 0;

            //Stream to write output data to
            using VnMemoryStream output = new();

            //Get the requested stream type to decompress the data
            using (Stream gz = GetDecompStream(inputStream, method))
            {
                gz.CopyTo(output);
            }

            return output.ToArray();
        }

        private static Stream GetDecompStream(Stream input, CompressionMethod method) 
        {
            return method switch
            {
                CompressionMethod.Gzip => new GZipStream(input, CompressionMode.Decompress, true),
                CompressionMethod.Deflate => new DeflateStream(input, CompressionMode.Decompress, true),
                CompressionMethod.Brotli => new BrotliStream(input, CompressionMode.Decompress, true),
                _ => throw new ArgumentException("Unsupported compression method", nameof(method)),
            };
        }

        private static Stream GetEncodeStream(Stream output, CompressionMethod method, CompressionLevel level)
        {
            return method switch
            {
                CompressionMethod.Gzip => new GZipStream(output, level, true),
                CompressionMethod.Deflate => new DeflateStream(output, level, true),
                CompressionMethod.Brotli => new BrotliStream(output, level, true),
                _ => throw new ArgumentException("Unsupported compression method", nameof(method)),
            };
        }

        private static void PrintSystemInformation()
        {


            string sysInfo = @$"
OS: {RuntimeInformation.OSDescription}
Framework: {RuntimeInformation.FrameworkDescription}
Processor: {RuntimeInformation.ProcessArchitecture}
Platform ID: {Environment.OSVersion.Platform}
Is 64 bit: {Environment.Is64BitOperatingSystem}
Is 64 bit process: {Environment.Is64BitProcess}
Processor Count: {Environment.ProcessorCount}
Page Size: {Environment.SystemPageSize}
";
            Debug.WriteLine(sysInfo);
        }

        interface ITestCompressor
        {
            int InitCompressor(CompressionMethod method);

            void DeinitCompressor();

            CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output);

            int Flush(Memory<byte> buffer);

            CompressionMethod GetSupportedMethods();
        }

        sealed class ManagerTestComp(object Compressor, CompressorManager Manager) : ITestCompressor
        {
            public CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output) => Manager.CompressBlock(Compressor, input, output);

            public void DeinitCompressor() => Manager.DeinitCompressor(Compressor);

            public int Flush(Memory<byte> buffer) => Manager.Flush(Compressor, buffer);

            public CompressionMethod GetSupportedMethods() => Manager.GetSupportedMethods();

            public int InitCompressor(CompressionMethod level) => Manager.InitCompressor(Compressor, level);

        }

        sealed class LibTestComp(NativeCompressionLib Library, CompressionLevel Level) : ITestCompressor
        {
            private INativeCompressor? _comp;

            public CompressionResult CompressBlock(ReadOnlyMemory<byte> input, Memory<byte> output) => _comp!.Compress(input, output);

            public CompressionResult CompressBlock(ReadOnlySpan<byte> input, Span<byte> output) => _comp!.Compress(input, output);

            public void DeinitCompressor()
            {
                _comp!.Dispose();
                _comp = null;
            }

            public int Flush(Memory<byte> buffer) => _comp!.Flush(buffer);

            public CompressionMethod GetSupportedMethods() => Library.GetSupportedMethods();

            public int InitCompressor(CompressionMethod method)
            {
                _comp = Library.AllocCompressor(method, Level);
                return (int)_comp.GetBlockSize();
            }
        }
    }
}