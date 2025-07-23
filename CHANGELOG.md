# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2-rc.7] - 2025-07-23

### Added

- Add zstd compression support 

### Changed

- Correct zstd linking and build private copy of rpmalloc for compression 
- Runtime compressor feedback and config - (compression)
- Coed cleanup in http request/response classes 
- Add extra gcc build flags for debug builds - (compression)
- Dev-init task is now fully parallelized 
- For dev builds just use the debuild build command instead of the CI build - (compression)
- Update all C library taskfiles - (libs)
- Improve cmake configuration and error handling - (compression)
- Minor code quality and consistency touchups - (lib)
- Standardize Taskfile structure and variable declarations - (build)
- Unify c taskfiles and build scripts 
- Update System.IO.Pipelines to 9.0.7 - (deps)

### Fixed

- Extended the unit tests to cover basic zstd compressor checks 
- Improve null safety in zstd compressor - (compression)

### Removed

- Remove unused CompressorStatus enumeration 
- Remove dangling ignore file - (compression)
- Remove unecessary argon2 source code from tree - (argon2)
- Removed more unecessary scripts and test files - (brotli)
- Remove commit limit for cliff commit changelog 

## [0.1.2-rc.6] - 2025-07-04

### Added

- Vendor minimal working copy of zstd c library - (libs)
- Feature C files for zstd have been started but not working - (libs)

### Changed

- Major changes to the build taskfile to account for breaking build changes - (libs)
- Unify malloc and free callbacks for all compressors 
- Update brotli to f2022b9 and close #31 - (deps)

### Fixed

- Correct the breaking changes section of the changelog template - (changelog)

### Removed

- Remove util.h references after memory cleanup 

## [0.1.2-rc.5] - 2025-06-23

### Added

- Implement async methods for fbm stream - (lib)

### Changed

- Replace FBMBuffer with FBMReusableRequestStream - (FBM)
- Improve code structure and readability - (libs)
- Rename DirectAlloc to AllocMemory - (lib)
- Make srcRef in CopyAndPublishDataOnSendPipe readonly - (lib)
- Update AsSpan to use ref readonly - (lib)
- Remove obsolete method DirectAlloc to AllocMemory - (lib)
- Remove obsolete call to DirectAlloc in favor of AllocMemory - (lib)

### Fixed

- Add tests and configuration for FBMRequest - (FBMRequest)
- Add SetLengthTest for stream length handling - (lib)

### Refactor

- **Breaking Change:** Remove FBM client extensions class & rename FBM buffer - (tools)

## [0.1.2-rc.4] - 2025-06-13

### Refactor

- **Breaking Change:** Add changelog readability, and simple filesystem changes 

## [0.1.1] - 2025-05-15

### Added

- Add FNV1a software checksum and basic correction tests 
- Added ReadFileDataAsync function 
- Add a default site adapater and interceptors 
- Allow multiple plugin loading directories 
- Add file path caching support 
- Multi transport listeners 
- Server arch update, Memory struct access - (server)
- Scoped spans for forward writer & tests to sln 
- Enable custom http file cache headers from config - (server)
- Compression library env variable support - (shared)
- Read configuration from stdin - (server)

### Changed

- JWK overhaul & add length getter to FileUpload 
- Update compression header files and macros + Ci build 
- Overhauled native library loading and lazy init 
- Updates, advanced tracing, http optimizations 
- Overhaul C libraries and fix builds 
- Harden some argon2 password hashing 
- Minor non-breaking changes to VNEncoding 
- Immutable tcp listeners 
- Update service stack to reflect new loading patterns 
- Refactor extensions with perf updates 
- Swallow vnlib.webserver into core & build updates - (app)
- #7 Update compression style, platform and linking 
- Testing updates and easier dev-testing 
- Minor code clarity change 
- Switch to -rc version sufffix for master builds 

### Fixed

- Fix _In_ macro for compression public api 
- Missed ! on null pointer check 
- **Breaking Change:** Middlware array, multiple cookie set, and cookie check 
- Improper request buffer property assignment 
- Zero/unsafezero with data types > sizeof(byte) 
- Memory leak: missing fbm loop buffer free 
- #10 Raise exception when max entity size >0 , but uploads == 0 
- Add bsic sample config testing to webserver - (app)
- Cleanup and correct some testing unit functions 
- Add UnsafeMemoryHandle tests 
- 27 remove unecessary platform attributes on test functions 
- Remove library address quotes from trace output 
- Process heap should correctly handle flags & global zero 

### Performance

- Deprecate unsafememoryhandle span extensions 
- Utils + http perf mods 
- Async pre-buffer to avoid sync buffer 
- Absolutely yuge perf boosts 

### Removed

- Remove argon2 docs & optional tcp resuse 

[0.1.2-rc.7]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.7&id2=v0.1.2-rc.6
[0.1.2-rc.6]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.6&id2=v0.1.2-rc.5
[0.1.2-rc.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.5&id2=v0.1.2-rc.4
[0.1.2-rc.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.4&id2=v0.1.1

<!-- generated by git-cliff -->
