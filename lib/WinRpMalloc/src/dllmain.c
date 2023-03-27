/*
* Copyright (c) 2023 Vaughn Nugent
*
* Library: VNLib
* Package: WinRpMalloc
* File: dllmain.c
*
* WinRpMalloc is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* WinRpMalloc is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with WinRpMalloc. If not, see http://www.gnu.org/licenses/.
*/

#include "pch.h"
//Include the native heap header directly from its repo location
#include "../../NativeHeapApi/src/NativeHeapApi.h"

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
	/*
    * Taken from the malloc.c file for initializing the library.
    * and thread events
    */
    switch (ul_reason_for_call)
    {
        case DLL_PROCESS_ATTACH:
            rpmalloc_initialize();
            break;
        case DLL_THREAD_ATTACH:
            rpmalloc_thread_initialize();
            break;
        case DLL_THREAD_DETACH:
            rpmalloc_thread_finalize(1);
            break;
        case DLL_PROCESS_DETACH:
            rpmalloc_finalize();
            break;
    }
    return TRUE;
}

#define GLOBAL_HEAP_HANDLE_VALUE -10
#define GLOBAL_HEAP_INIT_CHECK if (!rpmalloc_is_thread_initialized()) { rpmalloc_thread_initialize(); }

//Define the heap methods

HEAP_METHOD_EXPORT ERRNO heapCreate(UnmanagedHeapFlags* flags)
{
    //Check flags
    if (flags->CreationFlags & HEAP_CREATION_IS_SHARED)
    {
        //User requested the global heap, synchronziation is not required, so we can clear the sync flag
        flags->CreationFlags &= ~(HEAP_CREATION_SERIALZE_ENABLED);

        //Set the heap pointer as the global heap value
        flags->HeapPointer = (LPVOID)GLOBAL_HEAP_HANDLE_VALUE;

        //Success
        return TRUE;
    }
    
    //Allocate a first class heap
    flags->HeapPointer = rpmalloc_heap_acquire();

    //Ignore remaining flags, zero/sync can be user optional

    //Return value greater than 0
    return flags->HeapPointer;
}


HEAP_METHOD_EXPORT ERRNO heapDestroy(LPVOID heap)
{
    //Destroy the heap
    if ((int)heap == GLOBAL_HEAP_HANDLE_VALUE) 
    {
        //Gloal heap, do nothing, and allow the entrypoint cleanup
        return TRUE;
    }

    //Free all before destroy
    rpmalloc_heap_free_all(heap);

    //Destroy the heap
    rpmalloc_heap_release(heap);

    return TRUE;
}


HEAP_METHOD_EXPORT LPVOID heapAlloc(LPVOID heap, size_t elements, size_t alignment, BOOL zero)
{
    //Multiply for element size
    size_t size = elements * alignment;

    //Check for global heap
    if ((int)heap == GLOBAL_HEAP_HANDLE_VALUE)
    {          
        /*
        * When called from the dotnet CLR the thread may not call the DLL
        * thread attach method, so we need to check and initialze the heap
        * for the current thread
        */
        GLOBAL_HEAP_INIT_CHECK

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


HEAP_METHOD_EXPORT LPVOID heapRealloc(LPVOID heap, LPVOID block, size_t elements, size_t alignment, BOOL zero)
{
    //Multiply for element size
    size_t size = elements * alignment;

    //Check for global heap
    if ((int)heap == GLOBAL_HEAP_HANDLE_VALUE)
    {
        /*
        * When called from the dotnet CLR the thread may not call the DLL
        * thread attach method, so we need to check and initialze the heap
        * for the current thread
        */
        GLOBAL_HEAP_INIT_CHECK

        //Calloc
        return rprealloc(block, size);
    }
    else
    {
        //First class heap, lock is held by caller
        return rpmalloc_heap_realloc(heap, block, size, 0);
    }
}


HEAP_METHOD_EXPORT ERRNO heapFree(LPVOID heap, LPVOID block)
{
    //Check for global heap
    if ((int)heap == GLOBAL_HEAP_HANDLE_VALUE)
    {
        /*
        * If free happens on a different thread, we must allocate the heap
        * its cheap to check 
        */

        GLOBAL_HEAP_INIT_CHECK

        //free block
        rpfree(block);
    }
    else
    {
        //First class heap, lock is held by caller
        rpmalloc_heap_free(heap, block);
    }

    return TRUE;
}