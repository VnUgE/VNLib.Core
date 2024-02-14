# Changes

DotnetCorePlugins is/was originally developed by Nate McMaster -> [original repo](https://github.com/natemcmaster/DotNetCorePlugins)

Project maintaince has fallen off, so I've forked it to make some updates and changes. Changes are mostly specific to my application, or features that I don't think are necessary, or are experimental.

Changes made to the project will be documented in each file. Here are some of the changes I've made:

- Update to .NET 8.0 runtime
- Remove hot-reload feature (my runtime implements this better for my uses)
- Update deprecated packages
- Change public api for more control and removed less verbose features that were never used
- Removing conditional compilation for platform supported features
- General .NET 8.0 stdlib updates and syntax changes
- Foced/removed condtional compiliation features