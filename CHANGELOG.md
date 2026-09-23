# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Updated mimalloc to v3.3.2 and enable first class heap support - (mimalloc) [a674a84](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a674a843bbf8ea84acee28aa8060b36aa02b253d)
- Migrate IAsyncLazy into core utils and add unit tests - (utils) [f600605](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f60060539693de2a39b7ad6e042d569dddd177db)
- Add inter-plugin shared memory communication with host reserved regions - (ipc) [01205d6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=01205d68f2ed6979cef017e2da9b7ed6535f7527), [53ff998](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=53ff998db83c3af3e835177f58e6d4468f8407c4)

### Changed

- **Breaking Change:** Decouple plugin system from HTTP stack and make ServiceDomain and ServiceGroups immutable after creation - (ServiceStack) [5d96aed](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5d96aedf48bfa42edbed4456903d3472e316c5f2), [f86bd7a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f86bd7af75578ee77d10805ddf12567295edfe94), [6ff7b9c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6ff7b9c81ffaef8db8f69e68cce9ee22c8438c62)
- Cleanup webserver virtual host config processing - (webserver) [baad9c6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=baad9c6ca213dac09fe11e0787d1b2612da2dbe5)
- Improve code readability and consistency in various files - (webserver) [d875cd1](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=d875cd170db82153b57b08380090a2277dbf0c3d)
- Add gitattributes file to the repo to fix file encodings - [3c4c38a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3c4c38aa127e3b0a2ab341a2d8f67c5089e97bef)
- Update Serilog to version 4.4.0 across WebServer and PluginBase projects - [844f689](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=844f6895a62be283b1d6819aca00237b5f3729af), [bcab785](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bcab785baa55597637c99db09fb33ac866aff9fe)
- Fix typos in project descriptions across multiple .csproj files - [4d6eb46](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4d6eb46ec794a65991445c07a3869f91862c4e83)
- Update RestSharp to v114.0.0 - [312732f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=312732ffb2403c5fbc50b4d9e68bc20dbfadb3d3)
- **Breaking Change:** Correct public API spelling and naming across TCP, plugins, and essentials (`TCPConfig`→`TcpConfig`, `Privilages`→`Privileges`, `SecurityProcol`→`SecurityProtocol`, `PreProccess`→`PreProcess`, `AuthorzationCheckLevel`→`AuthorizationCheckLevel`, removed concrete `VirtualEndpoint`/`ProtectedWebEndpoint`/`UnprotectedWebEndpoint` (retained generic `IVirtualEndpoint<T>`), SimpleTCP→TCP package rename, `ConfigurationInitalizerAttribute`→`ConfigurationInitializerAttribute`) - [0d3eaa6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0d3eaa60b6a52e0fd2c61d5f24b6aa06d5c06f7d), [628ccce](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=628ccce4fa0df1bf1b605fd053f9105e0e6efc41), [97b9d33](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=97b9d3335ef69277958b971d5e9a2c5deab81bea), [e462338](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e462338267685372e7343b3ce995421cc1942419), [25f5999](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=25f59993b888d018863fa2272af3f04825678fc6), [5b46e8d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5b46e8db5f63ac7210cb1b0908abce968ce49179), [da3cfa5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=da3cfa5390c2985ee1c7a807cd61aa6cecadf5f0), [2d78642](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2d78642677e5b8bae22db36a26e53279b1353e2f)
- Enable warnings as errors for some projects - [20fac6f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=20fac6f6ab045c9abc738d1c0889792a40958437)
- Some minor performance and readability improvements - (essentials) [4413f31](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4413f310bc7f69d6d3946d95a81bdda3e42e6d45)
- Mostly minor doc fixes across the library packages + fbm helper refactor - [fb557fd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=fb557fd3e7f2ada6bd71335551f37bc2c9134f3f)
- Update System.IO.Pipelines from 10.0.3 to 10.0.12 - [136c39c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=136c39c7914bd543b842724cf8cad92676b12468), [bb8be6c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bb8be6c87c4181d0ea10146478f0cfc9afa22b4e), [bc36a04](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bc36a046ea24b0fc0b4d6092fa5a16378de30e55), [3fe163e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3fe163e728fa5c998549bb047bb3aacb2dd78cd8)
- Update linux ci dependencies - [98322a4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=98322a409b00d677c5dd866c105464ea0ca76a6c)
- Update YamlDotNet from v16.3.0 to v18.1.0 - [14171b9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=14171b9f9571cc2d08d654443ab825ff103ec0b0)
- Update sample config.json documentation - (webserver) [127eeea](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=127eeea4f7c341cbe675f2e64865bfc1e024be7d)
- Remove errorprone.net analyzers that cause build failures for SDK 8.0 - [ec8561d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ec8561d74388fe8e0ee706e3876aa256948709f4)
- **Breaking Change:** Plugin system overhaul - event-driven architecture, plugin stack rework (`PluginStackBuilder`/`PluginStackInitializer` removed, `IPluginAssemblyLoader`/`IPluginAssemblyWatcher`/`IPluginDiscoveryManager` removed, `IRuntimeServiceInjection`/`IHttpPluginManager`/`IManagedPlugin`/`IManualPlugin`/`PluginRutimeEventHandler` removed, `ProcessHostCommand()` no longer required, new `Batteries/`/`Construction/`/`Events/`/`Watcher/`/`Services/` layout), tests, and fixes - (plugins-runtime) [f1f41e4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f1f41e4d65c123c8a10989a5b8efd0307a4a4157)
- **Breaking Change:** Webserver plugin loading rework (`PluginAssemblyLoader`→`PluginAssemblyResolver`+`PluginConfigLoader`, `MainServerMiddlware`→`MainServerMiddleware`), plugin config immutability/path resolution, new `ConfigurationKeyAttribute` and `ipc_shared_memory`/`reserved_regions` config - (webserver) [3b4ffa7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3b4ffa74e8ea404f0a93697f86d4f7d8ac467ae0)
- Update zstd to ~v1.6.0 - [b316e43](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b316e43ceb0114481f17a11bee18a3d58e0241da)
- Update monocypher from v4.0.2 to v4.0.3 (fixes EdDSA timing-leak vulnerability, compiler warning fixes) - (crypto) [3dc87c0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3dc87c0adc258c9b1e7f65e997d9e0b5c1964cb9)
- Improve monocypher cmake build flags (stale comment fix, `-march=native` for source-only linux builds, `-Wconversion` in debug builds, remove erroneous `DEBUG` define in release builds) - (crypto) [546fbba](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=546fbbaa362a69cc83a891b3aae149249b652fa6)
- Update cmake from 4.3 to 4.4 - [3fe163e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3fe163e728fa5c998549bb047bb3aacb2dd78cd8)
- Normalize primary README file casing and package references - [3e20144](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3e20144ebbb91bd2cca261452513f23eeb21d6a6), [19a612b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=19a612b265a1d0916c8e121aa2cda2c558f91c03)
- Add verbosity and no-incremental flags to the default build task - [dccb84b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=dccb84be19a511a7832fc8b907e2212120464376)
- Cleanup and improve argon2 safelibrary apis - (hashing) [2ba27fa](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2ba27fa279026677ac703fbfb2fe9108f854a20f)
- **Breaking Change:** Refactor the monocypher managed library handle - (crypto) [8404698](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=840469889be0cbf683ecd5c31666e9b84092fd5d)
- Modernize build packaging with msbuild native projects and an updated default taskfile - [6467170](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6467170b1cc8997b56f36ffb18f4b3f46f47811c), [4e5da71](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4e5da711b9f22ea5d9a961a5e1304fa77f20033f), [f4caa3a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f4caa3aeda4cbcd1ecb4db9b57fa162d955195cc)
- Require task 3.45 and add build variable checks - [2790b17](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2790b172aca2868b18d73c289dff7306024c751a)
- Update test dependencies to latest versions (MSTest v4.4.1, Microsoft.NET.Test.Sdk v18.9, coverlet.collector v10.0.1) - [b0ab97c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b0ab97ce4e41b2c74b98f1a582cc97f0950b2f1b), [b458141](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b458141bd5924df8c042d1d5741a341a0b69d9bb), [f3a7e2d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f3a7e2d5d2f5a91461e5f47eaa67090b6c0600e4), [a512560](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a512560cc737ea437a98e446dbcc1f409d76599d), [81cf34b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=81cf34be36e1dddacfc6fa593fdc9513015a688f), [3fe163e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3fe163e728fa5c998549bb047bb3aacb2dd78cd8)
- **Breaking Change:** Make sessions and auth abstractions agnostic, removes many session apis - (essentials) [57023bd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=57023bd0778aa615731b049549296a3a7a85ab65)
- **Breaking Change:** Correct `MemoryUtil` environment consistent spelling (`SHARED_HEAP_ENABLE_DIAGNOISTICS_ENV`→`SHARED_HEAP_ENABLE_DIAGNOSTICS_ENV`) - (utils) [4ae9267](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4ae9267675139105c119da43ddcb3a38ae5fc676)
- **Breaking Change:** Tighten virtual host config validation (hostname whitespace rejection, `deny_extensions` format validation) with unit tests - (webserver) [e176c58](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e176c58c93b6347cdf6510270e835a8873e13674), [0ab8c75](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0ab8c75b083860300ea318ed367a370ccf0d3b8f)
- Warn when SO_REUSEPORT is enabled since port reuse is not currently supported - (webserver) [6067295](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6067295709a7b41726751efecad0792df90dd5aa)
- Relax `default_files` validation to allow extensionless names, leading-dot files, and digit extensions - (webserver) [c6ed196](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=c6ed19620a6c9a91c68bc7a6407838de4bbade91)
- Map zstd native errors -18/-19 (`ERR_ZSTD_INVALID_STATE`, `ERR_ZSTD_COMPRESSION_FAILED`) to managed exceptions - (compression) [b77e867](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b77e867cc34215ed99d23c7b7f517e4fac9d3dca)
- Restore `IEndpoint` path validations, unrooted paths (missing leading `/`) are rejected at registration - (essentials) [d5d2772](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=d5d277218a9919d0e0870e5d99f9a84f9867d52f)
- Remove documented-but-never-implemented `[system]` hostname expansion from the sample config - (webserver) [aae2942](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=aae2942d435830e8f320a122d954aef02b92c383)
- Validate `path_filter` regex syntax during config loading instead of failing at host build time - (webserver) [8746f19](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=8746f19a60f4abf008693cce1667da6634435f5f)
- Harden namespaced (`::`) config lookup, missing sections still return default but traversing through non-object values now throws instead of mis-deserializing - (webserver) [a35d37f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a35d37f6c522e820f7c82506af3df61bbdaa29d0)
- Include `.vcxitems` in mimalloc/rpmalloc `src.tgz` source distributions - (memory) [f00fe79](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f00fe79380d79571f481f26fd88b87cd2fb0e658)

