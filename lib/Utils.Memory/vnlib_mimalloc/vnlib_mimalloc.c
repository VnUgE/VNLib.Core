
#include <NativeHeapApi.h>
#include <mimalloc.h>

#if defined(_WIN32) || defined(_WIN64)

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <windows.h>

#else

#include <stddef.h>
#define TRUE 1
#define FALSE 0

#endif


#define SHARED_HEAP_HANDLE_VALUE ((HeapHandle)1)

HEAP_METHOD_EXPORT HeapHandle HEAP_METHOD_CC heapGetSharedHeapHandle(void)
{
    //Return the shared heap pointer
    return SHARED_HEAP_HANDLE_VALUE;
}

HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapCreate(UnmanagedHeapDescriptor* flags)
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


HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapDestroy(HeapHandle heap)
{
    //Destroy the heap if not shared heap
    if (heap != SHARED_HEAP_HANDLE_VALUE)
    {
        mi_heap_delete(heap);
    }

    return (ERRNO)TRUE;
}


HEAP_METHOD_EXPORT void* HEAP_METHOD_CC heapAlloc(HeapHandle heap, size_t elements, size_t alignment, int zero)
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


HEAP_METHOD_EXPORT void* HEAP_METHOD_CC heapRealloc(HeapHandle heap, void* block, size_t elements, size_t alignment, int zero)
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


HEAP_METHOD_EXPORT ERRNO HEAP_METHOD_CC heapFree(HeapHandle heap, void* block)
{
    (void)heap;
    mi_free(block);
    return (ERRNO)TRUE;
}
