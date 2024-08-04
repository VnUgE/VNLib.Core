/*
* Copyright (c) 2024 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_zlib.c
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
* Include the stub header and also the zlib header
*/


#include <zlib.h>
#include "feature_zlib.h"
#include "util.h"

#define validateCompState(state) \
	if (!state) return ERR_INVALID_PTR; \
	if (!state->compressor) return ERR_GZ_INVALID_STATE; \

/*
* Stream memory management functions
*/
static void* _gzAllocCallback(void* opaque, uint32_t items, uint32_t size)
{
	(void)opaque;
	return vnmalloc(items, size);
}

static void _gzFreeCallback(void* opaque, void* address)
{
	(void)opaque;
	vnfree(address);
}

int DeflateAllocCompressor(CompressorState* state)
{	
	int result, compLevel;
	z_stream* stream;

	assert(state);

	/*
	* Allocate the z-stream state on the heap so we can
	* store it in the compressor state
	*/
	stream = (z_stream*)vncalloc(1, sizeof(z_stream));

	if (!stream) 
	{
		return ERR_OUT_OF_MEMORY;
	}

	stream->zalloc = &_gzAllocCallback;
	stream->zfree = &_gzFreeCallback;
	stream->opaque = Z_NULL;

	/*
	* Initialize the z-stream state with the 
	* desired compression level
	*/


	switch (state->level)
	{
	case COMP_LEVEL_NO_COMPRESSION:
		compLevel = Z_NO_COMPRESSION;
		break;

	case COMP_LEVEL_FASTEST:
		compLevel = Z_BEST_SPEED;
		break;

	case COMP_LEVEL_OPTIMAL:
		compLevel = Z_BEST_COMPRESSION;
		break;

	case COMP_LEVEL_SMALLEST_SIZE:
		compLevel = Z_BEST_COMPRESSION;
		break;

	/*
	Default compression level
	*/
	default:
		compLevel = Z_DEFAULT_COMPRESSION;
		break;
	}

	/*
	* If gzip is enabled, we need to configure the deflatenit2, with 
	* the max window size to 16 
	*/

	if(state->type & COMP_TYPE_GZIP)
	{
		result = deflateInit2(
			stream, 
			compLevel,
			Z_DEFLATED, 
			GZ_ENABLE_GZIP_WINDOW, 
			GZ_DEFAULT_MEM_LEVEL, 
			Z_DEFAULT_STRATEGY
		);
	}
	else
	{
		/* Enable raw deflate */
		result = deflateInit2(
			stream,
			compLevel,
			Z_DEFLATED,
			GZ_ENABLE_RAW_DEFLATE_WINDOW,
			GZ_DEFAULT_MEM_LEVEL,
			Z_DEFAULT_STRATEGY
		);
	}

	/*
	* Inspect the result of the initialization,
	* of the init failed, free the stream and return
	* the error code
	*/

	if (result != Z_OK)
	{
		vnfree(stream);
		return result;
	}

	/*
	* Assign the z-stream state to the compressor state, all done!
	*/
	state->compressor = stream;
	return TRUE;
}

int DeflateFreeCompressor(CompressorState* state)
{
	int result;

	assert(state);

	/*
	* Free the z-stream state, only if the compressor is initialized
	*/
	if (state->compressor) 
	{
		/*
		* Attempt to end the deflate stream, and store the status code
		*/

		result = deflateEnd(state->compressor);

		/*
		* We can always free the z-stream state, even if the deflate
		* stream failed to end.
		*/

		vnfree(state->compressor);
		state->compressor = NULL;

		/*
		* A data error is acceptable when calling end in this library
		* since at that point all resources have been cleaned, and zlib 
		* is simply returning a warning that the stream was not properly
		* terminated.
		* 
		* We assum that calls to free are meant to clean up resources regarless 
		* of their status
		*/
		return result == Z_OK || result == Z_DATA_ERROR;
	}

	return TRUE;
}

int DeflateCompressBlock(const CompressorState* state, CompressionOperation* operation)
{
	z_stream* stream;
	int result;

	validateCompState(state)
	
	/* Clear the result read/written fields */
	operation->bytesRead = 0;
	operation->bytesWritten = 0;

	/*
	* If the input is empty a flush is not requested, they we are waiting for
	* more input and this was just an empty call. Should be a no-op
	*/

	if (operation->bytesInLength == 0 && operation->flush < 1)
	{
		return TRUE;
	}

	stream = (z_stream*)state->compressor;

	/*
	* Overwrite the stream state with the operation parameters from
	* this next compression operation.
	*
	* The caller stores the stream positions in its application memory.
	*/
	stream->avail_in = operation->bytesInLength;
	stream->next_in = (Bytef*)operation->bytesIn;

	stream->avail_out = operation->bytesOutLength;
	stream->next_out = (Bytef*)operation->bytesOut;

	/*
	* In this library we only use the flush flag as a boolean value. 
	* Callers only set the flush flag when the operation has completed 
	* and the compressor is expected to flush its internal buffers. 
	* (aka finish)
	*/

	result = deflate(stream, operation->flush ? Z_FINISH : Z_NO_FLUSH);

	/*
	 * Allways clear stream fields as they are assigned on every call
	 */
	stream->next_in = NULL;
	stream->next_out = NULL;

	/*
	* check for result overflow and return the error code
	*/
	if (stream->avail_in > operation->bytesInLength || stream->avail_out > operation->bytesOutLength)
	{
		return ERR_COMPRESSION_FAILED;
	}

	/*
	* Regardless of the return value, we should always update the
	* the number of bytes read and written.
	* 
	* The result is the number total bytes minus the number of
	* bytes remaining in the stream.
	*/

	operation->bytesRead = operation->bytesInLength - stream->avail_in;
	operation->bytesWritten = operation->bytesOutLength - stream->avail_out;
	
	stream->avail_in = 0;
	stream->avail_out = 0;
	
	return result;
}

int64_t DeflateGetCompressedSize(const CompressorState* state, uint64_t length, int32_t flush)
{
	uint64_t compressedSize;

	/*
	* When the flush flag is set, the caller is requesting the
	* entire size of the compressed data, which can include metadata
	*/

	validateCompState(state)

	if (length <= 0)
	{
		return 0;
	}

	if(flush)
	{
		/*
		* TODO: actualy determine the size of the compressed data
		* when the flush flag is set.
		*/

		compressedSize = deflateBound(state->compressor, length);
	}
	else
	{
		compressedSize = deflateBound(state->compressor, length);
	}

	/* Verify the results to make sure the value doesnt overflow */
	if (compressedSize > INT64_MAX)
	{
		return ERR_GZ_OVERFLOW;
	}

	return (int64_t)compressedSize;
}