### Fixed

- Add Virtual Host configuration validation unit tests - (webserver) [8db66b3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=8db66b3465a5cc06a42cf40380204b2a65104065)
- Hostname and default file path validation in virtual host config - [45b8f7f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=45b8f7f4851bb852218390a4ddc04f0e790d45aa)
- **Breaking Change:** Updated `ExecuteAsync` extension method type constraints for response type - (rest) [7a6c60f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7a6c60f52f2f06fd361f5a60d0d0ccb51f25fe63)
- **Breaking Change:** Correct public API spelling in plugins and essentials (`EndpointPortsMatch`, `ConfigurationInitalizerAttribute`→`ConfigurationInitializerAttribute`, `PluginUnloadExcpetion` (removed), `DissallowedAttributes`→`DisallowedAttributes`, `ISlindingWindowBuffer`→`ISlidingWindowBuffer`) - [0dc27eb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0dc27ebef7b508d8f47f4b1b3339d36bd14fb6f6), [aa6c9ad](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=aa6c9ad0a42d1ba34a83b3f2aa480fab3146751e), [e40e16b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e40e16b235c86a18a15e2e8d15e4c13c2d51700d), [9ce80cb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9ce80cb79919f44b929b7c20fcc59527c3c986c1), [824847b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=824847bd5c7d6af953878462ea02f184dad38d4c)
- Fixed a bug in internal utility function in `MemoryUtil.cs` class - (utils) [65a9381](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=65a938125a16220d91fa9f75ee558d60a7f9b911)
- Replace the Name and ValidFor properties for the SingleCookieController class - (essentials) [4286ec7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4286ec712058d1fe817ff948b4b3a10dfd5da501)
- Add unit test coverage for shared memory reserved regions - (ipc) [b0f5076](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b0f5076b8e5aea100b96af1c6a5b09a75a3e4312), [ba779dc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ba779dc4a0846296039905c659a16cb86a9c389f)
- Guard plugin console command dispatch so a throwing plugin handler is logged instead of breaking the host command loop - (plugins-runtime) [833cdcd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=833cdcdfbb100ba8e1658631a8e17707c004cf98)
- Fix native library handle leak when wrapping a custom argon2/monocypher library fails - (hashing) [0705144](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=070514472aacaaacfdfec92757f071d781c5fa0f)
- Map `COMP_LEVEL_NO_COMPRESSION` to `ZSTD_minCLevel()` instead of level 1 for true no-compression parity with gzip - (compression) [06bb2bc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=06bb2bca83fce3481170ee72c72d0e0d3f32c391)
- Correct upload/entity-size validation direction, uploads now require a max entity size instead of the reverse - (http) [6764a7f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6764a7f2ca0f57ace0872e1bc5f9316692c6c42a)
- Correct sample.config.json `allowed_origins` to `allowed_authority`. Bug that silently ignored values in the sample config - (webserver) [7f07e3f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7f07e3fb278717105d1d6a4e7a9ed57d3a6cbc9a)

