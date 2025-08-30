
# VNLib.Core

<p align="left">
  <a href="https://github.com/VnUgE/vnlib.core">
    <img src="https://img.shields.io/github/repo-size/vnuge/vnlib.core" alt="repo-size" />
  </a>
  <a href="https://github.com/VnUgE/vnlib.core/tags">
    <img src="https://img.shields.io/github/v/tag/vnuge/vnlib.core?include_prereleases&label=latest%20release" alt="Latest release"/>
  </a>
  <a href="https://www.vaughnnugent.com/Resources/Software/Modules/VNLib.Core-issues">
    <img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwww.vaughnnugent.com%2Fapi%2Fgit%2Fissues%3Fmodule%3DVNLib.Core&query=%24%5B'result'%5D.length&label=all%20issues" alt="Issues"/>
  </a>
  <a href="https://www.vaughnnugent.com/Resources/Software/Modules/VNLib.Core">
    <img src="https://img.shields.io/website?url=https%3A%2F%2Fwww.vaughnnugent.com" alt="Website Status"/>
  </a>
</p>

**VNLib.Core** is a foundational collection of libraries for the .NET ecosystem, written primarily in C#. It provides a suite of modular, high-performance components specifically designed for building robust server-side and long-running applications. To achieve maximum performance in critical areas, some libraries are implemented in native C, providing direct access to features like custom memory allocators and low-level cryptography. Unlike monolithic frameworks, VNLib.Core exposes its functionality as distinct, loosely-coupled building blocks—giving developers granular control from the networking stack to the application framework. 

## Philosophy

VNLib was created out of a dislike for closed, monolithic frameworks that offer a single, opinionated solution. A more lightweight and modular approach was needed—for example, using a full LAMP or ASP.NET stack for a simple home automation project is impractical. While forking a large project is possible, it often requires adopting a complex workflow and update cycle, which is not always feasible. VNLib approaches this problem differently.

**Internal components are made public and abstract where possible.**

This means you can use the same building blocks that VNLib uses for your own project without adopting the entire framework. The trade-off is that internal APIs can evolve quickly, meaning VNLib cannot offer the same strict public API contract that other solutions can.

**Dependencies are treated as a liability, not a convenience.** Many modern projects suffer from a bloated and fragile dependency chain; VNLib actively works against this trend. The policy is to build components in-house whenever practical. If a third-party library is required, it is carefully vetted for its stability and minimal footprint, and is often vendored (included directly in the source) to insulate the project from upstream changes. This results in a lean, predictable, and more secure library for you to build upon.

VNLib components may be opinionated for a given purpose, but they are meant to be individually useful. For example:
- An ELF-linkable project can link against `vnlib_compress`, a self-contained, streaming compression library.
- An ELF-linkable project can use the abstract, low-level dynamic memory allocation API defined in `NativeHeapApi.h` to plug in custom allocators.
- A C# application can get safe, configurable access to unmanaged memory that can be easily configured by the host.
- A project can use the FBM implementation of a semi-binary, request-response protocol over WebSockets or other full-duplex streaming transports.

Finally, **VNLib is a namespace, not a single repository.** This `core` repository is one of many that make up the VNLib ecosystem. For development and organizational reasons, some modules exist in their own independent repositories.

## Project Information & Resources

#### Quick Links
The easiest way to access the .NET libraries is by adding the [VNLib NuGet feed](https://www.vaughnnugent.com/resources/software/modules#support-info-title) to your project.

- [Project Homepage](https://www.vaughnnugent.com/resources/software/modules/vnlib.core)
- [Issue Tracker](https://www.vaughnnugent.com/resources/software/modules/vnlib.core-issues) (GitHub issues are disabled)
- [Package Downloads](https://www.vaughnnugent.com/resources/software/modules/vnlib.core?tab=downloads)
- [Documentation and Guides](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_vnlib.core)

#### Release Cycle & Distribution
VNLib follows a Continuous Delivery model, which allows for rapid and incremental development, aiming for small weekly releases. Projects are distributed as individual packages, and official distributions include:
- Pre-built binaries for most platforms that support Ahead-of-Time (AOT) compilation.
- Component-level source code and build scripts.
- SHA256 checksums and PGP cryptographic signatures for all packages.

#### API Stability & Versioning
As a fast-moving project, VNLib is effectively in a pre-release state.
- **Public APIs are subject to change**, potentially with little warning in any given release.
- Notable and breaking changes will be recorded in the [changelog](CHANGELOG.md) and commit messages.
- Obsoleted APIs will be marked with the `[Obsolete]` attribute where possible and are expected to be removed in a future release. While advance warning will be given, a strict API stability guarantee cannot be provided at this time.

#### Runtime Stability & Cross-Platform Support
A core pillar of VNLib is runtime stability. Great care is taken to ensure that components are reliable and that functionality, once working, continues to work as expected.

VNLib is designed to be cross-platform. Components should work on any platform that supports a C compiler or a modern .NET runtime. While integration testing is not performed on all operating systems, the architecture is platform-agnostic by design.

#### Contributing
Note that GitHub and Codeberg integrations are disabled. VNLib takes its independence seriously and does not use third-party platforms for development, issue tracking, or pull requests. Information about contributing to the project can be found on the official website. While the reach of free platforms is respected, project independence is a core value.

The project is, however, very interested in seeing what is built with VNLib! If you have created a plugin or a project you would like to share, please get in touch via the contact information on the official website.

## Donations
If you find VNLib valuable and wish to support its development, please consider making a donation. Your support helps fund the ongoing work and maintenance of the ecosystem.

**Fiat:** [PayPal](https://www.paypal.com/donate/?business=VKEDFD74QAQ72&no_recurring=0&item_name=By+donating+you+are+funding+my+love+for+producing+free+software+for+my+community.+&currency_code=USD)  
**On-Chain Bitcoin:** `bc1qgj4fk6gdu8lnhd4zqzgxgcts0vlwcv3rqznxn9`  
**LNURL:** `ChipTuner@coinos.io`

