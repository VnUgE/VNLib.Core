# Cloudflare zlib Fork C Library

### Introduction (Original)

This is a fork of zlib with performance improvements developed for use at Cloudflare.

This implementation only supports x86-64 with SSE 4.2+ and aarch64 with NEON & CRC. 32-bit CPUs are not supported at all.

The API and ABI are compatible with the original zlib.

## VNLib Modifications
This library is vendored from the [Cloudflare ZLIB repository](https://github.com/cloudflare/zlib) and **has been modified** to be built exclusivly as a C static library using CMake. The package has been trimmed to exactly the bare minimum needed to build the C library. I will likely trim this even more to include only the necessary features required for network compression.

## Updates
Cloudflare does not publish releases or stable tags but will regularlly push enhancing commits. So, I must manually verify and test stability and compatability directly from the latest branch (usually gcc.amd64). I will try to keep this library up to date with the latest changes from the official repository and ensure the stability and compatability is preserved as best as I can.

## License
This project was licensed under non-standard terms from Mark Adler and forwaded by me. The original license can be found in the [LICENSE](LICENSE) file.