### Performance

- Use .ConfigureAwait(false) for all await callbacks - (tcp) [7cd65d0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7cd65d02e000bf870660ec4129f5f5d455b2423a)

### Removed

- Remove unused/deprecated vn-encoding utilities - [09a11b7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=09a11b7eaef35f468083943c1db373702d932b8c)
- **Breaking Change:** Remove required abstract ProcessHostCommand() from PluginBase - (plugins) [3555d96](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3555d96a338ed8d78367d85eedc8d08d42a39fd1)  
- **Breaking Change:** Remove outdated OAuth2 helper api - (rest) [b8e7273](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b8e727306dc51ad7aca219300b861db8be329e20)  
- Remove obsolete apis - [6e46df3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6e46df36455124a2d6e9dfe9586e6a8a2644d5e7)
- **Breaking Change:** Make sessions and auth abstractions agnostic, removed `Accounts/` (`AccountUtils`, `Argon2HashProvider`, `IAccountSecurityProvider`, `IPasswordHashingProvider`, `ISecretProvider`, `FailedLoginLockout`, `LoginMessage`, etc), `Users/` (`IUser`, `IUserManager`, `UserEncodedData`, etc), `Oauth/` helpers, `Endpoints/ProtectedWebEndpoint`/`UnprotectedWebEndpoint`/concrete `VirtualEndpoint`, `Extensions/HttpCookie`/`UserExtensions`, `Sessions/ISessionExtensions`/`SessionCacheLimitException` - (essentials) [57023bd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=57023bd0778aa615731b049549296a3a7a85ab65)

