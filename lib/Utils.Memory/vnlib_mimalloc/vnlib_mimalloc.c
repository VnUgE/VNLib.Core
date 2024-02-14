/*
* Copyright (c) 2024 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_mimalloc
* File: vnlib_mimalloc.h
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

#define VNLIB_EXPORTING //Exporting when compiling the library

#include "NativeHeapApi.h"
#include <mimalloc.h>

#ifdef _P_IS_WINDOWS

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
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

    /*
    * FIRST CLASS HEAPS ARE NOT CURRENTLY SUPPORTED
    *
    * The mimalloc library requires first class heaps allocate
    * blocks on the thread that creates them. Thats not how this
    * library works, so just pass the shared heap for now to keep
    * things working.
    * 
    * Always clear serialize flag and set shared heap pointer
    * 
    * Shared heap supports realloc, so set the flag
    */

    flags->CreationFlags &= ~(HEAP_CREATION_SERIALZE_ENABLED);
    flags->CreationFlags |= HEAP_CREATION_SUPPORTS_REALLOC;
   
    flags->HeapPointer = heapGetSharedHeapHandle();

    //Ignore remaining flags, zero/sync can be user optional

    //Return value greater than 0
    return flags->HeapPointer;
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapDestroy(HeapHandle heap)
{
    //Destroy the heap if not shared heap
    if (heap != SHARED_HEAP_HANDLE_VALUE)
    {
        mi_heap_delete(heap);
    }

    return (ERRNO)TRUE;
}


VNLIB_HEAP_API void* VNLIB_CC heapAlloc(HeapHandle heap, size_t elements, size_t alignment, int zero)
{
    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        //Allocate the block
        return zero ?
            mi_calloc(elements, alignment) : 
            mi_mallocn(elements, alignment);
    }
    else
    {
        //First class heap, lock is held by caller, optionally zero the block
        return zero ?
            mi_heap_calloc(heap, elements, alignment) : 
            mi_heap_mallocn(heap, elements, alignment);
    }
}


VNLIB_HEAP_API void* VNLIB_CC heapRealloc(HeapHandle heap, void* block, size_t elements, size_t alignment, int zero)
{
    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        //reallocate the block
        return zero ?
			mi_recalloc(block, elements, alignment) :
			mi_reallocn(block, elements, alignment);
    }
    else
    {
        //First class heap realloc
        return zero ?
			mi_heap_recalloc(heap, block, elements, alignment) :
			mi_heap_reallocn(heap, block, elements, alignment);
    }
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapFree(HeapHandle heap, void* block)
{
    (void)heap;
    mi_free(block);
    return (ERRNO)TRUE;
}
