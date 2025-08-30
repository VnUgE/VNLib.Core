/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_rpmalloc
* File: vnlib_rpmalloc.h
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


/*
* Top level linux definitions
*/
#ifdef __GNUC__

#define _GNU_SOURCE 
#define TRUE 1
#define FALSE 0

#endif

#define VNLIB_EXPORTING //Exporting when compiling the library

#include "NativeHeapApi.h"
#include <rpmalloc.h>

#if defined(_P_IS_WINDOWS)

/*
* setup windows api incudes
*/

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    (void)sizeof(hModule);
    (void)sizeof(lpReserved);
    /*
    * Taken from the malloc.c file for initializing the library.
    * and thread events
    * 
    * Must set null when initlaizing the library so it will use the 
    * default allocation functions. 
    * 
	* Initalize accepts an interface of function that can be used to
    * map virtual memory operating system functions, we want to use the 
	* one built into the library, so set the interface to null.
    */
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        rpmalloc_initialize(NULL);
        break;
    case DLL_THREAD_ATTACH:
        rpmalloc_thread_initialize();
        break;
    case DLL_THREAD_DETACH:
        rpmalloc_thread_finalize();
        break;
    case DLL_PROCESS_DETACH:
        rpmalloc_finalize();
        break;
    }
    return TRUE;
}
#endif

#define SHARED_HEAP_HANDLE_VALUE ((HeapHandle)1)
#define GLOBAL_HEAP_INIT_CHECK() if (!rpmalloc_is_thread_initialized()) { rpmalloc_thread_initialize(); }

//Define the heap methods

VNLIB_HEAP_API HeapHandle VNLIB_CC heapGetSharedHeapHandle(void)
{
    //Return the shared heap pointer
	return SHARED_HEAP_HANDLE_VALUE;
}

VNLIB_HEAP_API ERRNO VNLIB_CC heapCreate(UnmanagedHeapDescriptor* flags)
{
    //All heaps support resizing
    flags->CreationFlags |= HEAP_CREATION_SUPPORTS_REALLOC;

    //Check flags
    if (flags->CreationFlags & HEAP_CREATION_IS_SHARED)
    {
        //User requested the global heap, synchronziation is not required, so we can clear the sync flag
        flags->CreationFlags &= ~(HEAP_CREATION_SERIALZE_ENABLED);

        //For shared heap set pointer to null
        flags->HeapPointer = heapGetSharedHeapHandle();

        //Success
        return (ERRNO)TRUE;
    }
    else
    {
        //Allocate a first class heap
        flags->HeapPointer = rpmalloc_heap_acquire();

        //Ignore remaining flags, zero/sync can be user optional

        //Return value greater than 0
        return flags->HeapPointer;
    }
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapDestroy(HeapHandle heap)
{
    //Destroy non-shared heaps
    if (heap != SHARED_HEAP_HANDLE_VALUE)
    {
        //Free all before destroy
        rpmalloc_heap_free_all(heap);

        //Destroy the heap
        rpmalloc_heap_release(heap);
    }

    return (ERRNO)TRUE;
}


VNLIB_HEAP_API void* VNLIB_CC heapAlloc(HeapHandle heap, size_t elements, size_t alignment, int zero)
{
    //Multiply for element size
    size_t size = elements * alignment;

    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        /*
        * When called from the dotnet CLR the thread may not call the DLL
        * thread attach method, so we need to check and initialze the heap
        * for the current thread
        */
        GLOBAL_HEAP_INIT_CHECK()

        //Allocate the block
        if (zero)
        {
            //Calloc
            return rpcalloc(elements, alignment);
        }
        else
        {
            //Alloc without zero
            return rpmalloc(size);
        }
    }
    else
    {
        //First class heap, lock is held by caller, optionally zero the block
        if (zero)
        {
            return rpmalloc_heap_calloc(heap, alignment, elements);
        }
        else
        {
            return rpmalloc_heap_alloc(heap, size);
        }
    }
}


VNLIB_HEAP_API void* VNLIB_CC heapRealloc(HeapHandle heap, void* block, size_t elements, size_t alignment, int zero)
{
    //Multiply for element size
    size_t size = elements * alignment;
    (void)zero;

    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        /*
        * When called from the dotnet CLR the thread may not call the DLL
        * thread attach method, so we need to check and initialze the heap
        * for the current thread
        */
        GLOBAL_HEAP_INIT_CHECK()

        //Calloc
        return rprealloc(block, size);
    }
    else
    {
        //First class heap, lock is held by caller
        return rpmalloc_heap_realloc(heap, block, size, 0);
    }
}


VNLIB_HEAP_API ERRNO VNLIB_CC heapFree(HeapHandle heap, void* block)
{
    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
    {
        /*
        * If free happens on a different thread, we must allocate the heap
        * its cheap to check
        */

        GLOBAL_HEAP_INIT_CHECK()

        //free block
        rpfree(block);
    }
    else
    {
        //First class heap, lock is held by caller
        rpmalloc_heap_free(heap, block);
    }

    return (ERRNO)TRUE;
}