## [0.1.5] - 2026-01-30

### Added

- Enable platform arch SIMD enhancements by default on amd64 systems - (mimalloc) [63e16cd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=63e16cd20fcec19b08d6ed8ab285dc19f8f93f0d)

### Changed

- Update brotli to `5fa73e2` just ahead of version 1.2.0 - (deps) [05e6b76](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=05e6b760cc6976ec3863453e508745dc1c1f8e04)
- Update mimalloc to v3.0.11 - (deps) [9e067e0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9e067e093d3a9f94e768c0243e4ab761387c0bde)
- Remove `ENABLE_GREEDY` heap build option and update license handling - (mimalloc) [a7dade1](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a7dade14d430c9340dc93efdc3858726b8725c80)
- Update Zstd - Cherrypick `c59812e` for memory leak bugfixes - (deps) [387101a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=387101a48c5c65784a6e2c9409a847810ada34b4)
- Update linux test dependencies to latest versions - (deps) [d301082](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=d301082a1035c65f0d1c94f90c5a009790c9659d)
- Update mimalloc from 3.0.11 to 3.0.12 - (deps) [3d37691](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3d37691a0d6b1f9f857d047f77aa6eefa3d0c5d2)
- Update cloudflare/zlib to 1252e25 - (deps) [676269d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=676269d2a37ddbb74929394a389f72e857472d96)
- Update RestSharp to v113.1.0 - (deps) [40ce007](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=40ce007ffbbb9515b82b9306c81d7e930e09b330)
- Update System.IO.Pipelines to v10.0.2 - (deps) [bd237ba](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bd237ba53a5a9670880b127b8e5468d3b8f95496)
- Remove RunAnalyzersDuringBuild property from project files - [f82130a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f82130ad52f931e489ca702b53ce74d4ef691ba6)

### Fixed

- Set the shared heap creation flag when calling `heapCreate` - (minalloc) [2bd2cfb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2bd2cfb6c6c3ff147b912eb1d7906521d9e3fc22)
- Corrected rpmalloc and mimalloc NativeHeapApi to match correct header api types - (utils.memory) [9863d63](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9863d63003fe57dfc978a876ea79d180c84008e7)
- Implement size guards for < 64bit systems - (memory) [777f543](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=777f543cd066c53ef86e8d1b4fc8162e33ee6dc6)
- Refactor and modernize unit test assertions and namespaces - [cf05883](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=cf05883f6204c6a1f47c81262ebb88dcf4140751)
- Fix value disacard in Subsequence unit tests during assertions - [f738ced](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f738ced219d4f3679a1fad06098924a4cb9d18b3)
- Suppress test warnings, improve async/threading/test code - [54af35f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=54af35f6418afd4926f37ed1749b45ae1e382765)

### Performance

- Improve waithandle WaitAsync and NoSpinWaitAsync extension methods - (utils) [ed59eb7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ed59eb7393b3ed5876206e9a45ca94841542bd44)

## [0.1.4] - 2025-11-17

### Changed

- Fix many code documentation typos - [c0a4887](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=c0a48874c798fa0327b69a6729b79546be640758)
- Update Serilog.Sinks.Console to version 6.1.1 - (deps) [9ce9dbe](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9ce9dbe8ff23e56b793efd453c4bd6d9d4a57154)
- Update System.IO.Pipelines to version 9.0.11 - (deps) [55a93fc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=55a93fcd5ed00e3f741bbdf0d9b23a095b9cd52f)

### Fixed

- Remove `vntable` oom test as it's out of scope and non-determinisitc - (utils) [6109a9c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6109a9cd580e88d4bbdef01b5e45d7429226055b)
- Added basic unit testing for some internal webserver components - (webserver) [a86d7d0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a86d7d07b431d96ca77a4a650c423046a93a2e8c)
- Implement server plugin config deserialzing tests. - (webserver) [5211d09](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5211d098867e8d2853b8bd3f428467e422d797df)
- Fixed a typo `ReleaseMutext()` -> `ReleaseMutex()` in structure `MutextReleaser` which obsoletes typo. - (utils) [323b1e6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=323b1e616033876c6afd03d75f43c4e15dd7436c)

