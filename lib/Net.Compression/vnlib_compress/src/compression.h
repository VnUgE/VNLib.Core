/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: compression.h
*
* vnlib_compress is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_compress is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_compress. If not, see http://www.gnu.org/licenses/.
*/


/*
* Implementation notes:
* 
* This library is designed to be a wrapper around the various compression libraries
* used for dynamic HTTP compression. Is is designed to be exported as a DLL or a shared
* library and written in portable C code. 
* 
* Compressors are standalone instances created by callers and used to perform compression
* operations. Compressors are created, used, and destroyed by callers. This library is meant 
* to unify compression to a single api. The goal is performance and portability, so it can 
* be easily used by portable runtimes.
*/

#pragma once

#ifndef _VNCMP_COMPRESSION_H_
#define _VNCMP_COMPRESSION_H_

#include <stdint.h>
#include <stddef.h>
#include "platform.h"


/*Set api export calling convention(allow used to override)*/
#ifndef VNLIB_COMPRESS_CC
	#ifdef _VNCMP_IS_WINDOWS
		/*STD for importing to other languages such as.NET*/
		#define VNLIB_COMPRESS_CC __stdcall
	#else
		#define VNLIB_COMPRESS_CC 
	#endif
#endif /* !VNLIB_CC */

#ifndef VNLIB_COMPRESS_EXPORT	/*Allow users to disable the export/impoty macro if using source code directly*/
	#ifdef VNLIB_COMPRESS_EXPORTING
		#ifdef _VNCMP_IS_WINDOWS
			#define VNLIB_COMPRESS_EXPORT __declspec(dllexport)
		#else
			#define VNLIB_COMPRESS_EXPORT __attribute__((visibility("default")))
		#endif /* IS_WINDOWS */
	#else
		#ifdef _VNCMP_IS_WINDOWS
			#define VNLIB_COMPRESS_EXPORT __declspec(dllimport)
		#else
			#define VNLIB_COMPRESS_EXPORT extern
		#endif /* IS_WINDOWS */
	#endif /* !VNLIB_EXPORTING */
#endif /* !VNLIB_EXPORT */

#ifndef _In_
	#define _In_
#endif

#define VNCMP_SUCCESS					1

/*
* ERRORS AND CONSTANTS
*/
#define ERR_INVALID_PTR					-1
#define ERR_OUT_OF_MEMORY				-2
#define ERR_OUT_OF_BOUNDS				-3
#define ERR_INVALID_ARGUMENT			-4

#define ERR_COMP_TYPE_NOT_SUPPORTED		-9
#define ERR_COMP_LEVEL_NOT_SUPPORTED	-10
#define ERR_INVALID_INPUT_DATA			-11
#define ERR_INVALID_OUTPUT_DATA			-12
#define ERR_COMPRESSION_FAILED			-13
#define ERR_OVERFLOW					-14

#define CHECK_NULL_PTR(ptr) if(!ptr) return ERR_INVALID_PTR;
#define CHECK_ARG_RANGE(x, min, max) if(x < min || x > max) return ERR_OUT_OF_BOUNDS;

/*
* Enumerated list of supported compression types for user selection
* at runtime.
* 
* Must match VNLib.Net.Http.Compression.CompressionMethod.cs 
*/
typedef enum CompressorType
{
	COMP_TYPE_NONE			= 0x00,
	COMP_TYPE_GZIP			= 0x01,
	COMP_TYPE_DEFLATE		= 0x02,
	COMP_TYPE_BROTLI		= 0x04,
	COMP_TYPE_ZSTD			= 0x08
} CompressorType;


/*
	Specifies values that indicate whether a compression operation emphasizes speed
	or compression size.
*/
typedef enum CompressionLevel
{
	/*
	The compression operation should be optimally compressed, even if the operation
	takes a longer time to complete.
	*/
	COMP_LEVEL_OPTIMAL			= 0,
	/*
	The compression operation should complete as quickly as possible, even if the
	resulting file is not optimally compressed.
	*/
	COMP_LEVEL_FASTEST			= 1,
	/*
		No compression should be performed on the file.
	*/
	COMP_LEVEL_NO_COMPRESSION	= 2,
	/*
	The compression operation should create output as small as possible, even if
	the operation takes a longer time to complete.
	*/
	COMP_LEVEL_SMALLEST_SIZE	= 3
} CompressionLevel;

