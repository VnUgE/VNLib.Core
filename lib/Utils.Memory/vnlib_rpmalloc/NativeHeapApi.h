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
* of the License, or (at your option) any later version.
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

/* Set api export calling convention (allow used to override) */
#ifndef VNLIB_CC
    #ifdef _P_IS_WINDOWS
        /* STD for importing to other languages such as.NET */
    #define VNLIB_CC __stdcall
    #else
        #define VNLIB_CC 
    #endif
#endif /* !VNLIB_CC */

#ifndef VNLIB_HEAP_API	/* Allow users to disable the export/impoty macro if using source code directly */
    #ifdef VNLIB_EXPORTING
        #ifdef _P_IS_WINDOWS
            #define VNLIB_HEAP_API __declspec(dllexport)
        #else
            #define VNLIB_HEAP_API __attribute__((visibility("default")))
        #endif /* _P_IS_WINDOWS */
    #else
        #ifdef _P_IS_WINDOWS
            #define VNLIB_HEAP_API __declspec(dllimport)
        #else
            #define VNLIB_HEAP_API extern
        #endif /* _P_IS_WINDOWS */
    #endif /* !VNLIB_EXPORTING */
#endif /* !VNLIB_EXPORT */

/* Internal heap creation flags passed to the creation method by the library loader */
typedef enum HeapCreationFlags
{
    /* Default/no flags */
    HEAP_CREATION_NO_FLAGS,
    /* Specifies that all allocations be zeroed before returning to caller */
    HEAP_CREATION_GLOBAL_ZERO = 0x01,
    /* Specifies that the heap should use internal locking, aka its not thread safe
    and needs to be made thread safe */
    HEAP_CREATION_SERIALZE_ENABLED = 0x02,
    /* Specifies that the requested heap will be a shared heap for the process/library */
    HEAP_CREATION_IS_SHARED = 0x04,
    /* Specifies that the heap will support block reallocation */
    HEAP_CREATION_SUPPORTS_REALLOC = 0x08
} HeapCreationFlags;

/* The vnlib ERRNO type, integer/process dependent,
 internally represented as a pointer 
*/
typedef void* ERRNO;

/* A pointer to a heap structure that was stored during heap creation */
typedef void* HeapHandle;

/* A structure for heap initialization */
typedef struct UnmanagedHeapDescriptor
{
    HeapHandle HeapPointer;
    ERRNO Flags;
    HeapCreationFlags CreationFlags;
} UnmanagedHeapDescriptor;

/* Gets the shared heap handle for the process/library
Returns: A pointer to the shared heap 
*/
VNLIB_HEAP_API HeapHandle VNLIB_CC heapGetSharedHeapHandle(void);

/* The heap creation method. You must set the flags->HeapPointer = your heap
structure.
Parameters:
    flags - Creation flags passed by the caller to create the heap. This structure will be initialized, and may be modified
Returns: A boolean value that indicates the result of the operation 
*/
VNLIB_HEAP_API ERRNO VNLIB_CC heapCreate(UnmanagedHeapDescriptor* flags);

/* Destroys a previously created heap
Parameters:
    heap - The pointer to your custom heap structure from heap creation 
*/
VNLIB_HEAP_API ERRNO VNLIB_CC heapDestroy(HeapHandle heap);

/* Allocates a block from the desired heap and returns a pointer
to the block. Optionally zeros the block before returning

Parameters:
    heap - A pointer to your heap structure
    elements - The number of elements to allocate
    alignment - The alignment (or size) of each element in bytes
    zero - A flag to zero the block before returning the block

Returns: A pointer to the allocated block 
*/
VNLIB_HEAP_API void* VNLIB_CC heapAlloc(HeapHandle heap, uint64_t elements, uint64_t alignment, int zero);

/* Reallocates a block on the desired heap and returns a pointer to the new block. If reallocation
is not supported, you should only return 0 and leave the block unmodified. The data in the valid
size of the block MUST remain unmodified.

Parameters:
    heap - A pointer to your heap structure
    block - A pointer to the block to reallocate
    elements - The new size of the block, in elements
    alignment - The element size or block alignment
    zero - A flag to zero the block (or the new size) before returning.

Returns: A pointer to the reallocated block, or zero if the operation failed or is not supported 
*/
VNLIB_HEAP_API void* VNLIB_CC heapRealloc(HeapHandle heap, void* block, uint64_t elements, uint64_t alignment, int zero);

/* Frees a previously allocated block on the desired heap.
Parameters:
    heap - A pointer to your heap structure
    block - A pointer to the block to free

Returns: A value that indicates the result of the operation, nonzero if success, 0 if a failure occurred 
*/
VNLIB_HEAP_API ERRNO VNLIB_CC heapFree(HeapHandle heap, void* block);

#endif /* !NATIVE_HEAP_API */