## [0.1.3] - 2025-10-01

### Changed

- Added more cross platform Task v3.45 syntax support to build scripts - [3cd06db](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3cd06dbb77529fd594192fba91bd3814d595eaaf)
- Cleanup and add more Task 3.45 cross platform build support - (webserver) [8689c50](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=8689c50c0187591d7904ac8e8ab7244bc4be1752)
- Corrected some outdated documentation in the `MemoryUtil` public api. - (utils) [2913f28](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2913f281579207ddab5c2b909d77b9737659eb06)

### Fixed

- Changed the exception type raised when `SysBufferMemoryManager` checks if an integer overflow would occur to an `ArgumentOutOfRangeException` - (utils) [cd80575](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=cd80575d012941daa00936909361fa4f5fa6136f)
- Catch zstd stream reuse after finish and return error - (compression) [4814175](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=481417512d7466b9d3882decefd3c519f9d69922)
- Fixed a bug that causes inconsistent errors when allocating 0 length blocks. closes #42 - (utils) [598a854](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=598a8544306ef8abcdd6df19df699adeab85c631)

## [0.1.2] - 2025-09-20

### Changed

- Update System.IO.Pipelines to version 9.0.9 - (deps) [de6bb2f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=de6bb2f44281d74a287b82b67b1b4c08ba7f9aff)
- Correct various spelling and grammar mistakes across public and internal code documentation. - [65abcc0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=65abcc05c3eae24ab30a0a0ab3d3dded76cc28cf)

### Fixed

- **Breaking Change:** Correct the spelling of IUnmanagedHeap public interface - [7662571](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7662571c4c584ac5b95097954e27b11f111fb663)

## [0.1.2-rc.10] - 2025-09-08

### Added

- Added public function `NativeHeap.CreateHeap` that accepts existing library handles - [c31478c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=c31478cb2fe85c6034d9f47d958ef4552d100f0a)

### Changed

- Update brotli to `98a89b1` - (deps) [4b21138](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4b21138978586fbce46427ec65a2f09c7d2526ba)
- Update zstd to `98d2b90` - (deps) [5007793](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5007793c2f417038661c8837e48ae57d8346e9d2)

### Fixed

- Fix custom response headers not getting sent by the server - (webserver) [1fcf6ed](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=1fcf6ed12098de524d7e17b782252cd5dd634170)

## [0.1.2-rc.9] - 2025-08-26

### Added

- Adds public api function `CanAccess()` for Linunx platforms - (utils) [ba66248](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ba662482d70cd9472bf70a18d6c2520dad1d253d)

### Changed

- Update `System.IO.Pipelines` to v9.0.8 for SimpleTCP - (deps) [9c3db83](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9c3db83d8f3193baaf445dac9c5fccdcb0af5731)
- Centralize MSBuild config via Directory.Build.props; drop MS_ARGS - [d28635e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=d28635e3c35b3e8ea3cde845d77012dd055f4466)
- Add warnings as errors for use of obsolete APIs for entire module - (props) [afbf2ce](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=afbf2ce9d8b6794df77c05c2c6a55d206e07eae0)
- Enable IDE0251 analyzer warning as an error for readonly members. - [9b66d46](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9b66d466fd5fc5a6edaef5babe353d75f3b6ab1b)
- Clean up readability in `GetAttributes()` function. - (utils) [6f31028](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6f31028dee21e1345d9bab6c3caa5ac26c1c4162)
- Code cleanup of random formattng and code style updates, bulk commit - (lib) [5bb2eec](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5bb2eec7d68381ccd13d710d6ce1390648a813be)
- Improve documentation for `INativeCompressor` public interface - (compression) [e723539](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e723539c897acb64c3c1beacd4adaeef2d9f1d13)

### Fixed

- Disable incremental builds during module testing - [628ee5c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=628ee5c266dedc7d3275d2ea060c7d051d05cf11)
- Improve native heap unmanaged API testing for all built-in heap implementations - (util) [0abf2ea](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0abf2ea7154be36f068aa5e0e2abc0106a49cae2)
- Add unit tests for the `FileOperations.cs` public api - (utils/io) [875b6f0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=875b6f07857b6d8e36aaa8d99962d0e3c20f15d7)
- Fix utils tests for linux containers running as root - (util/io) [11c65ab](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=11c65ab8dd6150d8628e90e8094b35f666547f02)
- Disable recent tests for known denied `access()` on linux - (utils/io) [87c5dc0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=87c5dc05219a4ac481796cf91a04ae0fdbce90fe)
- Update MSTest dependencies - [671093e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=671093ecb168c06816c9c4e3bec14a4ed9678b19)

### Performance

