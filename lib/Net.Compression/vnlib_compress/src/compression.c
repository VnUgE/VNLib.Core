/*
* Copyright (c) 2024 Vaughn Nugent
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
#include "util.h"

#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
#include "feature_brotli.h"
#endif /* VNLIB_COMPRESSOR_BROTLI_ENABLED */


#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED 
#include "feature_zlib.h"
#endif /* VNLIB_COMPRESSOR_GZIP_ENABLED */

/*
 Gets the supported compressors, this is defined at compile time and is a convenience method for
 the user to know what compressors are supported at runtime.
*/
VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetSupportedCompressors(void)
{
	/*
	* Supported compressors are defined at compile time
	*/
	CompressorType supported;

	supported = COMP_TYPE_NONE;

#ifdef VNLIB_COMPRESSOR_ZLIB_ENABLED
	supported |= COMP_TYPE_GZIP;
	supported |= COMP_TYPE_DEFLATE;
#endif

#ifdef VNLIB_COMPRESSOR_LZ4_ENABLED
	supported |= COMP_TYPE_LZ4;
#endif

#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
	supported |= COMP_TYPE_BROTLI;
#endif

	return supported;
}

VNLIB_COMPRESS_EXPORT CompressorType VNLIB_COMPRESS_CC GetCompressorType(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return ((CompressorState*)compressor)->type;
}

VNLIB_COMPRESS_EXPORT CompressionLevel VNLIB_COMPRESS_CC GetCompressorLevel(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return ((CompressorState*)compressor)->level;
}

VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressorBlockSize(_In_ const void* compressor)
{
	CHECK_NULL_PTR(compressor)
	return (int64_t)((CompressorState*)compressor)->blockSize;
}

VNLIB_COMPRESS_EXPORT void* VNLIB_COMPRESS_CC AllocateCompressor(CompressorType type, CompressionLevel level)
{
	int result;
	CompressorState* state;

	/* Validate input arguments */
	if (level < 0 || level > 9)
	{
		return (void*)ERR_COMP_LEVEL_NOT_SUPPORTED;
	}

	state = (CompressorState*)vncalloc(1, sizeof(CompressorState));

	if (!state)
	{
		return (void*)ERR_OUT_OF_MEMORY;
	}

	/* Configure the comp state */
	state->type = type;
	state->level = level;
	
	result = ERR_COMP_TYPE_NOT_SUPPORTED;

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

		/*
		* Unsupported compressor type allow error to propagate
		*/
		case COMP_TYPE_LZ4:
		case COMP_TYPE_NONE:
		default:
			break;
	}
	

	/*
		If result was successfull return the context pointer, if
		the creation failed, free the state if it was allocated
		and return the error code.
	*/

	if (result > 0)
	{
		return (void*)state;
	}
	else
	{
		vnfree(state);

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
}

VNLIB_COMPRESS_EXPORT int VNLIB_COMPRESS_CC FreeCompressor(void* compressor)
{	
	CompressorState* comp;
	int errorCode;

	CHECK_NULL_PTR(compressor)
	
	comp = (CompressorState*)compressor;
	errorCode = TRUE;

	switch (comp->type)
	{
		case COMP_TYPE_BROTLI:
#ifdef VNLIB_COMPRESSOR_BROTLI_ENABLED
			BrFreeCompressor(comp);
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
			errorCode = DeflateFreeCompressor(comp);
#endif
			break;		


		/*
		* If compression type is none, there is nothing to do
		* since its not technically an error, so just return
		* true.
		*/
		case COMP_TYPE_NONE:
		case COMP_TYPE_LZ4:
		default:			
			break;		
	}

	/*
	* Free the compressor state
	*/

	vnfree(comp);
	return errorCode;
}

VNLIB_COMPRESS_EXPORT int64_t VNLIB_COMPRESS_CC GetCompressedSize(_In_ const void* compressor, uint64_t inputLength, int32_t flush)
{
	CompressorState* comp;
	int64_t result;

	CHECK_NULL_PTR(compressor)

	if (inputLength > INT64_MAX)
	{
		return ERR_OVERFLOW;
	}

	comp = (CompressorState*)compressor;
	result = ERR_COMP_TYPE_NOT_SUPPORTED;

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

	/*
	* Set the result as an error code, since the compressor
	* type is not supported.
	*/
	case COMP_TYPE_NONE:
	case COMP_TYPE_LZ4:
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
	int result;
	CompressorState* comp;

	comp = (CompressorState*)compressor;

	/*
	* Validate input arguments
	*/

	CHECK_NULL_PTR(comp)
	CHECK_NULL_PTR(operation)

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

	case COMP_TYPE_NONE:
	case COMP_TYPE_LZ4:
		break;
	}
	
	return result;
}
