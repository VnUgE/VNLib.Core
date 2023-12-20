# VNLib.Core

*A Mono-repo for "core" VNLib projects for simplicity.*

### What is VNLib.Core?
VNLib is a collection of cross platform .NET/C#/C libraries that I maintain for many private and public projects. VNLib.Core is a subset of the larger collection of projects I have named VNLib. This repo contains libraries I consider to be utility only, or building blocks that are individually useful for other projects. You will likely see many or most of them used across may other VNLib type projects. These libraries are meant to be stand-alone, meaning that there are no required* external dependencies (except the [mscorelib](https://github.com/dotnet/runtime)). For example the [VNLib.Utils](lib/Utils/#) library is a standalone, 0 required dependency library that is useful for, logging, common extensions, and a significant collection of memory related utilities. 

### Dependencies 
Any libraries in this repository that contain external dependencies will be mentioned explicitly in the library's readme. I intend to limit this behavior, as it is the reason this repository exists.

### Licensing
Projects contained in this repository are individually licensed, either GNU GPL V2+ or GNU GPL Affero V3+. Builds contain the required license txt files in the archive. Reasoning: some projects are expected for server use, that's really about it, I prefer the the GPL v2 license for other applications where usage will be more broad. For example, the [Utils](lib/Utils/#) library is used for all sorts of projects. 

### Documentation
Docs and articles will be available from the docs link below . There are docs per project (as they are added) and docs tagged for the VNLib.Core module. I will also be adding "spec" or specification articles to explain the higher level concepts behind the design. 

### Contact info
Again, go to my website below, my email address is available, go ahead and send me a message. Or use the email address from my profile to send me an email (via proton mail for now)

