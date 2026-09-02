# vnlib_wtlfu

_A C library implementing a W-TinyLFU based LRU cache._

## Description

`vnlib_wtlfu` provides a high-performance, general-purpose LRU cache store backed by the W-TinyLFU eviction policy. It separates recently admitted items from frequently promoted items using a window cache and a main cache, with a Count-Min Sketch frequency estimator deciding which items survive promotion. The library is a low-dependency, embeddable C component suitable for servers, clients, and embedded workloads across Windows and Linux.

This library defers all memory management to the user. The WtlCtx structure is fixed on configuration and allocated a single contiguous block for minimal moving parts and good cache locality. This library is an stdlib only dependency with some minor optimizations for win32 and libc but mostly portable and will likely compile on your C99 platform. While context size is derived at runtime it can easily be statically allocated if computed correctly.

## Links

- [Home Page](https://www.vaughnnugent.com) - Website home page
- [Documentation](https://www.vaughnnugent.com/resources/software/articles?tags=docs,vnlib_wtlfu) - Docs and articles for this project
- [Builds for VNLib.Core](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core) - Per-project build artifacts, source code and precompiled binaries

## Third-Party Notices

This library does not bundle third-party code. Some snippets or influence was taken from other developers attributed in the source files. 

## License

The software in this repository is licensed under the GNU Lesser General Public License version 2.1 (or any later version). See `LICENSE` for the full text.

SPDX-License-Identifier: LGPL-2.1-or-later

## References 

- [TinyLFU](https://ar5iv.labs.arxiv.org/html/1512.00727) - Whitepaper reference for the TinyLFU admission policy
- [w-tinylfu](https://github.com/vimpunk/w-tinylfu) - Reference for window lru and implementation
- [caffeine](https://github.com/ben-manes/caffeine) - A high performance cache in Java implementation w-tinylfu 