typedef void* (*vnlib_mem_alloc) (void* opaque, size_t size);
typedef void  (*vnlib_mem_free) (void* opaque, void* address);

typedef struct _vn_cmp_state_struct {	

	/*
	  Pointer to the underlying compressor implementation.
	*/
	void* compressor;

	/* 
		Opaque pointer for custom memory allocation 
	*/
	void* memOpaque;

	/*
	  Memory allocation function for the compressor.
    */
	vnlib_mem_alloc allocFunc;

	/*
		Memory deallocation function for the compressor.
	*/
	vnlib_mem_free freeFunc;

	/*
		Indicates the type of underlying compressor.
	*/
	CompressorType type;

	/*
	   The user specified compression level, the underlying compressor will decide 
	   how to handle this value.
	*/
	CompressionLevel level;

	/*
		Indicates the suggested block size for the underlying compressor.
	*/
	uint32_t blockSize;  

} comp_state_t;

/*
* An extern caller generated structure passed to calls for 
* stream compression operations.
*/
typedef struct CompressionOperationStruct {

	/*
	 * Input stream data
	 */
	const void* bytesIn;
	/*
	 * Output buffer/data stream
	 */
	void* bytesOut;

	/*
	* If the operation is a flush operation
	*/
	int32_t flush;

	uint32_t bytesInLength;	
	uint32_t bytesOutLength;

	/*
	* Results of the streaming operation
	*/

	uint32_t bytesRead;
	uint32_t bytesWritten;

} CompressionOperation;


static _vncmp_inline void* _stateMemAlloc(const comp_state_t* state, size_t size)
{
	DEBUG_ASSERT2(state != NULL, "Expected non-null state parameter");
	DEBUG_ASSERT2(state->allocFunc != NULL, "Expected non-null allocFunc pointer");

	if (!state || !state->allocFunc || size == 0)
	{
		return NULL; // Return NULL for invalid parameters
	}

	return state->allocFunc(state->memOpaque, size);
}

static _vncmp_inline void _stateMemFree(const comp_state_t* state, void* ptr)
{
	DEBUG_ASSERT2(state != NULL, "Expected non-null state parameter");
	DEBUG_ASSERT2(state->freeFunc != NULL, "Expected non-null freeFunc pointer");

	if (state && state->freeFunc && ptr)
	{
		state->freeFunc(state->memOpaque, ptr);
	}
}

/*
* Public API functions
*/
VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetSupportedCompressors(void);

/*
* Returns the suggested block size for the underlying compressor.
* 
* @param compressor A pointer to the desired compressor instance to query.
* @return The suggested block size for the underlying compressor in bytes
*/
VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressorBlockSize(_In_ const void* compressor);

/*
* Gets the compressor type of the specified compressor instance.
* 
* @param compressor A pointer to the desired compressor instance to query.
* @return The type of the specified compressor instance.
*/
VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetCompressorType(_In_ const void* compressor);

/*
* Gets the compression level of the specified compressor instance.
* 
* @param compressor A pointer to the desired compressor instance to query.
* @return The compression level of the specified compressor instance.
*/
VNLIB_COMPRESS_EXPORT CompressionLevel VNLIB_COMPRESS_CC GetCompressorLevel(_In_ const void* compressor);

/*
* OBSOLETE
* 
* Allocates a new compressor instance on the native heap of the desired compressor type.
* Calling this function performs an implicit allocation of a compression state instance
* and the desired compressor instance. The returned pointer is a pointer to the state
* instance. The state instance must be freed using FreeCompressor when it is no longer
* needed.
* 
* This function is considered obsolete. New applications should allocate a reusable state 
* instance using CompressionAllocState or CompressionAllocState2. Then allocate a compressor instance
* using CompressionAllocCompressor.
* 
* @param type The desired compressor type.
* @param level The desired compression level.
* @return A pointer to the newly allocated compressor instance. NULL if the compressor 
could not be allocated.
*/
VNLIB_COMPRESS_EXPORT void* VNLIB_COMPRESS_CC AllocateCompressor(CompressorType type, CompressionLevel level);

