/*
* Copyright (c) 2023 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_rpmalloc
* File: vnlib_rpmalloc.c
*
* framework.h is part of vnlib_rpmalloc which is part of the larger
* VNLib collection of libraries and utilities.
*
* vnlib_rpmalloc is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_rpmalloc is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_rpmalloc. If not, see http://www.gnu.org/licenses/.
*/

/*
* Top level linux definitions
*/
#ifdef __GNUC__

#define _GNU_SOURCE 
#define TRUE 1
#define FALSE 0

#endif

#include <NativeHeapApi.h>
#include <rpmalloc.h>

#if defined(_WIN32)

/*
* setup windows api incudes
*/

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    (void)hModule;
    (void)lpReserved;
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

#else

#include <pthread.h>
#include <stdlib.h>
#include <stdint.h>
#include <unistd.h>

//! Set main thread ID (from rpmalloc.c)
extern void rpmalloc_set_main_thread(void);

static pthread_key_t destructor_key;

static void thread_destructor(void*);

static void __attribute__((constructor)) initializer(void) {
    rpmalloc_set_main_thread();
    rpmalloc_initialize();
    pthread_key_create(&destructor_key, thread_destructor);
}

static void __attribute__((destructor)) finalizer(void) {
    rpmalloc_finalize();
}

typedef struct {
    void* (*real_start)(void*);
    void* real_arg;
} thread_starter_arg;

static void* thread_starter(void* argptr) {
    thread_starter_arg* arg = argptr;
    void* (*real_start)(void*) = arg->real_start;
    void* real_arg = arg->real_arg;
    rpmalloc_thread_initialize();
    rpfree(argptr);
    pthread_setspecific(destructor_key, (void*)1);
    return (*real_start)(real_arg);
}

static void thread_destructor(void* value) {
    (void)sizeof(value);
    rpmalloc_thread_finalize(1);
}


#include <dlfcn.h>

int pthread_create(pthread_t* thread,
    const pthread_attr_t* attr,
    void* (*start_routine)(void*),
    void* arg) {
#if defined(__linux__) || defined(__FreeBSD__) || defined(__OpenBSD__) || defined(__NetBSD__) || defined(__DragonFly__) || \
    defined(__APPLE__) || defined(__HAIKU__)
    char fname[] = "pthread_create";
#else
    char fname[] = "_pthread_create";
#endif
    void* real_pthread_create = dlsym(RTLD_NEXT, fname);

    rpmalloc_thread_initialize();
    thread_starter_arg* starter_arg = rpmalloc(sizeof(thread_starter_arg));
    starter_arg->real_start = start_routine;
    starter_arg->real_arg = arg;
    return (*(int (*)(pthread_t*, const pthread_attr_t*, void* (*)(void*), void*))real_pthread_create)(thread, attr, thread_starter, starter_arg);
}

#endif

#define SHARED_HEAP_HANDLE_VALUE ((HeapHandle)1)
#define GLOBAL_HEAP_INIT_CHECK if (!rpmalloc_is_thread_initialized()) { rpmalloc_thread_initialize(); }

//Define the heap methods

HEAP_METHOD_EXPORT HeapHandle HEAP_METHOD_CC heapGetSharedHeapHandle(void)
{
    //Return the shared heap pointer
	return SHARED_HEAP_HANDLE_VALUE;
}

HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapCreate(UnmanagedHeapDescriptor* flags)
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

    //Allocate a first class heap
    flags->HeapPointer = rpmalloc_heap_acquire();

    //Ignore remaining flags, zero/sync can be user optional

    //Return value greater than 0
    return flags->HeapPointer;
}


HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapDestroy(HeapHandle heap)
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


HEAP_METHOD_EXPORT void* HEAP_METHOD_CC heapAlloc(HeapHandle heap, size_t elements, size_t alignment, int zero)
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


HEAP_METHOD_EXPORT void* HEAP_METHOD_CC heapRealloc(HeapHandle heap, void* block, size_t elements, size_t alignment, int zero)
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


HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapFree(HeapHandle heap, void* block)
{
    //Check for global heap
    if (heap == SHARED_HEAP_HANDLE_VALUE)
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

    return (ERRNO)TRUE;
}