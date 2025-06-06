# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Memory locking (mlock) functions and comprehensive unit testing (closes #27)
- Heap flags tests and global zero preliminary checks
- Comprehensive XML documentation comments to resolve compiler warnings
- ZSTD compression feature preparation in compression libraries
- Enhanced FBM (Fixed Buffer Messaging) client quality improvements
- New error handling and status code management for HTTP responses
- UnsafeMemoryHandle tests for better memory safety validation
- Enhanced ERRNO error code support and handling

### Changed
- **BREAKING**: Refactored async resource handling and removed lots of dead code
- **BREAKING**: Simplified filesystem API for better readability and added features
- Improved HTTP response handling - status codes are now set last (closes #29)
- Updated NuGet dependencies across multiple projects
- Enhanced memory management with better heap flag handling
- Simplified internal memory move operations (memmove) for better performance
- Updated compression library architecture to support ZSTD
- Improved FBM messaging quality and reliability
- Enhanced build configuration for new containers and unified testing
- Updated GitVersion configuration for better version handling
- Migrated to -rc version suffix for master builds
- Updated all C libraries to CMake 3.18 standard
- Enhanced logging extensions with more comprehensive functionality

### Fixed
- Process heap now correctly handles flags and global zero allocation
- Removed library address quotes from trace output
- Base64 encoding tests now work correctly
- Missing test loop issues resolved
- JSON Web Key handling improvements
- Memory copy utility internal API improvements
- Various code cleanup and readability improvements
- XML comment warnings across the codebase
- Command listener cleanup for unknown commands in WebServer

### Removed
- Deprecated APIs and unused code types
- AsyncUpdatableResource and related async resource abstractions
- IJsonSerializerBuffer interface (replaced with better alternatives)
- Unnecessary platform attributes on test functions
- Unused abstractions from copy utility internal API

### Infrastructure
- Updated build scripts and output file management
- Enhanced CI/CD pipeline with better container support
- Enhanced testing framework with better unit test coverage
- Updated native library dates and Git URLs

## [0.1.1]

**Initial Pre-release**