/*
* OBSOLETE
* 
* This function is considered obsolete. New applications should free the state instance 
* using CompressionFreeCompressor and the state instance using CompressionFreeState. You 
* are encouraged to reuse the state instance to reduce memory allocations.
* 
* Frees a previously allocated compressor instance from AllocateCompressor. This will also
* free the underlying compression state instance. The pointer passed to this function 
* should not be used after this call.
* 
* @param compressor A pointer to the desired compressor instance to free.
* @return The underlying compressor's native return code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC FreeCompressor(void* compressor);

/*
* Computes the maximum compressed size of the specified input data. This is not supported
 for all compression types.
* 
* @param compressor A pointer to the initialized compressor instance to use.
* @param inputLength The length of the input data in bytes.
* @return The maximum compressed size of the specified input data in bytes.
*/
VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressedSize(
	_In_ const void* compressor, 
	uint64_t inputLength, 
	int32_t flush
);


/*
* Perform compression operation using the specified compressor instance.
* 
* @param compressor A pointer to the initialized compressor instance to use.
* @param operation A pointer to the compression operation structure
* @return The underlying compressor's native return code
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressBlock(
	_In_ const void* compressor, 
	CompressionOperation* operation
);

/* 
*	V2 API
* 
* Separates the compressor instance from the state instance. and allows state instance 
* to be reused with multiple compressors.
*/

/*
* Allocates a new compression state instance on the default heap. Note that a compressor
* must still be allocated using CompressionAllocCompressor before compression operations can
* be performed.
* 
* @param statePtr A pointer to a pointer that will receive the newly allocated state instance.
* @return VNCMP_SUCCESS if the state was successfully allocated, otherwise an error code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocState(void** statePtr);


/*
* Allocates a new compression state instance using the specified memory allocation callback functions. Note that a compressor
* must still be allocated using CompressionAllocCompressor before compression operations can
* be performed.
* 
* @param statePtr A pointer to a pointer that will receive the newly allocated state instance.
* @param alloc A pointer to a memory allocation function that matches the vnlib_mem_alloc signature
* @param free A pointer to a memory free function that matches the vnlib_mem_free signature
* @param opaque An opaque pointer that will be passed to the alloc/free functions when called.
* @return VNCMP_SUCCESS if the state was successfully allocated, otherwise an error code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocState2(void** statePtr, vnlib_mem_alloc alloc, vnlib_mem_free free, void* opaque);

/*
* Frees a previously allocated compression state instance. This will also free any allocated
* compressor instance associated with the state, if it was not already freed.
* 
* @param statePtr A pointer to the state instance to free.
* @return VNCMP_SUCCESS if the state was successfully freed, otherwise an error code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionFreeState(void* statePtr);

/*
* Allocates a new compressor instance of the specified type and compression level using
* the specified state instance. The state instance must have been previously allocated
* using CompressionAllocState or CompressionAllocState2.
* 
* @param statePtr A pointer to the state instance to use for allocation.
* @param type The desired compressor type.
* @param level The desired compression level.
* @return VNCMP_SUCCESS if the compressor was successfully allocated, otherwise an error code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocCompressor(void* statePtr, CompressorType type, CompressionLevel level);

/*
* Frees a previously allocated compressor instance associated with the specified state instance.
* The state instance itself is not freed. The state may be reused to allocate another compressor
* instance after this call.
* 
* This function is safe to call even if no compressor instance is allocated, in which case
* the function will simply return VNCMP_SUCCESS.
* 
* @param statePtr A pointer to the state instance whose compressor is to be freed.
* @return VNCMP_SUCCESS if the compressor was successfully freed, otherwise an error code.
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionFreeCompressor(void* statePtr);

#endif /* !VNLIB_COMPRESS_MAIN_H_ */