- Improve performance for `FileOperations.FileExists()` for Linux platforms using the `access()` libc api - (utils) [923288f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=923288f23d12ebe4759bed206d08e01520cff7cd)

## [0.1.2-rc.8] - 2025-08-13

### Added

- Added Merge() extension method for JsonElement structures - (utils) [7213e5f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7213e5fc5f00bdfd909022c0d3ae196f8d7d9513)
- Add explicit and mulit-level config for TCP_NODELAY and close #41 - (webserver) [7cb65d3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7cb65d31c52aeef8d30f8ea169427501c1707fa5)
- Add option `reuse_address` to global tcp configuration - (server) [ffd500f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ffd500f778d7510c01bfb90823e6634f56bd963f)

### Changed

- Deprecate some json extension methods in utils - (utils) [7d5a8bd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7d5a8bdccfa2af0ca745a52749b038192842a35c)
- GetPropString ensures the json kind is a string before getting the value - (utils) [ef67909](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ef67909b302e39335a2a3e16ca0b86fb827ab35f)
- Rename config header_buf_size to request_header_buf_size - (webserver) [57f90a8](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=57f90a81febbdeee30cd5f68ba18141a19beecd8)
- Update mimalloc to 09a2709 - (deps) [9d08f85](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9d08f85f6103819f13feaaf93261e2dc1819e1ad)
- Update mimalloc build defaults and cmake forced cache - (utils) [0e6e5cc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0e6e5ccbb5048e8ae7081c53acfda5a01bbcc1ef)
- Expose the MI_SECURE build option - (mimalloc) [ab7ccf0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ab7ccf0de1c9179f5aa6a27757c83db72e1a6b3e)
- Improve tcp configuration logging and warnings - (server) [7142963](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=71429636ffc48076846a8273e0f02ec9d16f9886)

## [0.1.2-rc.7] - 2025-07-23

### Added

- Add zstd compression support - [ffd607e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ffd607ea04009e5d3a0d906317f547db4a4cff38)

### Changed

- Correct zstd linking and build private copy of rpmalloc for compression - [80bd3aa](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=80bd3aa60291c7d79d4815e20c0fa93f12d5f2bb)
- Runtime compressor feedback and config - (compression) [3b67873](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3b67873deae8978644a27edf3d0f5f231f7f40a7)
- Coed cleanup in http request/response classes - [905f159](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=905f1598d8ca813f750e7751e511502f95447479)
- Add extra gcc build flags for debug builds - (compression) [642673c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=642673cf911ffea05af0228ebb16f139325c2c51)
- Dev-init task is now fully parallelized - [afd62d9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=afd62d9618a083405077dd30449197de33341173)
- For dev builds just use the debuild build command instead of the CI build - (compression) [2ca7f1b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2ca7f1b0b236b69eb410a9f6cd4d02b84adba305)
- Update all C library taskfiles - (libs) [fc7497b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=fc7497b318a4110f95b71bf24b5514cc3637009e)
- Improve cmake configuration and error handling - (compression) [e26209e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e26209e3b38911dd1207c3ca24af2aec1fbdb52b)
- Minor code quality and consistency touchups - (lib) [6b052cf](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6b052cf847a8d74de52a8fb2224280164a3b6a6e)
- Standardize Taskfile structure and variable declarations - (build) [39fb2ab](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=39fb2abdceca42b00d1480aeb9c262a1e7588282)
- Unify c taskfiles and build scripts - [1fb475e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=1fb475e3f2ba25b67fba5e8c32abae4483a02854)
- Update System.IO.Pipelines to 9.0.7 - (deps) [750c263](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=750c2635a79c4859ef1453c88ae2743121b1b5fa)

### Fixed

- Extended the unit tests to cover basic zstd compressor checks - [f2da3b6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f2da3b638aa433e3bbfaa59f6442ffefd8573395)
- Improve null safety in zstd compressor - (compression) [4daf96b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4daf96b79fb4aa01c172149551f6495e5a5b1697)

### Removed

- Remove unused CompressorStatus enumeration - [e74a71a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e74a71ac04ea4193db1883b63d774c0f75558aca)
- Remove dangling ignore file - (compression) [a7f8f87](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a7f8f87700abc65b4cc130660c27cc31129279f2)
- Remove unecessary argon2 source code from tree - (argon2) [3166625](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=31666259fcc132e290727186532174b329824d4d)
- Removed more unecessary scripts and test files - (brotli) [b29629e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=b29629ed14911fd9685f2759958f518a833ed95c)
- Remove commit limit for cliff commit changelog - [f99a8ff](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f99a8ffbb58ee2240649367000e94f8f1f5939fc)

## [0.1.2-rc.6] - 2025-07-04

### Added

- Vendor minimal working copy of zstd c library - (libs) [e2607c0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e2607c00d9792861d03bf245649fad925a56294e)
- Feature C files for zstd have been started but not working - (libs) [13d059d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=13d059db5ce66cec481e080f6515cb832d7607eb)

