# vnlib_wtlfu

_A C library implementing a W-TinyLFU based LRU cache._

## Description

`vnlib_wtlfu` provides a high-performance, general-purpose LRU cache backed by the W-TinyLFU eviction policy. It separates recently admitted items from frequently promoted items using a window cache and a main cache, with a Count-Min Sketch frequency estimator deciding which items survive promotion. The library is a low-dependency, embeddable C component suitable for servers, clients, and embedded workloads across Windows and Linux.

## Links

- [Home Page](https://www.vaughnnugent.com) - Website home page
- [Documentation](https://www.vaughnnugent.com/resources/software/articles?tags=docs,vnlib_wtlfu) - Docs and articles for this project
- [Builds for VNLib.Core](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core) - Per-project build artifacts, source code and precompiled binaries

## Third-Party Notices

This library does not bundle third-party code. All source files are original work within the VNLib project.

## License

The software in this repository is licensed under the GNU Lesser General Public License version 2.1 (or any later version). See `LICENSE` for the full text.

SPDX-License-Identifier: LGPL-2.1-or-later

