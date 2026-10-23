# VNLib Mimalloc Heap

## Introduction

`vnlib_mimalloc` wraps [Microsoft's mimalloc](https://github.com/microsoft/mimalloc) allocator and
exposes it through the vnlib `NativeHeapApi`, so it can be used interchangeably with the other native
heap providers. Both a static and a shared library are produced.

Mimalloc is a general-purpose allocator with excellent performance characteristics, a small and
well-tested core, and support for cross-thread private heaps. It is MIT licensed by Microsoft, see
the `vendor/LICENSE` file.

## Vendored source

The upstream source lives in the `vendor` directory and is pinned to a specific upstream commit,
which is recorded in the `vendor/COMMIT` file. This source may contain patched or cherry-picked
commits from upstream. Ideally the latest stable release is used, unless patches exist for
performance, security, or bug fixes. This vendor image focuses _only_ on the C source code and
requires C build tooling, and a C++ toolchain on Windows.

| | |
| --- | --- |
| Upstream | [microsoft/mimalloc](https://github.com/microsoft/mimalloc) |
| Upstream release | 3.5.3 |

## License

The vnlib wrapper in this module is licensed to me under the GNU LGPL version 2.1 (or any later
version), see the `LICENSE` file. The vendored mimalloc source is licensed by Microsoft under the
MIT license, see the `vendor/LICENSE` file, and is redistributed here under those same terms.