### Changed

- Major changes to the build taskfile to account for breaking build changes - (libs) [3bdef10](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3bdef104cad3c92bb31196d37df5f84a35e7c634)
- Unify malloc and free callbacks for all compressors - [48b4f5e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=48b4f5e447c7d47e917c1e95067375eb9e426080)
- Update brotli to f2022b9 and close #31 - (deps) [a317db5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a317db5e8ebf453e4d57d22d16ff99e915ba6e9a)

### Fixed

- Correct the breaking changes section of the changelog template - (changelog) [404b3d5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=404b3d5fcbb3b540dbbc82f467132f248000dbd0)

### Removed

- Remove util.h references after memory cleanup - [f228ed4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f228ed481e4775bca08b113357aefdafa2c3550b)

## [0.1.2-rc.5] - 2025-06-23

### Added

- Implement async methods for fbm stream - (lib) [285fd30](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=285fd30f9fd268addecb872a2e2013ee975966f9)

### Changed

- Replace FBMBuffer with FBMReusableRequestStream - (FBM) [a209aeb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a209aebe82a3ca82417df5438688b368cdafdaa1)
- Improve code structure and readability - (libs) [76a3d95](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=76a3d95baf7a061bd5b485216c21821562e22fff)
- Rename DirectAlloc to AllocMemory - (lib) [3e3edd5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3e3edd5cf7fa33bf84d435daa8cc1a7c2aea5381)
- Make srcRef in CopyAndPublishDataOnSendPipe readonly - (lib) [3a2fb3a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3a2fb3a372f3ab42b1974a5410cc440ac305da35)
- Update AsSpan to use ref readonly - (lib) [bdb8115](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bdb81154d0a1369d95e872ff1bf6cf93187dfe34)
- Remove obsolete method DirectAlloc to AllocMemory - (lib) [280ece8](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=280ece8789d02ee9dc3fd68694e0cdf800c06195)
- Remove obsolete call to DirectAlloc in favor of AllocMemory - (lib) [5f25c30](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=5f25c30c5aa74066cbfbfb6e245a7b9307dc1a64)

### Fixed

- Add tests and configuration for FBMRequest - (FBMRequest) [c5ddbb7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=c5ddbb7779c8d623fdb78132f1135d8407e1621c)
- Add SetLengthTest for stream length handling - (lib) [729ba59](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=729ba590c57d6deac0f57efdf267fe7954aec56d)

### Refactor

- **Breaking Change:** Remove FBM client extensions class & rename FBM buffer - (tools) [7ed7f11](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7ed7f11a9ca9307636a87c36ee449730cd25498d)

## [0.1.2-rc.4] - 2025-06-13

### Refactor

- **Breaking Change:** Add changelog readability, and simple filesystem changes - [09dcaba](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=09dcabab5c98ce24f8fd283150e395c94803d529)

## [0.1.1] - 2025-05-15

### Added

- Add FNV1a software checksum and basic correction tests - [6d8c344](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6d8c3444e09561e5957491b3cc1ae858e0abdd14)
- Added ReadFileDataAsync function - [3b7004b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3b7004b88acfc7f7baa3a8857a5a2f7cf3dd560e)
- Add a default site adapater and interceptors - [75c1d0c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=75c1d0cbf9a5a7856c544671a45f1b4312ffe7ce)
- Allow multiple plugin loading directories - [ff0926b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ff0926be56fc6eafdce36411847d73bf4ce9f183)
- Add file path caching support - [ee3620b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ee3620b8168a42c8e571e853c751ad5999a9b907)
- Multi transport listeners - [92e182c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=92e182ceaf843f8d859d38faa8b2c0ff53207ff6)
- Server arch update, Memory struct access - (server) [12391e9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=12391e9a207b60b41a074600fc2373ad3eb1c3ab)
- Scoped spans for forward writer & tests to sln - [8da9685](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=8da9685d9bf3fcd73a775cb7306e4e188cfa214b)
- Enable custom http file cache headers from config - (server) [0fadf0d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0fadf0d24cfcf26f3f4134da79ca7e9901238410)
- Compression library env variable support - (shared) [bb81959](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=bb819597d410e7592aa9489b9e9df0f35f736cea)
- Read configuration from stdin - (server) [e2f4122](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=e2f41222d9a106c64c3c1cb00cc1198286e29e1d)

### Changed

