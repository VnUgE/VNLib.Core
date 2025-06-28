/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_zstd.c
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

#include <zstd.h>
#include "feature_zstd.h"

#define ZSTD_CLEVEL_DEFAULT 10

#define ERR_ZSTD_INVALID_STATE -18
#define ERR_ZSTD_COMPRESSION_FAILED -19

#define validateCompState(state) \
	if (!state) return ERR_INVALID_PTR; \
	if (!state->compressor) return ERR_ZSTD_INVALID_STATE;

/*
* Custom memory allocation functions for ZSTD
*/
static void* _zstdAllocCallback(void* opaque, size_t size)
{
	(void)sizeof(opaque);
	return vnmalloc(1, size);
}

static void _zstdFreeCallback(void* opaque, void* address)
{
	(void)sizeof(opaque);
	vnfree(address);
}

int ZstdAllocCompressor(comp_state_t* state)
{
	int compLevel;
	ZSTD_CStream* cstream;
	size_t result;

	DEBUG_ASSERT2(state, "Expected non-null state parameter");

	ZSTD_customMem customMem = {
		.customAlloc = &_zstdAllocCallback,
		.customFree = &_zstdFreeCallback,
		.opaque = state
	};

	/* Create compression stream with custom allocator */
	cstream = ZSTD_createCStream_advanced(customMem);

	if (!cstream)
	{
		return ERR_OUT_OF_MEMORY;
	}

	/* Map compression levels to ZSTD levels */
	switch (state->level)
	{
	case COMP_LEVEL_NO_COMPRESSION:
		compLevel = 1;  /* ZSTD minimum level */
		break;

	case COMP_LEVEL_FASTEST:
		compLevel = 1;  /* Fastest compression */
		break;

	case COMP_LEVEL_OPTIMAL:
		compLevel = 6;  /* Balanced performance/compression */
		break;

	case COMP_LEVEL_SMALLEST_SIZE:
		compLevel = ZSTD_maxCLevel();  /* Maximum compression */
		break;

	default:
		compLevel = ZSTD_CLEVEL_DEFAULT;  /* Default level */
		break;
	}

	/* Initialize the compression stream */
	result = ZSTD_initCStream(cstream, compLevel);

	if (ZSTD_isError(result))
	{
		ZSTD_freeCStream(cstream);
		return ERR_COMPRESSION_FAILED;
	}

	/* Set suggested block size */
	state->blockSize = (uint32_t)ZSTD_CStreamInSize();

	/* Store the stream in the compressor state */
	state->compressor = cstream;

	return VNCMP_SUCCESS;
}

int ZstdFreeCompressor(comp_state_t* state)
{
	DEBUG_ASSERT2(state != NULL, "Expected non-null compressor state");

	if (state->compressor)
	{
		ZSTD_freeCStream((ZSTD_CStream*)state->compressor);
		state->compressor = NULL;
	}

	return VNCMP_SUCCESS;
}

int ZstdCompressBlock(_In_ const comp_state_t* state, CompressionOperation* operation)
{
	ZSTD_CStream* cstream;
	ZSTD_inBuffer input;
	ZSTD_outBuffer output;
	size_t result;

	validateCompState(state)

	/* Clear the result read/written fields */
	operation->bytesRead = 0;
	operation->bytesWritten = 0;

	/*
	* If the input is empty and flush is not requested, this is a no-op
	*/
	if (operation->bytesInLength == 0 && operation->flush < 1)
	{
		return VNCMP_SUCCESS;
	}

	cstream = (ZSTD_CStream*)state->compressor;

	/* Setup input buffer */
	input.src = operation->bytesIn;
	input.size = operation->bytesInLength;
	input.pos = 0;

	/* Setup output buffer */
	output.dst = operation->bytesOut;
	output.size = operation->bytesOutLength;
	output.pos = 0;

	/* Perform compression operation */
	if (operation->flush)
	{
		/* Flush/finish the stream */
		result = ZSTD_endStream(cstream, &output);
	}
	else
	{
		/* Regular compression */
		result = ZSTD_compressStream(cstream, &output, &input);
	}

	/* Check for errors */
	if (ZSTD_isError(result))
	{
		return ERR_ZSTD_COMPRESSION_FAILED;
	}

	/* Update bytes read and written */
	operation->bytesRead = (uint32_t)input.pos;
	operation->bytesWritten = (uint32_t)output.pos;

	/* Return success or bytes remaining (for flush operations) */
	return (int)result;
}

int64_t ZstdGetCompressedSize(_In_ const comp_state_t* state, uint64_t length, int32_t flush)
{
	size_t compressedSize;
	
	validateCompState(state)

	if (length <= 0)
	{
		return 0;
	}

	/* Get the maximum compressed size bound */
	compressedSize = ZSTD_compressBound(length);

	if (flush)
	{
		/* Add some extra space for flush operations (frame end) */
		compressedSize += ZSTD_CStreamOutSize();
	}

	/* Verify the results to make sure the value doesn't overflow */
	if (compressedSize > INT64_MAX)
	{
		return ERR_OVERFLOW;
	}

	return (int64_t)compressedSize;
}

