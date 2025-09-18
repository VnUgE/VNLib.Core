/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: compression.c
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
* Notes:
* This api is desgined to be friendly to many types of callers
* without needing to worry about the platform integer size. I would
* like to return errors as negative values, and I am requiring block 
* operations on block sizes under INT64_MAX. This allows 64bit support
* while allowing negative error codes as return values. I think 
* this is a good compromise.
*/

#define VNLIB_COMPRESS_EXPORTING 1

#include "compression.h"

#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
	#include "feature_brotli.h"
#endif /* VNLIB_COMPRESSOR_BROTLI_ENABLED */

#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED 
	#include "feature_zlib.h"
#endif /* VNLIB_COMPRESSOR_ZLIB_ENABLED */

#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
	#include "feature_zstd.h"
#endif /* VNLIB_COMPRESSOR_ZSTD_ENABLED */

#include <string.h>


/*
* If the native heap is desired, pull the header in and specify the extern
* symbol for the heap api functions, since it should be linked statically
*/
#ifdef VNLIB_CUSTOM_MALLOC_ENABLE	
	/* Since static linking is enabled, heapapi must have extern symbol not dllimport */
	#define VNLIB_HEAP_API extern	
	#include <NativeHeapApi.h>
#else
	/* If not using the NativeHeapApi, use the standard malloc/calloc/free */
	#include <stdlib.h>
#endif /* NATIVE_HEAP_API */

static void* vnmalloc(void* opaque, size_t num)
{
	(void)sizeof(opaque);

#ifdef NATIVE_HEAP_API	/* Defined in the NativeHeapApi */
	return heapAlloc(heapGetSharedHeapHandle(), num, 1, 0);
#else
	return malloc(num);
#endif
}

static void vnfree(void* opaque, void* ptr)
{
	(void)(sizeof(opaque));

#ifdef NATIVE_HEAP_API	/* Defined in the NativeHeapApi */
	ERRNO result = heapFree(heapGetSharedHeapHandle(), ptr);

	/* track failed free results */
	DEBUG_ASSERT(result != 0);
#else
	free(ptr);
#endif /* NATIVE_HEAP_API */
}


/* Clears/resets the compressor state for reuse */
static _vncmp_inline void _stateClearCompressor(comp_state_t* state)
{
	if (state)
	{
		state->type = COMP_TYPE_NONE;
		state->level = COMP_LEVEL_OPTIMAL;
		state->blockSize = 0;
		state->compressor = NULL;
	}
}

static _vncmp_inline void* _intPtrCast(int result)
{
	/*
	 * Using strict/pedantic error checking int gcc will cause a warning
	 * when casting an int to a void* pointer. We are returning an error code
	* and it is expected behavior
	*/
#ifdef  __GNUC__
#pragma GCC diagnostic push
#pragma GCC diagnostic ignored "-Wint-to-pointer-cast"
		return (void*)result;
#pragma GCC diagnostic pop
#elif defined(_MSC_VER)
#pragma warning(push)
#pragma warning(disable: 4312)
		return (void*)result;
#pragma warning(pop)
#else 
		return (void*)result;
#endif 
}

/*
 Gets the supported compressors, this is defined at compile time and is a convenience method for
 the user to know what compressors are supported at runtime.
*/
VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetSupportedCompressors(void)
{
	/*
	* Supported compressors are defined at compile time
	*/
	CompressorType supported = COMP_TYPE_NONE;

#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
	supported |= COMP_TYPE_GZIP;
	supported |= COMP_TYPE_DEFLATE;
#endif

#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
	supported |= COMP_TYPE_ZSTD;
#endif

#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
	supported |= COMP_TYPE_BROTLI;
#endif

	return supported;
}

VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetCompressorType(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return ((comp_state_t*)compressor)->type;
}

VNLIB_COMPRESS_EXPORT CompressionLevel VNLIB_COMPRESS_CC GetCompressorLevel(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return ((comp_state_t*)compressor)->level;
}

VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressorBlockSize(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return (int64_t)((comp_state_t*)compressor)->blockSize;
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocState(void** statePtr)
{
	return CompressionAllocState2(statePtr, &vnmalloc, &vnfree, NULL);
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocState2(void** statePtr, vnlib_mem_alloc alloc, vnlib_mem_free free, void* opaque)
{
	if (!alloc || !free)
	{
		return ERR_INVALID_ARGUMENT;
	}

	comp_state_t* state = (comp_state_t*)alloc(opaque, sizeof(comp_state_t));
	if (!state)
	{
		return ERR_OUT_OF_MEMORY;
	}

	/* Zero the state memory */
	memset(state, 0, sizeof(comp_state_t));

	state->allocFunc = alloc;
	state->freeFunc = free;
	state->memOpaque = opaque;	
	/* No other state fields need to be initialized yet */

	*statePtr = state;

	return VNCMP_SUCCESS;
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionFreeState(void* statePtr)
{	
	int result = VNCMP_SUCCESS;

	comp_state_t* state = (comp_state_t*)statePtr;

	CHECK_NULL_PTR(state);

	/* Free the compressor if it is still allocated */
	if (state->type != COMP_TYPE_NONE)
	{		
		result = CompressionFreeCompressor(statePtr);
	}
	
	/* Free the state itself */
	_stateMemFree(state, statePtr);

	return result;
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionAllocCompressor(void* statePtr, CompressorType type, CompressionLevel level)
{
	int result = ERR_COMP_TYPE_NOT_SUPPORTED;

	comp_state_t* state = (comp_state_t*)statePtr;

	CHECK_NULL_PTR(statePtr);

	/* Validate input arguments */
	if (level < 0 || level > 9)
	{
		return ERR_COMP_LEVEL_NOT_SUPPORTED;
	}	
	
	/* Check that the compressor isn't already allocated */
	if (state->type != COMP_TYPE_NONE || state->compressor)
	{
		/* Compressor already allocated */
		return ERR_INVALID_ARGUMENT;
	}
	

	/* 
	* The alloc/free functions should be guarunteed if the 
	* user called proper alloc functions, so this is a sanity check 
	*/
	DEBUG_ASSERT2(state->allocFunc != NULL, "Expected non-null allocFunc pointer");
	DEBUG_ASSERT2(state->freeFunc != NULL, "Expected non-null freeFunc pointer");

	/* 
	* Configure the comp state, these changes will need to be 
	* undone if the allocation fails
	*/
	state->type = type;
	state->level = level;

	/*
	* Compressor types are defined at compile time
	* and callers are allowed to choose which to allocate 
	*/

	switch (type)
	{
		case COMP_TYPE_BROTLI:
#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
			result = BrAllocCompressor(state);			
#endif		
			break;

		case COMP_TYPE_DEFLATE:
		case COMP_TYPE_GZIP:
#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
			result = DeflateAllocCompressor(state);			
#endif
			break;

		case COMP_TYPE_ZSTD:
#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
			result = ZstdAllocCompressor(state);			
#endif
			break;

		/*
		* Unsupported compressor type allow error to propagate
		*/
		
		case COMP_TYPE_NONE:
		default:
			break;
	}

	/*
	* If this failed, the state must return to it's initial state
	* so the caller can try again or free the state.
	*/
	if (result != VNCMP_SUCCESS)
	{
		_stateClearCompressor(state);
	}

	return result;
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressionFreeCompressor(void* statePtr)
{	
	int errorCode = VNCMP_SUCCESS;

	comp_state_t* state = (comp_state_t*)statePtr;

	CHECK_NULL_PTR(state);		
	
	switch (state->type)
	{
	case COMP_TYPE_BROTLI:
#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
		BrFreeCompressor(state);
#endif			
		break;

	case COMP_TYPE_DEFLATE:
	case COMP_TYPE_GZIP:
		/*
		* Releasing a deflate compressor will cause a deflate
		* end call, which can fail, we should send the error
		* to the caller and clean up as best we can.
		*/
#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
		errorCode = DeflateFreeCompressor(state);
#endif		
		break;

	case COMP_TYPE_ZSTD:
#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
		ZstdFreeCompressor(state);
#endif
		break;

		/*
		* If compression type is none, there is nothing to do
		* since its not technically an error, so just return
		* true.
		*/

	case COMP_TYPE_NONE:
	default:
		break;
	}

	_stateClearCompressor(state);

	return errorCode;
}


VNLIB_COMPRESS_EXPORT void* VNLIB_COMPRESS_CC AllocateCompressor(CompressorType type, CompressionLevel level)
{
	int result = ERR_COMP_TYPE_NOT_SUPPORTED;

	void* state;

	result = CompressionAllocState(&state);

	/* Validate input arguments */
	if (result != VNCMP_SUCCESS)
	{
		return _intPtrCast(result);
	}

	DEBUG_ASSERT2(state != NULL, "Expected non-null state pointer when successful result code");

	result = CompressionAllocCompressor(state, type, level);
	if (result != VNCMP_SUCCESS)
	{
		CompressionFreeState(state);
		return _intPtrCast(result);
	}

	return state;	
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC FreeCompressor(void* compressor)
{	
	/* Freeing the state will also free the compressor if it's allocated */
	return CompressionFreeState(compressor);
}

VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressedSize(
	_In_ const void* compressor, 
	uint64_t inputLength, 
	int32_t flush
)
{
	const comp_state_t* comp = (const comp_state_t*)compressor;
	int64_t result = ERR_COMP_TYPE_NOT_SUPPORTED;

	CHECK_NULL_PTR(compressor);
	
	if (inputLength > INT64_MAX)
	{
		return ERR_OUT_OF_BOUNDS;
	}
	
	switch (comp->type)
	{

	case COMP_TYPE_BROTLI:
#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
		result = BrGetCompressedSize(comp, inputLength, flush);
#endif
		break;

	case COMP_TYPE_DEFLATE:
	case COMP_TYPE_GZIP:
#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
		result = DeflateGetCompressedSize(comp, inputLength, flush);
#endif
		break;

	case COMP_TYPE_ZSTD:
#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
		result = ZstdGetCompressedSize(comp, inputLength, flush);
#endif
		break;

	/*
	* Set the result as an error code, since the compressor
	* type is not supported.
	*/
	
	case COMP_TYPE_NONE:	
	default:
		break;
	}

	return result;
}

/*
* Compresses the data contained in the operation structure, ingests and compresses
* the data then writes it to the output buffer. The result of the operation is
* returned as an error code. Positive integers indicate success, negative integers
* indicate failure.
* @param compressor 
*/
VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC CompressBlock(_In_ const void* compressor, CompressionOperation* operation)
{
	int result = ERR_INVALID_ARGUMENT;
	const comp_state_t* comp = (const comp_state_t*)compressor;

	/*
	* Validate input arguments
	*/

	CHECK_NULL_PTR(comp);
	CHECK_NULL_PTR(operation);

	/*
	* Validate buffers, if the buffer length is greate than 0
	* it must point to a valid buffer
	*/

	if (operation->bytesInLength > 0 && !operation->bytesIn)
	{
		return ERR_INVALID_INPUT_DATA;
	}

	if (operation->bytesOutLength > 0 && !operation->bytesOut)
	{
		return ERR_INVALID_OUTPUT_DATA;
	}

	/*
	* Determine the compressor type and call the appropriate
	* compression function
	*/

	result = ERR_COMP_TYPE_NOT_SUPPORTED;

	switch (comp->type)
	{
		/* Brolti support */
	case COMP_TYPE_BROTLI:

#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
		result = BrCompressBlock(comp, operation);
#endif
		break;


		/* Deflate support */
	case COMP_TYPE_DEFLATE:
	case COMP_TYPE_GZIP:

#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
		result = DeflateCompressBlock(comp, operation);
#endif
		break;

	case COMP_TYPE_ZSTD:
#ifdef VNLIB_COMPRESSOR_ZSTD_ENABLED
		result = ZstdCompressBlock(comp, operation);
#endif
		break;

	case COMP_TYPE_NONE:
	default:
		break;
	}

	return result;
}
