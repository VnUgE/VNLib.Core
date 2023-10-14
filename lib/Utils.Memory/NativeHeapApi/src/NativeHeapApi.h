/*
* Copyright (c) 2023 Vaughn Nugent
*
* Library: VNLib
* Package: NativeHeapApi
* File: NativeHeapApi.h
*
* NativeHeapApi is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* NativeHeapApi is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with NativeHeapApi. If not, see http://www.gnu.org/licenses/.
*/

#pragma once

#ifndef NATIVE_HEAP_API
#define NATIVE_HEAP_API

/*
* Method calling convention for export
*/
#ifndef HEAP_METHOD_CC 
 #ifdef WIN32
  #define HEAP_METHOD_CC __stdcall
 #else
  #define HEAP_METHOD_CC
 #endif // WIN32
#endif // !HEAP_METHOD_CC


/*
* Decorator for exporting methods for dll usage
*/
#ifndef HEAP_METHOD_EXPORT
 #ifdef WIN32
  #define HEAP_METHOD_EXPORT __declspec(dllexport)
 #else
  #define HEAP_METHOD_EXPORT  __attribute__((visibility("default")))
 #endif
#endif // HEAP_METHOD_EXPORT!

#ifndef WIN32
typedef unsigned long DWORD;
typedef void* LPVOID;
#endif // !WIN32

/// <summary>
/// Internal heap creation flags passed to the creation method by the library loader
/// </summary>
typedef enum HeapCreationFlags
{
    /// <summary>
    /// Default/no flags
    /// </summary>
    HEAP_CREATION_NO_FLAGS,
    /// <summary>
    /// Specifies that all allocations be zeroed before returning to caller
    /// </summary>
    HEAP_CREATION_GLOBAL_ZERO = 0x01,
    /// <summary>
    /// Specifies that the heap should use internal locking, aka its not thread safe
    /// and needs to be made thread safe
    /// </summary>
    HEAP_CREATION_SERIALZE_ENABLED = 0x02,
    /// <summary>
    /// Specifies that the requested heap will be a shared heap for the process/library
    /// </summary>
    HEAP_CREATION_IS_SHARED = 0x04
} HeapCreationFlags;

/// <summary>
/// The vnlib ERRNO type, integer/process dependent, 
/// internally represented as a pointer
/// </summary>
typedef void* ERRNO;

/// <summary>
/// A structure for heap initialization
/// </summary>
typedef struct UnmanagedHeapDescriptor
{
    LPVOID HeapPointer;
    HeapCreationFlags CreationFlags;
    ERRNO Flags;    
} UnmanagedHeapDescriptor;

/// <summary>
/// The heap creation method. You must set the flags->HeapPointer = your heap
/// structure
/// </summary>
/// <param name="flags">Creation flags passed by the caller to create the heap. This structure will be initialized, and may be modified</param>
/// <returns>A boolean value that indicates the result of the operation</returns>
HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapCreate(UnmanagedHeapDescriptor* flags);

/// <summary>
/// Destroys a previously created heap
/// </summary>
/// <param name="heap">The pointer to your custom heap structure from heap creation</param>
HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapDestroy(LPVOID heap);

/// <summary>
/// Allocates a block from the desired heap and returns a pointer 
/// to the block. Optionally zeros the block before returning
/// </summary>
/// <param name="heap">A pointer to your heap structure</param>
/// <param name="elements">The number of elements to allocate</param>
/// <param name="alignment">The alignment (or size) of each element in bytes</param>
/// <param name="zero">A flag to zero the block before returning the block</param>
/// <returns>A pointer to the allocated block</returns>
HEAP_METHOD_EXPORT LPVOID HEAP_METHOD_CC heapAlloc(LPVOID heap, size_t elements, size_t alignment, BOOL zero);

/// <summary>
/// Reallocates a block on the desired heap and returns a pointer to the new block. If reallocation
/// is not supported, you should only return 0 and leave the block unmodified. The data in the valid
/// size of the block MUST remain unmodified.
/// </summary>
/// <param name="heap">A pointer to your heap structure</param>
/// <param name="block">A pointer to the block to reallocate</param>
/// <param name="elements">The new size of the block, in elements</param>
/// <param name="alignment">The element size or block alignment</param>
/// <param name="zero">A flag to zero the block (or the new size) before returning.</param>
/// <returns>A pointer to the reallocated block, or zero if the operation failed or is not supported</returns>
HEAP_METHOD_EXPORT LPVOID HEAP_METHOD_CC heapRealloc(LPVOID heap, LPVOID block, size_t elements, size_t alignment, BOOL zero);

/// <summary>
/// Frees a previously allocated block on the desired heap.
/// </summary>
/// <param name="heap">A pointer to your heap structure</param>
/// <param name="block">A pointer to the block to free</param>
/// <returns>A value that indicates the result of the operation, nonzero if success, 0 if a failure occurred </returns>
HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapFree(LPVOID heap, LPVOID block);

#endif // !NATIVE_HEAP_API