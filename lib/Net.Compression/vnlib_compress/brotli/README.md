# Google Brotli Compression C Library

### Introduction (Original)

Brotli is a generic-purpose lossless compression algorithm that compresses data
using a combination of a modern variant of the LZ77 algorithm, Huffman coding
and 2nd order context modeling, with a compression ratio comparable to the best
currently available general-purpose compression methods. It is similar in speed
with deflate but offers more dense compression.

The specification of the Brotli Compressed Data Format is defined in [RFC 7932](https://tools.ietf.org/html/rfc7932).

Brotli is open-sourced under the MIT License, see the LICENSE file.

> **Please note:** brotli is a "stream" format; it does not contain
> meta-information, like checksums or uncompresssed data length. It is possible
> to modify "raw" ranges of the compressed stream and the decoder will not
> notice that.

## VNLib Modifications
This library is vendored from the official [Google Brotli repository](https://github.com/google/brotli) and has been modified to be built exclusivly as a C static library using CMake. The package has been trimmed to exactly the bare minimum needed to build the C library. I will likely trim this even more to include only the necessary features required for network compression.

## Updates
As of late 2023, Google no longer publishes releases or stable tags but the continually push updates. I must manually verify and test stability and compatability directly from the latest branch (usually master). I will try to keep this library up to date with the latest changes from the official repository and ensure the stability and compatability is preserved as best as I can.

## License
This project was licensed to me under the MIT License. The original license can be found in the [LICENSE](LICENSE) file, and these changes will be licensed to you under the same terms :).