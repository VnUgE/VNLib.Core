/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_mimalloc
* File: vnlib_mimalloc.c
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

#define VNLIB_EXPORTING

#include "NativeHeapApi.h"
#include <mimalloc.h>

#ifdef _P_IS_WINDOWS

#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#else

#include <stddef.h>
#define TRUE 1
#define FALSE 0

#endif


#define SHARED_HEAP_HANDLE_VALUE ((HeapHandle)1)

VNLIB_HEAP_API HeapHandle VNLIB_CC heapGetSharedHeapHandle(void)
{
    //Return the shared heap pointer
    return SHARED_HEAP_HANDLE_VALUE;
}

VNLIB_HEAP_API ERRNO VNLIB_CC heapCreate(UnmanagedHeapDescriptor* flags)
{
    //All heaps support resizing
    flags->CreationFlags |= HEAP_CREATION_SUPPORTS_REALLOC;

    /*
    * Neither first class, nor shared heaps require thread 
    * synchronization
    */
    flags->CreationFlags &= ~(HEAP_CREATION_SERIALZE_ENABLED);

    // If shared heap requested, return it
    if (flags->CreationFlags & HEAP_CREATION_IS_SHARED)
    {        
        flags->HeapPointer = heapGetSharedHeapHandle();
    }
    else
    { 
        //Allocate a first-class heap
        flags->HeapPointer = mi_heap_new();
    }

    // Only used as a boolean but ERRNO is a pointer type so we can just return it
    // the runtime checks != 0;
    return flags->HeapPointer;
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapDestroy(HeapHandle heap)
{
    //Destroy non-shared heaps
    if (heap != SHARED_HEAP_HANDLE_VALUE)
    {
        //Free all live blocks and destroy the heap
        mi_heap_destroy(heap);
    }

    return (ERRNO)TRUE;
}


VNLIB_HEAP_API void* VNLIB_CC heapAlloc(HeapHandle heap, uint64_t elements, uint64_t alignment, int zero)
{
#if SIZE_MAX < UINT64_MAX
    //Check multiplication overflow: if alignment is non-zero and elements exceeds the safe limit
    if (alignment != 0 && elements > (SIZE_MAX / alignment)) return NULL;
#endif

    //Check for shared/global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        //Allocate the block from default functions
        return zero ?
            mi_calloc((size_t)elements, (size_t)alignment) :
            mi_mallocn((size_t)elements, (size_t)alignment);
    }
    else
    {
        //First class heap allocation with alignment info
        return zero ?
            mi_heap_calloc(heap, (size_t)elements, (size_t)alignment) :
            mi_heap_mallocn(heap, (size_t)elements, (size_t)alignment);
    }
}


VNLIB_HEAP_API void* VNLIB_CC heapRealloc(HeapHandle heap, void* block, uint64_t elements, uint64_t alignment, int zero)
{
#if SIZE_MAX < UINT64_MAX
    //Check multiplication overflow: if alignment is non-zero and elements exceeds the safe limit
    if (alignment != 0 && elements > (SIZE_MAX / alignment)) return NULL;
#endif

    //Check for shared/global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        //reallocate on default heap
        return zero ?
            mi_recalloc(block, (size_t)elements, (size_t)alignment) :
            mi_reallocn(block, (size_t)elements, (size_t)alignment);
    }
    else
    {
        //First class heap realloc
        return zero ?
            mi_heap_recalloc(heap, block, (size_t)elements, (size_t)alignment) :
            mi_heap_reallocn(heap, block, (size_t)elements, (size_t)alignment);
    }
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapFree(HeapHandle heap, void* block)
{
    (void)heap;
    mi_free(block);
    return (ERRNO)TRUE;
}
