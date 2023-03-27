# NativeHeapApi

Contains necessary API header files for user defined heap dlls. Contains the required type definitions, structures, and method signatures to implement (and export) for dll usage that matches the VNLib.Utils.NativeHeap implementation. The managed heap api does a minimal amount of parameter validation, and you may want to verify that your implementation does not require more strict validation.

## Getting started

You may copy the [NativeHeapApi.h](src/NativeHeapApi.h) header file into your project and begin implementing the heap methods defined in the header file.

You must define a constant called **HEAP_METHOD_EXPORT** that defines the method calling convention for proper dll loading for your given platform. On windows, this defaults to `__declspec(dllexport)`. 

When the `heapCreate` method is called, a mutable structure pointer is passed as an argument and expected to be updated by your create method.  The VNLib.Utils library implements two types of heaps, a global/shared heap and "private" or "first class" heaps exposed by the Memory namespace. Consumers are allowed to create a private heap to use at will. 

### UnmanagedHeapFlags structure

`UnmanagedHeapFlags.HeapPointer` - Set your heap pointer that will be passed to all heap methods

`UnmanagedHeapFlags.CreationFlags` - Managed creation flags, that may be read and written. The managed heap implementation will observe the result after the `heapCreate` method returns. 

`UnmanagedHeapFlags.Flags`  - Generic flags passed by the caller directly to the heapCreate method, not observed or modified by the managed library in any way. 

### Example Create
``` c
HEAP_METHOD_EXPORT ERRNO heapCreate(UnmanagedHeapFlags* flags)
{
    //Check flags
    if (flags->CreationFlags & HEAP_CREATION_IS_SHARED)
    {
       //Shared heap may not require synchronization, so we can clear that flag
        flags->CreationFlags &= ~(HEAP_CREATION_SERIALZE_ENABLED);
       
       //Heap structure pointer is required
        flags->HeapPointer = yourSharedHeapPointer;

        //Success
        return 1;
    }
    else
    {
        flags->HeapPointer = yourPrivateHeap;
        return 1;
    }
}
```

## License
The software in this repository is licensed under the GNU GPL version 2.0 (or any later version).
See the LICENSE files for more information.
