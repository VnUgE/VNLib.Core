# VNLib.Net.Compression

Provides a cross platform (w/ cmake) native compression DLL for Brotli, Deflate, and Gzip compression encodings for dynamic HTTP response streaming of arbitrary data. This directory also provides a managed implementation with support for runtime loading.

The native library relies on source code (which are statically compiled) for Brotli and Zlib. The original repositories for both libraries will do, but I use the Cloudflare fork of Zlib for testing. You should consult my documentation below for how and where to get the source for these libraries. 


### Builds
Debug build w/ symbols & xml docs, release builds, NuGet packages, and individually packaged source code are available on my website (link below). All tar-gzip (.tgz) files will have an associated .sha384 appended checksum of the desired download file.

### Docs and Guides
Documentation, specifications, and setup guides are available on my website.

[Docs and Articles](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_vnlib.net.compression)  
[Builds and Source](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core)  

## License 
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). See the LICENSE files for more information.