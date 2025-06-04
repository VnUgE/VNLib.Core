# VNLib C# Style Guide

*A comprehensive style guide for C# development within the VNLib ecosystem, based on established conventions and performance-first principles. This style guide closely follows classic C conventions*

---

## Table of Contents

1. [Philosophy](#philosophy)
2. [File Organization](#file-organization)
3. [Naming Conventions](#naming-conventions)
4. [Formatting Guidelines](#formatting-guidelines)
5. [Code Organization](#code-organization)
6. [Performance and Memory Management](#performance-and-memory-management)
7. [Error Handling and Defensive Programming](#error-handling-and-defensive-programming)
8. [Documentation](#documentation)
9. [Threading and Concurrency](#threading-and-concurrency)
10. [Platform Considerations](#platform-considerations)
11. [Code Examples](#code-examples)

---

## Philosophy

VNLib follows a **performance-first, safety-conscious** approach to C# development with these core principles:

- **Performance Over Convenience**: Choose the most efficient solution, even if it requires more code
- **Memory Efficiency**: Minimize allocations, prefer stack allocation and object pooling
- **Defensive Programming**: Validate inputs early and handle edge cases explicitly
- **Platform Awareness**: Write code that works efficiently across different operating systems
- **Explicit Over Implicit**: Make intentions clear through explicit code rather than relying on defaults
- **Allman/BSD Bracing**: Opening braces must always be on new lines
- **No `var` Keyword**: All variable types must be explicitly declared
- **Modern Validation**: Prefer .NET 8.0+ ArgumentException.ThrowX methods

---

## File Organization

### File Headers

Every C# source file must include the VNLib copyright header:

```csharp
/*
* Copyright (c) 2025 Vaughn Nugent
* 
* Library: VNLib
* Package: [PackageName]
* File: [FileName.cs]
*
* [FileName.cs] is part of [PackageName] which is part of the larger 
* VNLib collection of libraries and utilities.
*
* [PackageName] is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* [PackageName] is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with [PackageName]. If not, see http://www.gnu.org/licenses/.
*/
```

### Using Statements

- Place `using` statements at the top of the file, outside any namespace declarations
- Order alphabetically, with `System` namespaces first
- Use global `using` statements sparingly and only for truly universal types
- Prefer explicit type names over aliases unless the alias significantly improves readability

```csharp
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using VNLib.Utils.Memory;
using VNLib.Utils.Extensions;
```

### File Naming

- Use `PascalCase` for file names: `MemoryUtil.cs`, `SafeLibraryHandle.cs`
- File name should match the primary class name when possible
- For partial classes, use descriptive suffixes: `MemoryUtil.CopyUtilCore.cs`
- Prefer one core class per file unless classes are tightly coupled

---

## Naming Conventions

### General Rules

Follow Microsoft's C# naming guidelines with VNLib-specific conventions:

| Element | Convention | Example |
|---------|------------|---------|
| Classes, Methods, Properties, Enums | `PascalCase` | `MemoryUtil`, `GetReference()`, `IsLoaded` |
| Local variables, parameters | `camelCase` | `elementCount`, `buffer` |
| Private/protected fields | `_camelCase` | `_instance`, `_lock` |
| Constants | `UPPER_CASE` | `SHARED_HEAP_SIZE`, `READ_MSK` |
| Interface names | `I` + `PascalCase` | `IUnmangedHeap`, `IMemoryHandle<T>` |

### VNLib-Specific Patterns

- Use descriptive suffixes for specialized types:
  - `*Handle` for resource handles: `MemoryHandle<T>`, `SafeMethodHandle<T>`
  - `*Manager` for lifecycle management: `PrivateStringManager`
  - `*Wrapper` for adapter patterns: `TrackedHeapWrapper`
  - `*Base` for abstract base classes: `UnmanagedHeapBase`

- Constants should be grouped logically and use meaningful prefixes:
  ```csharp
  // Privilege masks
  public const ulong READ_MSK = 0x0000000000000001L;
  public const ulong WRITE_MSK = 0x0000000000000004L;
  
  // Entry keys
  private const string BROWSER_ID_ENTRY = "acnt.bid";
  private const string FAILED_LOGIN_ENTRY = "acnt.flc";
  ```

---

## Formatting Guidelines

### Indentation and Spacing

- **Use 4 spaces for indentation**
- **Maximum line length: 120 characters**
- No tabs - configure your editor to use spaces

### Braces and Line Breaks

**REQUIRED**: Use Allman/BSD style bracing (opening brace on new line):

```csharp
public void Method()
{
    // implementation
}

if (condition)
{
    // code
}
else
{
    // code
}
```

- Use braces even for single statements:
  ```csharp
  if (condition)
  {
      DoSomething();
  }
  ```

### Method Parameters

For method parameters, follow these rules:
- Parameters may stay on one line if they fit comfortably on a small screen width
- When wrapping is required, **ALL parameters must be on separate lines**:

```csharp
// Acceptable - fits on one line
public void ShortMethod(string param1, int param2)
{
    // implementation
}

// Required format for long parameter lists - ALL parameters on new lines
public void LongMethod(
    string parameter1,
    int parameter2,
    bool parameter3,
    object parameter4
)
{
    // implementation
}

// Constructor example
public class ExampleClass(
    ILogger logger,
    IConfiguration configuration,
    IServiceProvider serviceProvider
) : BaseClass
{
    // implementation
}
```

### Attributes

- Place attributes on the line above the element they modify
- Separate multiple attributes with newlines:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
[SecurityCritical]
public static unsafe void* GetPointer<T>(ref T value) where T : unmanaged
```

### Variable Declarations

**PROHIBITED**: The `var` keyword is strictly forbidden. All types must be explicitly declared:

```csharp
// CORRECT - Explicit type declarations
string message = "Hello World";
Dictionary<string, int> lookup = new Dictionary<string, int>();
IMemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(1024);
Task<bool> result = ProcessAsync();

// INCORRECT - var keyword forbidden
var message = "Hello World";           // ❌ 
var lookup = new Dictionary<...>();    // ❌
var handle = MemoryUtil.Shared...      // ❌
```

You may use .NET 6.0 constructor syntax to sorten instantiations, but only when the type is clear and unambiguous:

```csharp
// CORRECT - .NET 6.0+ constructor syntax
string message = new("Hello World");
Dictionary<string, int> lookup = new();
IMemoryHandle<byte> handle = MemoryUtil.Shared.Alloc<byte>(1024);
Task<bool> result = ProcessAsync();
```

### Argument Validation

**PREFERRED**: Use .NET 8.0+ ArgumentException.ThrowX methods:

```csharp
// CORRECT - Modern validation patterns
public void ProcessData(string input, int count, object target)
{
    ArgumentException.ThrowIfNullOrEmpty(input);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentNullException.ThrowIfNull(target);
}

// ACCEPTABLE but not preferred
public void ProcessDataOld(string input, int count, object target)
{
    string validInput = input ?? throw new ArgumentNullException(nameof(input));
    if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
    object validTarget = target ?? throw new ArgumentNullException(nameof(target));
}
```

---

## Code Organization

### Class Member Ordering

Organize class members in the following order:

1. **Nested classes, enums, delegates, and events**
2. **Constants and static readonly fields**
3. **Static fields and properties**
4. **Instance fields and properties**
5. **Constructors and finalizers**
6. **Static methods**
7. **Instance methods**

Within each group, order by accessibility:
1. `public`
2. `internal`
3. `protected internal`
4. `protected`
5. `private`

### Modifier Order

Use this exact order for modifiers:
```
public protected internal private new abstract virtual override sealed static readonly extern unsafe volatile async
```

### Example Class Structure

```csharp
public class ExampleClass : BaseClass, IInterface
{
    // Constants
    private const int DEFAULT_SIZE = 1024;
    public const string VERSION = "1.0.0";
    
    // Static fields
    private static readonly object _staticLock = new();
    public static int InstanceCount { get; private set; }
    
    // Instance fields
    private readonly string _name;
    private int _count;
    
    // Properties
    public string Name => _name;
    public bool IsValid { get; private set; }
    
    // Constructors
    public ExampleClass(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
    }
    
    // Public methods
    public void DoSomething()
    {
        // implementation
    }
    
    // Private methods
    private void Initialize()
    {
        // implementation
    }
}
```

---

## Performance and Memory Management

### Memory Allocation Guidelines

- **Minimize heap allocations** in hot paths
- **Prefer stackalloc** for small, short-lived buffers:
  ```csharp
  Span<byte> buffer = stackalloc byte[256];
  ```

- **Use object pooling** for frequently allocated objects:
  ```csharp
  private static readonly ObjectRental<StringBuilder> _builderPool = 
      ObjectRental.CreateReusable<StringBuilder>();
  ```

- **Prefer unsafe operations** when performance is critical and safety can be guaranteed:
  ```csharp  
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static unsafe ref T GetReference<T>(void* ptr) where T : unmanaged
  {
      return ref Unsafe.AsRef<T>(ptr);
  }
  ```

### Generic Constraints

Be explicit with generic constraints for better performance and clarity:

```csharp
public static void Process<T>(T[] array) where T : unmanaged
{
    // Implementation can make assumptions about T
}

public static void Initialize<T>(Span<T> span) where T : struct
{
    // More efficient initialization possible
}
```

### Aggressive Inlining

Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for:
- Simple property getters/setters
- Wrapper methods
- Performance-critical small methods
- Validation helpers

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void ThrowIfNull<T>(T? obj, string paramName) where T : class
{
    ArgumentNullException.ThrowIfNull(obj, paramName);
}
```

---

## Error Handling and Defensive Programming

### Input Validation

**Always validate inputs early** using the appropriate validation method:

```csharp
public void ProcessData(byte[] data, int offset, int count)
{
    ArgumentNullException.ThrowIfNull(data);
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, data.Length, nameof(count));
    
    // Process data
}
```

### Custom Validation Methods

Create reusable validation helpers:

```csharp
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static void CheckBounds<T>(ReadOnlySpan<T> block, int offset, int count)
{
    ArgumentOutOfRangeException.ThrowIfNegative(offset);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + count, block.Length, nameof(count));
}
```

### Exception Handling

- Use specific exception types when possible
- Avoid catching and rethrowing without adding value
- Use `using` statements for disposable resources
- Implement proper cleanup in finalizers when managing unmanaged resources

```csharp
public void ProcessFile(string path)
{
    try
    {
        using StreamReader stream = File.OpenRead(path);
        // Process stream
    }
    catch (FileNotFoundException ex)
    {
        // Handle specific case
        throw new InvalidOperationException($"Required file not found: {path}", ex);
    }
}
```

### Debug Assertions

Use Debug.Assert for internal consistency checks:

```csharp
Debug.Assert(_instance != null, "Instance should be initialized");
Debug.Assert(elements > 0, "Element count must be positive");
```

---

## Documentation

### XML Documentation

Document all public APIs with XML documentation:

```csharp
/// <summary>
/// Allocates a block of unmanaged memory and returns a handle to it.
/// </summary>
/// <typeparam name="T">The unmanaged type to allocate</typeparam>
/// <param name="elements">The number of elements to allocate</param>
/// <param name="zero">Whether to zero the allocated memory</param>
/// <returns>A memory handle representing the allocated block</returns>
/// <exception cref="ArgumentOutOfRangeException">Thrown when elements is negative</exception>
/// <exception cref="OutOfMemoryException">Thrown when allocation fails</exception>
public static MemoryHandle<T> Alloc<T>(int elements, bool zero = false) where T : unmanaged
```

### Inline Comments

Use comments to explain **why**, not **what**:

```csharp
// Avoid static initializer to prevent potential loader lock issues
private static IUnmangedHeap InitSharedHeapInternal()
{
    // Implementation
}

// Use aggressive inlining for performance-critical path
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static ref T GetReference<T>(this MemoryHandle<T> handle) where T : unmanaged
```

---

## Threading and Concurrency

### Thread Safety Patterns

- **Prefer immutable types** where possible
- **Use proper locking** when mutation is necessary:
  ```csharp  private readonly object _lock = new();
  
  public void Update()
  {
      lock (_lock)
      {
          // Critical section
      }
  }
  ```

- **Use lazy initialization** for expensive objects:
  ```csharp
  private static readonly LazyInitializer<IUnmangedHeap> _lazyHeap = 
      new(InitSharedHeapInternal);
  
  public static IUnmangedHeap Shared => _lazyHeap.Instance;
  ```

### Async Patterns

- **Use ConfigureAwait(false)** in library code:
  ```csharp
  ResultType result = await SomeAsyncOperation().ConfigureAwait(false);
  ```

- **Prefer ValueTask** for frequently called async methods that often complete synchronously
- **Pass CancellationToken** parameters for long-running operations

---

## Platform Considerations

### Conditional Compilation

Use platform-specific attributes and conditional compilation:

```csharp
[SupportedOSPlatform("windows")]
[SupportedOSPlatform("linux")]
public static bool LockMemory<T>(MemoryHandle<T> handle) where T : unmanaged
{
#if WINDOWS
    return WindowsLockMemory(handle);
#elif LINUX
    return LinuxLockMemory(handle);
#else
    return false;
#endif
}
```

### Environment Variables

Use environment variables for runtime configuration of libraries:

```csharp
private static IUnmangedHeap InitSharedHeapInternal()
{
    ERRNO diagEnable = ERRNO.TryParse(Environment.GetEnvironmentVariable("VNLIB_SHARED_HEAP_DIAG"), out ERRNO result) ? result : ERRNO.FALSE;
    
    if (diagEnable)
    {
        Trace.WriteLine("Shared heap diagnostics enabled");
    }
    
    // Implementation
}
```

---

This style guide represents the VNLib approach to C# development, emphasizing performance, safety, and maintainability. It should be used as the foundation for all C# code within the VNLib ecosystem.
