# VNLib.Net.Compression

Provides a cross platform (w/ cmake) native compression DLL (vnlib_compress) for Brotli, Deflate, Gzip, and Zstd compression encodings for dynamic HTTP response streaming of arbitrary data. This directory also provides a managed C# implementation with support for runtime loading into vnlib.plugins.runtime applications.

The native library relies on source code (which are statically compiled) for Brotli, Zlib, and Zstd. The original repositories for all libraries will do, but I use the Cloudflare fork of Zlib for testing. You should consult my documentation below for how and where to get the source for these libraries. 

## Builds

## Docs and Guides
Documentation, specifications, and setup guides are available on my website.

[Docs and Articles](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_vnlib.net.compression)  
[Builds and Source](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core)  

## License 
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version). See the LICENSE files for more information.