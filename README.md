
# VNLib.Core

<h4 align="left">
 <a href="https://github.com/VnUgE/vnlib.core">
    <img src="https://img.shields.io/github/repo-size/vnuge/vnlib.core" alt="repo-size" />
  </a>
  <a href="https://www.vaughnnugent.com/Resources/Software/Modules/VNLib.Core-issues">
    <img src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fwww.vaughnnugent.com%2Fapi%2Fgit%2Fissues%3Fmodule%3DVNLib.Core&query=%24%5B'result'%5D.length&label=all%20issues" alt="Issues"/>
  </a>
  <a href="https://github.com/VnUgE/vnlib.core/commits">
    <img src="https://img.shields.io/github/last-commit/vnuge/vnlib.core/develop" alt="Latest commit"/>
  </a>
    <a href="https://www.vaughnnugent.com/Resources/Software/Modules/VNLib.Core">
    <img src="https://img.shields.io/website?url=https%3A%2F%2Fwww.vaughnnugent.com" alt="Website Status"/>
  </a>
</h4>

*A mono-repo for "core" VNLib libraries and applications.*

</br>
<h3 align="center">
 <a href="https://www.vaughnnugent.com/resources/software/modules/VNLib.Core">
   🏠 Project Home
  </a>
  <a href="https://www.vaughnnugent.com/resources/software/articles?tags=docs,_VNLib.Core">
    📖 Documentation
  </a> 
  <a href="https://www.vaughnnugent.com/resources/software/modules">
    📦 NuGet Feed
  </a>
</h3>
</br>

## What is VNLib.Core?
VNLib is a collection of cross platform .NET/C#/C libraries that I maintain for many private and public projects. VNLib.Core is a subset of the larger collection of projects I have named VNLib. This repo contains libraries I consider to be utility only, or building blocks that are individually useful for other projects. You will likely see many or most of them used across may other VNLib type projects. These libraries are meant to be stand-alone, meaning that there are no required* external dependencies (except the [mscorelib](https://github.com/dotnet/runtime)). For example the [VNLib.Utils](lib/Utils/#) library is a standalone, 0 required dependency library that is useful for, logging, common extensions, and a significant collection of memory related utilities. 

## Dependencies 
Any libraries in this repository that contain external dependencies will be mentioned explicitly in the library's readme. I intend to limit this behavior, as it is the reason this repository exists.

## Licensing
Projects contained in this repository are individually licensed, usually GNU copy-left but some are more open, or are modified and/or vendored projects. Builds will contain the required license txt files (along with third-party licenses) in the archive. 

