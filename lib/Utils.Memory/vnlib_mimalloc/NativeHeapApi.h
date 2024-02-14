/*
* Copyright (c) 2024 Vaughn Nugent
*
* Library: VNLib
* Package: NativeHeapApi
* File: NativeHeapApi.h
*
* This library is free software; you can redistribute it and/or 
* modify it under the terms of the GNU Lesser General Public License
* as published by the Free Software Foundation; either version 2.1 
* of the License, or  (at your option) any later version.
*
* This library is distributed in the hope that it will be useful, 
* but WITHOUT ANY WARRANTY; without even the implied warranty of 
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with NativeHeapApi. If not, see http://www.gnu.org/licenses/.
*/

#pragma once

#include <stdint.h>

#ifndef NATIVE_HEAP_API
#define NATIVE_HEAP_API

#if defined(_MSC_VER) || defined(WIN32) || defined(_WIN32)
    #define _P_IS_WINDOWS
#endif

//Set api export calling convention (allow used to override)
#ifndef VNLIB_CC
    #ifdef _P_IS_WINDOWS
        //STD for importing to other languages such as .NET
        #define VNLIB_CC __stdcall
    #else
        #define VNLIB_CC 
    #endif
#endif // !NC_CC

#ifndef VNLIB_EXPORT	//Allow users to disable the export/impoty macro if using source code directly
    #ifdef VNLIB_EXPORTING
        #ifdef _P_IS_WINDOWS
            #define VNLIB_HEAP_API __declspec(dllexport)
        #else
            #define VNLIB_HEAP_API __attribute__((visibility("default")))
        #endif // _NC_IS_WINDOWS
    #else
        #ifdef _P_IS_WINDOWS
            #define VNLIB_HEAP_API __declspec(dllimport)
        #else
            #define VNLIB_HEAP_API
        #endif // _P_IS_WINDOWS
    #endif // !VNLIB_EXPORTING
#endif // !VNLIB_EXPORT

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
    HEAP_CREATION_IS_SHARED = 0x04,
    /// <summary>
    /// Specifies that the heap will support block reallocation
    /// </summary>
    HEAP_CREATION_SUPPORTS_REALLOC = 0x08,
} HeapCreationFlags;

#ifdef _P_IS_WINDOWS
    typedef void* LPVOID;
#endif // !WIN32

/// <summary>
/// The vnlib ERRNO type, integer/process dependent, 
/// internally represented as a pointer
/// </summary>
typedef void* ERRNO;

/// <summary>
/// A pointer to a heap structure that was stored during heap creation
/// </summary>
typedef void* HeapHandle;

/// <summary>
/// A structure for heap initialization
/// </summary>
typedef struct UnmanagedHeapDescriptor
{
    HeapHandle HeapPointer;
    ERRNO Flags;
    HeapCreationFlags CreationFlags;
} UnmanagedHeapDescriptor;

/// <summary>
/// Gets the shared heap handle for the process/library
/// </summary>
/// <returns>A pointer to the shared heap</returns>
VNLIB_HEAP_API HeapHandle VNLIB_CC heapGetSharedHeapHandle(void);

/// <summary>
/// The heap creation method. You must set the flags->HeapPointer = your heap
/// structure
/// </summary>
/// <param name="flags">Creation flags passed by the caller to create the heap. This structure will be initialized, and may be modified</param>
/// <returns>A boolean value that indicates the result of the operation</returns>
VNLIB_HEAP_API ERRNO VNLIB_CC heapCreate(UnmanagedHeapDescriptor* flags);

/// <summary>
/// Destroys a previously created heap
/// </summary>
/// <param name="heap">The pointer to your custom heap structure from heap creation</param>
VNLIB_HEAP_API ERRNO VNLIB_CC heapDestroy(HeapHandle heap);

/// <summary>
/// Allocates a block from the desired heap and returns a pointer 
/// to the block. Optionally zeros the block before returning
/// </summary>
/// <param name="heap">A pointer to your heap structure</param>
/// <param name="elements">The number of elements to allocate</param>
/// <param name="alignment">The alignment (or size) of each element in bytes</param>
/// <param name="zero">A flag to zero the block before returning the block</param>
/// <returns>A pointer to the allocated block</returns>
VNLIB_HEAP_API void* VNLIB_CC heapAlloc(HeapHandle heap, uint64_t elements, uint64_t alignment, int zero);

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
VNLIB_HEAP_API void* VNLIB_CC heapRealloc(HeapHandle heap, void* block, uint64_t elements, uint64_t alignment, int zero);

/// <summary>
/// Frees a previously allocated block on the desired heap.
/// </summary>
/// <param name="heap">A pointer to your heap structure</param>
/// <param name="block">A pointer to the block to free</param>
/// <returns>A value that indicates the result of the operation, nonzero if success, 0 if a failure occurred </returns>
VNLIB_HEAP_API ERRNO VNLIB_CC heapFree(HeapHandle heap, void* block);

#endif // !NATIVE_HEAP_API