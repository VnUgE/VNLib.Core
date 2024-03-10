# vnlib_monocypher
A native shared/dynamic library for transforming exporting Monocypher library functions for use with my .NET managed libraries. A vendored version of Monocypher is included in this directory, that I maintain.


Pre-built Windows binaries are available on my website (link below).  
Source code blobs are also packaged and ready for building. See the docs link below for building instructions.

## Builds
C libraries are packaged in source code (to compile locally) or Windows amd64 dll binaries and individually packaged source code are available on my website (link below). All tar-gzip (.tgz) files will have an associated checksum and PGP signature of the desired download file.

## Docs and Guides
Documentation, specifications, and setup guides are available on my website.

[Docs and Articles](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_vnlib.utils.cryptography)  
[Builds and Source](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core)  

## License 
Code is individually licensed. See the `LICENSE` file in each subdirectory for more information. 

## Notes

### Building from source
> This guide may become out-of-date, use the docs link above for the most recent information.  

This project uses the [CMake](https://cmake.org) build system for cross-platform compilation. My CMakelist.txt might not be perfect for your platform, so feel free to make a new issue or send me an email if you run into problems compiling on your platform.  

Download the package [source code src.tgz](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core?p=vnlib_monocypher) archive from my builds page.  

```bash
tar -xzf src.tgz
cmake -B./build/ -DCMAKE_BUILD_TYPE=Release
cmake --build ./build/ --config Release
```

On **Windows**, you should navigate to build/Release to see your `vnlib_monocypher.dll` file.  

On **Linux**, you should navigate to build/ to see your `libvn_monocypher.so` file.  