## Index/NameSpaces
**VNLib.**
- [Utils](lib/Utils/#) - A mutli-use library focused on reducing complexity for working with native resources, memory, asynchronous patterns and data-structures, object/resource pooling, critical resource handling, and common logging abstractions.  
- [Utils.Memory](lib/Utils.Memory/#) - Utilty libraries for native memory management framework for VNLib, includes mimalloc and rpmalloc forks.
- [Utils.Cryptography](lib/Utils.Cryptography/#) - Contains vendored copies of recommended/referenced libraries and wrapper libraries such as Argon2 and Monocypher. 
- [Hashing.Portable](lib/Hashing.Portable/#) - Common cryptographic/hashing relate operations in a single package with room to grow. Also Argon2 bindings
- [Net.Http](lib/Net.Http/#) - High performance ^HTTP/1.1 application processing library, transport not included! For web or custom use, custom app layer protocols supported - roll your own web-sockets or SSE if you want! (See [Essentials](lib/Plugins.Essentials/#) library if you want to use mine! It's not bad.)
- [Net.Transport.SimpleTCP](lib/Net.Transport.SimpleTCP/#) - A not-so-simple, performance oriented, low/no allocation, .NET/C# native, TCP listener library using the System.IO.Pipelines architecture with SSL/TLS support.
- [Plugins.Essentials](lib/Plugins.Essentials/#) - Http processing layer library, provides essentials processing abstractions, extensions and data structures for quickly scaffolding extensible web/http based applications. Dynamic file processing, web sockets, stateful sessions, uses, accounts, permissions, dynamic file routing, and more.
- [Plugins](lib/Plugins/#) - Base/shared library for implementing runtime loaded plugins to provide extensibility and development isolation.
- [Plugins.Runtime](lib/Plugins.Runtime/#) - Scaffolding library for implementing and managing runtime loaded plugin (and unloading) instances safely in an application. Can be used standalone by an developer that wants a structured way of incorporating dynamically loaded plugins.
- [Plugins.Essentials.ServiceStack](lib/Plugins.Essentials.ServiceStack/#) - A library for scaffolding structured web applications from the individual http and supporting libraries into a completely managed http service stack for an entire application. 
- [Plugins.PluginBase](lib/Plugins.PluginBase/#) - Base library/api for plugin developers to build fully managed/supported runtime loaded plugins, without worrying about the plumbing, such as the IPlugin api, endpoint creation, and logging! This library is required if you wish to use most of the Plugin.Extensions libraries.
- [Net.Messaging.FBM](lib/Net.Messaging.FBM/#) - Fixed Buffer Messaging protocol, high performance, request/response architecture, client & server library, built atop http and web-sockets. As implied, relies on fixed sized internal buffers that are negotiated to transfer data with minimal overhead for known messaging architectures.
- [Net.Compression](lib/Net.Compression/#) - A cross platform native compression provider and IHttpCompressorManager configured for runtime dynamic loading for high performance native response data compression.
- [Net.Rest.Client](lib/Net.Rest.Client/#) - A library for defining REST api clients via a fluent api by defining sites and endpoints, OAuth2 authenticator for RestSharp, and a simple RestSharp client pool.
- [WebServer](apps/VNLib.WebServer/#) - A high performance, reference .NET 8 web server built the Essentials web framework for building fast, plugin-driven web/http services

The **third-party** directory contains third-party libraries that are forked, modified, and vendored for use in VNLib projects that I will actively maintain.

## Branches
There are currently two branches I use, master and develop. Develop is the my default building branch, all changes are merged into master when I am satisfied. An internal PR is opened, reviewed and merged into master using squash commits to preserve master history and development speed. The develop branch will ALWAYS be ahead of the master branch.

## How is the repo maintained?
I use many internal tools to build and maintain these projects. I use [OneDev](https://code.onedev.io/) for my internal source control (as well as other git tools) and code updates are pushed to GitHub as part of a build process. I use [Task](https://taskfile.dev) in the build process to publish builds for all projects in this repository. Builds are publicly available on my website from the link below. I do *not* intend to expose my internal tools for security reasons. I prefer to keep my dependencies/processes internal, and will be relying on GitHub for as little as possible. 

## Contributing
I'm not currently looking for code contributors for this project and others, and NO contributions will be accepted through GitHub directly. I would prefer you contact me via my contact information on my website if possible. 

I am however interested in what you build with my libraries, especially web-plugins. You may consider viewing my plugin repos to see what I mean. Since I spend most of my time developing the core and extension libraries (and will continue to do) I don't have much time to focus on new plugin libraries used to build web functionality. Some ideas: content management, code documentation management, NuGet feed host (I have an simple internal one I built but its private for now), I **really** want a secure chat application, secure voice chat would be brilliant as well. If you have ideas or have built a plugin you want featured or reviewed, please feel free to get in touch.

## Donations
If you like this project and want to support it or motivate me for faster development you can donate with fiat or on-chain BTC for now.  

Fiat: [Paypal](https://www.paypal.com/donate/?business=VKEDFD74QAQ72&no_recurring=0&item_name=By+donating+you+are+funding+my+love+for+producing+free+software+for+my+community.+&currency_code=USD)  
On-Chain Bitcoin: `bc1qgj4fk6gdu8lnhd4zqzgxgcts0vlwcv3rqznxn9`  
lnurl: ChipTuner@coinos.io  