### Links
[Home Page](https://www.vaughnnugent.com) - Website home page  
[Documentation](https://www.vaughnnugent.com/resources/software/articles?tags=docs,_VNLib.Core) - Docs and articles for this module  
[Builds for VNLib.Core](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core) - Per-project build artifacts  
[Links for Nuget Feeds](https://www.vaughnnugent.com/resources/software/modules) - Get my NuGet feed links  

### .NET Version Notice
I prefer sticking with lts .NET versions and 8 just released, so I will likely be upgrading once its "stable" and I have the time to port and test everything. Core libraries will be first then downstream packages.  

## Index/NameSpaces
**VNLib.**
- [Utils](lib/Utils/#) - A mutli-use library focused on reducing complexity for working with native resources, memory, asynchronous patterns and data-structures, object/resource pooling, critical resource handling, and common logging abstractions. 
- [Hashing.Portable](lib/Hashing.Portable/#) - Common cryptographic/hashing relate operations in a single package with room to grow. Also Argon2 bindings
- [Net.Http](lib/Net.Http/#) - High performance ^HTTP/1.1 application processing library, transport not included! For web or custom use, custom app layer protocols supported - roll your own web-sockets or SSE if you want! (See [Essentials](lib/Plugins.Essentials/#) library if you want to use mine! It's not bad.)
- [Net.Transport.SimpleTCP](lib/Net.Transport.SimpleTCP/#) - A not-so-simple, performance oriented, low/no allocation, .NET/C# native, TCP listener library using the System.IO.Pipelines architecture with SSL/TLS support.
- [Plugins.Essentials](lib/Plugins.Essentials/#) - Http processing layer library, provides essentials processing abstractions, extensions and data structures for quickly scaffolding extensible web/http based applications. Dynamic file processing, web sockets, stateful sessions, uses, accounts, permissions, dynamic file routing, and more.
- [Plugins](lib/Plugins/#) - Base/shared library for implementing runtime loaded plugins to provide extensibility and development isolation.
- [Plugins.Runtime](lib/Plugins.Runtime/#) - Scaffolding library for implementing and managing runtime loaded plugin (and unloading) instances safely in an application. Can be used standalone by an developer that wants a structured way of incorporating dynamically loaded plugins.
- [Plugins.Essentials.ServiceStack](lib/Plugins.Essentials.ServiceStack/#) - A library for scaffolding structured web applications from the individual http and supporting libraries into a completely managed http service stack for an entire application. 
- [Plugins.PluginBase](lib/Plugins.PluginBase/#) - Base library/api for plugin developers to build fully managed/supported runtime loaded plugins, without worrying about the plumbing, such as the IPlugin api, endpoint creation, and logging! This library is required if you wish to use most of the Plugin.Extensions libraries.
- [Net.Messaging.FBM](lib/Net.Messaging.FBM/#) - Fixed Buffer Messaging protocol, high performance, request/response architecture, client & server library, built atop http and web-sockets. As implied, relies on fixed sized internal buffers that are negotiated to transfer data with minimal overhead for known messaging architectures.
- [Utils.Memory](lib/Utils.Memory/#) - Utilty libraries for native memory management framework for VNLib, including an x64 CMake build of rpmalloc.
- [Net.Compression](lib/Net.Compression/#) - A cross platform native compression provider and IHttpCompressorManager configured for runtime dynamic loading for high performance native response data compression.
- [Net.Rest.Client](lib/Net.Rest.Client/#) - A library for defining REST api clients via a fluent api by defining sites and endpoints, OAuth2 authenticator for RestSharp, and a simple RestSharp client pool.
- [Utils.Cryptography](lib/Utils.Cryptography/#) - Contains vendored copies of recommended/referenced libraries and wrapper libraries such as Argon2 and Monocypher. 

## Builds & Source
Builds contain the individual components listed below packaged per-project, available for download on my website. Build packages will be tgz archives (except for nuget packages). You can obtain debug and release builds, along with per-project source code 

[Link to builds](https://www.vaughnnugent.com/resources/software/modules/VNLib.Core)

### NuGet Feeds
NuGet feeds for release and debug packages are available on my website as the links are subject to change. Debug packages contain symbols and source code, and both contain XML documentation. (Search is now working)

[Links for Nuget Feeds](https://www.vaughnnugent.com/resources/software/modules)

## Branches
There are currently two branches I use, master and develop. Develop is the my default building branch, all changes are merged into master when I am satisfied. An internal PR is opened, reviewed and merged into master, you will notice this merge often consists of multiple commits. The develop branch will ALWAYS be ahead of the master branch.

## Notes
As with all of my VNlib projects, there are in a very early pre-release state. This is my first large-scale software engineering project and its not in highest of priorities to be a good "software developer", sorry :smiley:. I care about the highest performance code I know how to make for my applications regardless of complexity. I use ALL of the packages/libraries in these projects in my own infrastructure and as issues appear I try to fix and test them before publishing the build packages on my website (see builds below). I understand that most of my projects are often re-inventing the wheel, I'm aware, I enjoy boilerplate because it's mine... This all subject to change without warning. 

### How is the code for this repo maintained?
I use many internal tools to build and maintain these projects. I use [OneDev](https://code.onedev.io/) for my internal source control (as well as other git tools) and code updates are pushed to GitHub as part of a build process. I use [Task](https://taskfile.dev) in the build process to publish builds for all projects in this repository. Builds are publicly available on my website from the link below. I do *not* intend to expose my internal tools for security reasons. I prefer to keep my dependencies/processes internal, and will be relying on GitHub for as little as possible. 

### My code style
It changes when I learn new things, it will continue to evolve. I live on hard-mode, aka ADHD (surprise...), so I prefer verbosity and very heavy comments, recognition is better than recall. Comments may get outdated from time to time, sorry, again pattern recognition helps me remember what all 450k lines of code do at a glance (among all my repos at this stage).

### Why so many individual projects? - Motivation (TLDR)
I originally wanted the ability to listen for simple HTTP connections without the beast of ASP and learning curve involved, and wanted to test my skills. I wanted something that was up-to-date, actually cross-platform, and maybe even compete with ASP. At the same time, I wanted to provide abstractions and layers for other engineers to be able to choose the pieces they want to their project.  Finally, I also wanted few to no 3rd party dependencies. These libraries were eventually broken down into their useful pieces and may continue to expand in the future. 

## Contributing
I'm not currently looking for code contributors for this project and others, and NO contributions will be accepted through GitHub directly. I would prefer you contact me via my contact information on my website if possible. 

I am however interested in what you build with my libraries, especially web-plugins. You may consider viewing my plugin repos to see what I mean. Since I spend most of my time developing the core and extension libraries (and will continue to do) I don't have much time to focus on new plugin libraries used to build web functionality. Some ideas: content management, code documentation management, NuGet feed host (I have an simple internal one I built but its private for now), I **really** want a secure chat application, secure voice chat would be brilliant as well. If you have ideas or have built a plugin you want featured or reviewed, please feel free to get in touch.