using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
using System.Security.Cryptography;

using VNLib.Utils.IO;
using VNLib.Net.Http;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VNLib.Net.Compression.Tests
{
    [TestClass()]
    public class CompressorManagerTests
    {
        const string LIB_PATH = @"F:\Programming\VNLib\VNLib.Net.Compression\native\vnlib_compress\build\Debug\vnlib_compress.dll";
     

        [TestMethod()]
        public void OnLoadTest()
        {
            CompressorManager manager = InitCompressorUnderTest();

            //Allocate a compressor instance
            object? compressor = manager.AllocCompressor();

            Assert.IsNotNull(compressor);

            //Test all 3 compression methods
            TestCompressorMethod(manager, compressor, CompressionMethod.Brotli);
            TestCompressorMethod(manager, compressor, CompressionMethod.Gzip);
            TestCompressorMethod(manager, compressor, CompressionMethod.Deflate);
        }

        [TestMethod()]
        public void InitCompressorTest()
        {
            CompressorManager manager = InitCompressorUnderTest();

            //Allocate a compressor instance
            object compressor = manager.AllocCompressor();

            Assert.IsNotNull(compressor);

            Assert.ThrowsException<ArgumentNullException>(() => manager.InitCompressor(null!, CompressionMethod.Deflate));
            Assert.ThrowsException<ArgumentNullException>(() => manager.DeinitCompressor(null!));

            //Make sure error occurs with non-supported comp
            Assert.ThrowsException<ArgumentException>(() => { manager.InitCompressor(compressor, CompressionMethod.None); });

            //test out of range, this should be a native lib error
            Assert.ThrowsException<NotSupportedException>(() => { manager.InitCompressor(compressor, (CompressionMethod)24); });

            //Test all 3 compression methods
            CompressionMethod supported = manager.GetSupportedMethods();

            if ((supported & CompressionMethod.Gzip) > 0)
            {
                //Make sure no error occurs with supported comp
                manager.InitCompressor(compressor, CompressionMethod.Gzip);
                manager.DeinitCompressor(compressor);
            }

            if((supported & CompressionMethod.Brotli) > 0)
            {
                //Make sure no error occurs with supported comp
                manager.InitCompressor(compressor, CompressionMethod.Brotli);
                manager.DeinitCompressor(compressor);
            }

            if((supported & CompressionMethod.Deflate) > 0)
            {
                //Make sure no error occurs with supported comp
                manager.InitCompressor(compressor, CompressionMethod.Deflate);
                manager.DeinitCompressor(compressor);
            }
        }

        private static CompressorManager InitCompressorUnderTest()
        {
            CompressorManager manager = new();

            //Get the json config string
            string config = GetCompConfig();

            using JsonDocument doc = JsonDocument.Parse(config);

            //Attempt to load the native library
            manager.OnLoad(null, doc.RootElement);

            //Get supported methods
            CompressionMethod methods = manager.GetSupportedMethods();

            //Verify that at least one method is supported
            Assert.IsFalse(methods == CompressionMethod.None);

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

        private static void TestCompressorMethod(CompressorManager manager, object compressor, CompressionMethod method)
        {
            /*
             * This test method initalizes a new compressor instance of the desired type
             * creates a test data buffer, compresses it using the compressor instance
             * then decompresses the compressed data using a managed decompressor as 
             * a reference and compares the results.
             * 
             * The decompression must be able to recover the original data.
             */

            //Time to initialize the compressor
            int blockSize = manager.InitCompressor(compressor, method);

            Assert.IsTrue(blockSize == 0);

            try
            {
                using VnMemoryStream outputStream = new();

                //Create a buffer to compress
                byte[] buffer = new byte[4096];

                //fill with random data
                RandomNumberGenerator.Fill(buffer);

                //try to compress the data in chunks
                for(int i = 0; i < 4; i++)
                {
                    //Get 4th of a buffer
                    ReadOnlyMemory<byte> chunk = buffer.AsMemory(i * 1024, 1024);

                    //Compress data
                    ReadOnlyMemory<byte> output = manager.CompressBlock(compressor, chunk, i == 3);

                    //Write the compressed data to the output stream
                    outputStream.Write(output.Span);
                }

                //flush the compressor
                while(true)
                {
                    ReadOnlyMemory<byte> output = manager.Flush(compressor);
                    if(output.IsEmpty)
                    {
                        break;
                    }
                    outputStream.Write(output.Span);
                }

                //Verify the data
                byte[] decompressed = DecompressData(outputStream, method);

                Assert.IsTrue(buffer.SequenceEqual(decompressed));
            }
            finally
            {
                //Always deinitialize the compressor when done
                manager.DeinitCompressor(compressor);
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
    }
}