- JWK overhaul & add length getter to FileUpload - [9c7b564](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9c7b564911080ccd5cbbb9851a0757b05e1e9047)
- Update compression header files and macros + Ci build - [ebf688f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ebf688f2f974295beabf7b5def7e6f6f150551d0)
- Overhauled native library loading and lazy init - [6c1667b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6c1667be23597513537f8190e2f55d65eb9b7c7a)
- Updates, advanced tracing, http optimizations - [3ff90da](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=3ff90da4f02af47ea6d233fdd4445337ebe36452)
- Overhaul C libraries and fix builds - [8c4a45e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=8c4a45e384accf92b1b6d748530e8d46f7de40d6)
- Harden some argon2 password hashing - [9a96479](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9a964795757bf0da4dd7fcab15ad304f4ea3fdf1)
- Minor non-breaking changes to VNEncoding - [51cb4eb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=51cb4eb93e4f1b4c47d35b105e72af1fe771abcc)
- Immutable tcp listeners - [2160510](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2160510fcc22a8574b0090fd91ca29072f45ab59)
- Update service stack to reflect new loading patterns - [6b8c678](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=6b8c67888731f7dd210acdb2b1160cdbdbe30d47)
- Refactor extensions with perf updates - [981ba28](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=981ba286e4793de95bf65e6588313411344c4d53)
- Swallow vnlib.webserver into core & build updates - (app) [904560a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=904560a7b5eafd7580fb0a03e778d1751e72a503)
- #7 Update compression style, platform and linking - [d297b3a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=d297b3a958e13a76ea61c8df588ec32ea9a40faf)
- Testing updates and easier dev-testing - [322bbe0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=322bbe00f77772ba6b0e25759de95dd517b6014c)
- Minor code clarity change - [184465f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=184465f33944a5bc0c7aaec15969e2f28f1fbf4e)
- Switch to -rc version sufffix for master builds - [4d1523e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4d1523e6b8e8eaebce5e64ef8bd0002532395675)

### Fixed

- Fix _In_ macro for compression public api - [9b40363](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9b4036377c52200c6488c98180d69a0e63321f97)
- Missed ! on null pointer check - [4ca5791](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4ca5791ed67b9834bdbd010206b30373e4705e9b)
- **Breaking Change:** Middlware array, multiple cookie set, and cookie check - [42ff770](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=42ff77080d10b0fc9fecbbc46141e8e23a1d066a)
- Improper request buffer property assignment - [ff15c05](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=ff15c05a9c3e632c39f3889820fb7d889342b452)
- Zero/unsafezero with data types > sizeof(byte) - [2ae018a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=2ae018af277b808786cf398c689910bc016e7ef0)
- Memory leak: missing fbm loop buffer free - [4fafa9e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4fafa9e4d32e15dbd30ed5082bcd999fd5b536da)
- #10 Raise exception when max entity size >0 , but uploads == 0 - [240348e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=240348e338353e5933e4d67670151ab83283ff8c)
- Add bsic sample config testing to webserver - (app) [a8f618a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=a8f618a3088d4d19d6e2fcd3759faf578f6d2fd1)
- Cleanup and correct some testing unit functions - [f9c9eae](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f9c9eae829175e0954aebb759515dfc4ee60227b)
- Add UnsafeMemoryHandle tests - [0b41bf7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=0b41bf74c98fbdd7ca3d6f3a7b3625171be650df)
- 27 remove unecessary platform attributes on test functions - [32fe5ee](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=32fe5eeaec3e425e933610fa78b0d6a98f4c73b0)
- Remove library address quotes from trace output - [fbee5bc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=fbee5bc5ff442e8a2f4103ff5df63edea68da7fc)
- Process heap should correctly handle flags & global zero - [cbe9ee0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=cbe9ee0055b457e1cfebd5bc3cab0af630f7763a)

### Performance

- Deprecate unsafememoryhandle span extensions - [9afed14](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=9afed1427472da1ea13079f98dbe27339e55ee7d)
- Utils + http perf mods - [4035c83](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=4035c838c1508af0aa7e767a97431a692958ce1c)
- Async pre-buffer to avoid sync buffer - [7d2987f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=7d2987f1d4048c30808a85798e32c99747f6cfe3)
- Absolutely yuge perf boosts - [07ddf67](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=07ddf6738d32127926d07b1366e56d2a2308b53b)

### Removed

- Remove argon2 docs & optional tcp resuse - [f836e09](https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/commit/?id=f836e09981866f5c9f2ae46990d11b186a7d12bb)

[0.1.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.5&id2=v0.1.4
[0.1.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.4&id2=v0.1.3
[0.1.3]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.3&id2=v0.1.2
[0.1.2]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2&id2=v0.1.2-rc.10
[0.1.2-rc.10]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.10&id2=v0.1.2-rc.9
[0.1.2-rc.9]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.9&id2=v0.1.2-rc.8
[0.1.2-rc.8]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.8&id2=v0.1.2-rc.7
[0.1.2-rc.7]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.7&id2=v0.1.2-rc.6
[0.1.2-rc.6]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.6&id2=v0.1.2-rc.5
[0.1.2-rc.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.5&id2=v0.1.2-rc.4
[0.1.2-rc.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.4&id2=v0.1.1
[0.1.1]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.1&id2=v0.1.2
[unreleased]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=HEAD&id2=v0.1.5

<!-- generated by git-cliff -->
