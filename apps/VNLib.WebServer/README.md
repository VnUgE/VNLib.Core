<h1 align="center">VNLib.WebServer</h1> 

<p align="center">
A high performance, reference .NET 8 web server built with VNLib.Core and Essentials web framework for building fast, plugin-driven web/http services.
</p>

<h4 align="center">
  <a href="https://www.vaughnnugent.com/resources/software/modules/VNLib.Core?p=vnlib.webserver">Builds</a> |
  <a href="https://www.vaughnnugent.com/resources/software">My Software</a> |
  <a href="https://www.vaughnnugent.com/resources/software/articles?tags=_vnlib.webserver">Documentation</a>
</h4>

<img src="https://www.vaughnnugent.com/public/blogs/docs/content/pvsa5sttunurphjljc73jhg3mi.png" width="100%">

## Short Intro
VNLib.Webserver is a runtime host for http or "web" bassed server applications. As a standalone application, it can only do basic http file processing via virtual hosts similar to nginx and appache, but when plugins are configured, it becomes a highly versitlile dynamic server application. VNLib.WebServer inclues only the bare minimum binaries for any application, and is infinitly expandable using dynamic assembly loading.  

### Some features
- HTTP 0.9-1.1 support with granular control over http and tcp settings 
- Virtual Hosts: many-to-many hostname-transport configuration (similar to nginx)
- Strong TLS support using .NET SslStream library
- Static file processing
- JSON and Yaml coniguration language support
- CORS resource support and protections
- Per-host error file caching (ex: 403, 404)
- IP based whitelist and blacklist
- File protection by extension
- File path probing/autocomplete (ex: / -> /index.html or / -> custom_file.ext)
- Primitive file http cache control
- Optional console command listener for issuing control commands
- HTTP compression with external library and Brotli as a built-in fallback
- HTTP range support for files

## License 
The software in this repository is licensed under the GNU Affero General Public License (or any later version). See the LICENSE files for more information.  

## Donations
If you like this project and want to support it or motivate me for faster development, please see the parent project's [README](../../#).
