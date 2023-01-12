# VNLib.Core

*A Mono-repo for "core" VNLib projects for simplicity.*

### What is VNLib.Core?

VNLib is a collection of .NET/C# (mostly) libraries that I maintain for many private and public projects. VNLib.Core is a subset of the larger collection of projects I have named VNLib. This repo contains libraries I consider to be utility only, or building blocks that are individually useful for other projects. You will likely see many or most of them used across may other VNLib type projects. These libraries are meant to be stand-alone, meaning that there are no required* external dependencies (except the **[mscorelib](https://github.com/dotnet/runtime)**). For example the VNLib.Utils library is a standalone, 0 required dependency library that is useful for, logging, common extensions, and a significant collection of memory related utilities. 

Any libraries in this repository that contain external dependencies will be mentioned explicitly in the library's readme. I intend to limit this behavior, as it is the reason this repository exists.

### How is this repository maintained?
I use many internal tools to build and maintain these projects. I use [OneDev](https://code.onedev.io/) for my internal source control and updates are pushed to GitHub as part of a build process. I use [Task](https://taskfile.dev) to maintain the build/release for all projects in this repository. Builds are publicly available on my [website](https://www.vaughnnugent.com/resources/software) described in the builds section. I do *not* intend to expose my internal tools for security reasons.

### Licensing
Projects contained in this repository are individually licensed, either GNU GPL V2+ or GNU GPL GPL Affero V3+. Builds contain the required license txt files in the archive.

### Why so many individual projects?
Motivation: I work on many different types of projects and often require modules that I have built before but haven't maintained etc. To solve this issue I prioritize code-reuse by maintaining a smaller subset of libraries that I may include as-needed (similar to how .NET core/5+ maintains libraries outside of mscorelib). I also have a strong dislike for including projects that require many external dependencies for a simple feature/function. I would prefer the ability to include the least amount if code required to get the job done with the least amount of side-effects. Finally I prefer homogeneity in my projects so I tend to use .NET implementations opposed to 3rd party even if the implementation is not as performant for my use-cases.

### Builds
Builds contain the individual components listed below packaged per-project, available for download on my [website](https://www.vaughnnugent.com/resources/software). Builds are maintained manually for now until I build a testing pipeline. Build packages will be tar +gzipped (except for nuget packages). 

*All downloads will contain a sha384 hash of the file by adding a .sha384 to the desired file download, eg: debug.tgz.sha384*
*PGP signed downloads will be available eventually*

- Project source code (src.tgz)
- Nuget package (where applicable), debug w/ symbols & source + release (pkg/buildType/projName.version.nupkg)
- Debug build w/ symbols & xml docs (debug.tgz)
- Release build (release.tgz)