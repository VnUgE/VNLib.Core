# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.2-rc.7] - 2025-07-18

### Added

- Add zstd compression support

### Changed

- Get zstd compression working closes #30
- Remove unused CompressorStatus enumeration
- Some minor code clarity changes
- Switch to debug assertions for state pointers
- Malloc cmake platform generator
- Correct zstd linking and build private copy of rpmalloc for compression
- Runtime compressor feedback and config
- Coed cleanup in http request/response classes
- Cleanup and update the readme
- Minor code quality and consistency touchups
- Add zstd readme notice and commit files
- Standardize Taskfile structure and variable declarations
- Remove unecessary argon2 source code from tree
- Removed more unecessary scripts and test files
- Add debug assertions to alloc functions
- Update ErrorProne.NET.CoreAnalyzers to 0.8.0
- Update System.IO.Pipelines to 9.0.7
- Correct argon2 copyright name
- Adjust cliff config & default taskfile cleanup

### Fixed

- Extended the unit tests to cover basic zstd compressor checks
- Fix build server long path build issues and switch back to parallel dev init
- Improve null safety in zstd compressor
- Fix clone urls for new server changes
- Fix branch triggers for git repo sync job

## [0.1.2-rc.6] - 2025-07-04

### Added

- Vendor minimal working copy of zstd c library
- Feature C files for zstd have been started but not working

### Changed

- Unify malloc and free callbacks for all compressors
- Remove util.h references after memory cleanup
- Update brotli to f2022b9 and close #31
- Update test dependencies

### Fixed

- Correct the breaking changes section of the changelog template
- Fix changelog commit id order

## [0.1.2-rc.5] - 2025-06-23

### Added

- Implement async methods for fbm stream

### Changed

- Replace FBMBuffer with FBMReusableRequestStream
- Improve code structure and readability
- Rename DirectAlloc to AllocMemory
- Make srcRef in CopyAndPublishDataOnSendPipe readonly
- Update AsSpan to use ref readonly
- Remove obsolete method DirectAlloc to AllocMemory
- Remove obsolete call to DirectAlloc in favor of AllocMemory
- Enhance changelog generation with git-cliff and remove automatic ci task
- Update changelog for upcoming release

### Fixed

- Add tests and configuration for FBMRequest
- Add SetLengthTest for stream length handling

## [0.1.2-rc.4] - 2025-06-13

### Fixed

- Closes #29 set status code last
- Fix removed code and remove more unused types

### Removed

- Remove some unused code

## [0.1.1] - 2025-05-15

### Added

- Add .onedev-buildspec.yml
- Add description, remove old version tags
- Add kv documentation
- Add FNV1a software checksum and basic correction tests
- Feat: Buff middleware handlers
- Added ReadFileDataAsync function
- Add a default site adapater and interceptors
- Allow multiple plugin loading directories
- Add file path caching support
- Multi transport listeners
- Server arch update, Memory struct access
- Scoped spans for forward writer & tests to sln
- Enable custom http file cache headers from config
- Compression library env variable support
- Read configuration from stdin
- Add debug testing helper classes
- Add unload delay overload & service count
- Add errors object to webmessage class
- Add some immutability changes and slight working set reduction
- Add tests for monocypher argon2
- Add cmake changes for new library structure and updates with debug support
- Add unsafe alloc nearest page with heap overload
- Add modified brotli source code
- Add COMMIT file to track the hash
- Add modified zlib compression
- Add fedora tests, switch to curl, unlink staging build for now
- Add heap flags tests and global zero prelim check
- Add void* mlock overloads

### Fixed

- Fix FBM session cancel, fix plugin log file names
- Fix FBMMessageHeader default and session connection status
- Fix watcher pause, add base64urlencode
- Fix exceptions, more test coverage, fix sequences, heap flags, and spelling
- Fix tcp buffering with optimization, package updates, and C mem lib readmes
- Fix tx buffer clear after sync send, clear on release
- Fix third-party dir cleanup
- Fix _In_ macro for compression public api
- Missed ! on null pointer check
- **Breaking Change:** Middlware array, multiple cookie set, and cookie check
- Improper request buffer property assignment
- Fix spelling Enqueue and deprecate mispelled version
- Fix compression source tree and source package
- Zero/unsafezero with data types > sizeof(byte)
- Memory leak: missing fbm loop buffer free
- Fix artifact output
- Fix module versioning
- Fix type propagation from abstract json respons serialization & password sample config
- Fix package names & interface log
- Fix log config deserialization. Must be in lax parsing mode
- #10 Raise exception when max entity size >0 , but uploads == 0
- Fix target framework variable, and extend userencoded data api
- Add bsic sample config testing to webserver
- Test package updates
- Fix copyright
- Fix build comments for clarity
- Fix config, inline memory handle init, and fix some exception comments
- Fix a couple naming things
- Fix mimalloc project name for static fpic (fixes #20)
- Fix compilation order, hush xml comment warnings for test exe, call server build from task directly not indirectly
- Fix base64 encoding tests to work correctly
- Fix missing test loop
- Fix mimalloc description
- Cleanup and correct some testing unit functions
- Add UnsafeMemoryHandle tests
- Close #27 and add mlock functions and basic unit testing
- 27 remove unecessary platform attributes on test functions
- Remove library address quotes from trace output
- Process heap should correctly handle flags & global zero
- Closes #27, merge branch 'issue#27' into develop

### Removed

- Removed hrs
- Remove powershell commands
- Removed unrecognized casts, and lingering await
- Remove sleet from build steps
- Remove previously commited dev files
- Remove unused tasks and actualy remeber to pack up the vendored source so it builds on target machines :)
- Remove deprecated apis
- Remove assumption tests for private heaps and add test comments

[0.1.2-rc.7]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.7&id2=v0.1.2-rc.6
[0.1.2-rc.6]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.6&id2=v0.1.2-rc.5
[0.1.2-rc.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.5&id2=v0.1.2-rc.4
[0.1.2-rc.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-core.git/diff?id=v0.1.2-rc.4&id2=v0.1.1

<!-- generated by git-cliff -->
