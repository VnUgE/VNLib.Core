# VNLib Mimalloc Heap

## Introduction

`vnlib_mimalloc` wraps [Microsoft's mimalloc](https://github.com/microsoft/mimalloc) allocator and
exposes it through the vnlib `NativeHeapApi`, so it can be used interchangeably with the other native
heap providers. Both a static and a shared library are produced.

Mimalloc is a general purpose allocator with excellent performance characteristics, a small and
well tested core, and support for cross thread private heaps. It is MIT licensed by Microsoft, see
the `vendor/LICENSE` file.

## Vendored source

The upstream source lives in the `vendor` directory and is pinned to a specific upstream commit,
which is recorded in the `vendor/COMMIT` file.

| | |
| --- | --- |
| Upstream | [microsoft/mimalloc](https://github.com/microsoft/mimalloc) |
| Pinned commit | `d4881d338125e1cb7c47ba4cfb398d6f7c0c8d45` (`v3.5.3`) |
| Upstream version | 3.5.3 |

The vendored tree is trimmed to the sources and headers needed to build the library. The upstream
CLI, tests, benchmarks, documentation and IDE project files are not included. The upstream
`CMakeLists.txt` is used unmodified so that upstream build options keep working. Our own CMake
wrapper disables the features this library does not need (malloc overrides, Windows redirection,
OSX interposition, OSX zone, xmalloc and sampled profiling).

## Upstream patches

The following upstream commits are cherry-picked on top of the pinned release and are not part of it:

| Upstream commit | Description |
| --- | --- |
| [d8a81d3](https://github.com/microsoft/mimalloc/commit/d8a81d3b4ff2557200fc0b78aee19513848ccd9c) | Adapt `page_full_retain` when cross-thread frees revive full pages. Keeps pages warm in producer/consumer workloads instead of abandoning and reclaiming them. |
| [41f42d5](https://github.com/microsoft/mimalloc/commit/41f42d5db3a170428bfda1d6271c564288f283a6) | Add an ownership assert to `_mi_page_free_collect_partly`. |
| [902849a](https://github.com/microsoft/mimalloc/commit/902849a6ea4e041e712d34c20c087d845727e22b) | Refine the cross-thread collection score: saturating increment and a score typed as `size_t`. |

These three commits are the complete upstream implementation of the adaptive `page_full_retain`
feature, and are byte for byte identical to the same feature in upstream `v3.5.4`.

The rest of `v3.5.4` is deliberately not taken. Upstream tagged `v3.5.4` as an *interim* tag, has
not published it as a release, and has been fixing bugs in it since (the new pprof profiler, heap
sampling, and the `mi_free_block_mt` compare and swap). The remaining changes in that tag are
allocator codegen work, a C++ new handler rework, and the new profiler, none of which are relevant
to this library. The next full sync should be to a properly published upstream release.

## Building

See `build.readme.txt` for build instructions.

## License

The vnlib wrapper in this module is licensed to me under the GNU LGPL version 2.1 (or any later
version), see the `LICENSE` file. The vendored mimalloc source is licensed by Microsoft under the
MIT license, see the `vendor/LICENSE` file, and is redistributed here under those same terms.
