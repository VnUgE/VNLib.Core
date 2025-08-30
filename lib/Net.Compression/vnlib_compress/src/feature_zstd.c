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

#define ZSTD_STATIC_LINKING_ONLY 1

#include <zstd.h>
#include "feature_zstd.h"

#define STREAM_FLAG_FINISHED 0x01

struct zstd_stream_state
{
	ZSTD_CStream* stream;
	int flags;
};


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

int ZstdAllocCompressor(comp_state_t* state)
{
	int compLevel, ret;
	struct zstd_stream_state* streamState;
	size_t result;

	DEBUG_ASSERT2(state, "Expected non-null state parameter");

	/*
	* Allocate the private stream state structure from dynamic 
	* memory.
	*/
	streamState = (struct zstd_stream_state*)_stateMemAlloc(state, sizeof(struct zstd_stream_state));
	if(!streamState)
	{
		return ERR_OUT_OF_MEMORY;
	}

	streamState->stream = NULL;
	streamState->flags = 0;

	/* Store the stream wrapper struct in the compressor state */
	state->compressor = streamState;

	ZSTD_customMem customMem = {
		.customAlloc = state->allocFunc,
		.customFree = state->freeFunc,
		.opaque = state->memOpaque
	};

	/* Create compression stream with custom allocator */
	streamState->stream = ZSTD_createCStream_advanced(customMem);
	if (!streamState->stream)
	{
		ret = ERR_OUT_OF_MEMORY;
		goto Error;
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
	result = ZSTD_initCStream(streamState->stream, compLevel);

	if (ZSTD_isError(result))
	{
		ret = ERR_ZSTD_COMPRESSION_FAILED;
		goto Error;
	}

	/* Set suggested block size */
	state->blockSize = (uint32_t)ZSTD_CStreamInSize();

	return VNCMP_SUCCESS;

Error:
	
	/* If we failed to initialize the compressor, free the stream state */
	ZstdFreeCompressor(state);

	return ret;
}

void ZstdFreeCompressor(comp_state_t* state)
{
	DEBUG_ASSERT2(state != NULL, "Expected non-null compressor state");

	if (state && state->compressor) 
	{
		struct zstd_stream_state* streamState = (struct zstd_stream_state*)state->compressor;

		if (streamState->stream)
		{
			ZSTD_freeCStream(streamState->stream);
			streamState->stream = NULL;
		}

		/* Free the stream state */
		_stateMemFree(state, state->compressor);
		state->compressor = NULL;
	}	
}

int ZstdCompressBlock(_In_ const comp_state_t* state, CompressionOperation* operation)
{
	size_t result;
	struct zstd_stream_state* streamState;

	DEBUG_ASSERT2(state != NULL, "Expected non-null compressor state");
	DEBUG_ASSERT2(state->compressor != NULL, "Expected non-null compressor structure pointer");
	DEBUG_ASSERT2(operation != NULL, "Expected non-null operation parameter");

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

	streamState = (struct zstd_stream_state*)state->compressor;

	/*
	* If a previous operation to finish the stream was successful,
	* but produced output, this call is to confirm the stream successfully
	* finished and no more data is expected.
	* 
	* The flag is set when the compressor successfully finishes a stream
	* and has no more data to process.
	*/
	if (operation->flush && (streamState->flags & STREAM_FLAG_FINISHED))
	{
		return VNCMP_SUCCESS;
	}

	/* Setup input buffer */
	ZSTD_inBuffer input = {
		.src = operation->bytesIn,
		.size = operation->bytesInLength,
		.pos = 0
	};

	/* Setup output buffer */
	ZSTD_outBuffer output = {
		.dst = operation->bytesOut,
		.size = operation->bytesOutLength,
		.pos = 0
	};

	/* Regular compression */
	result = ZSTD_compressStream2(
		streamState->stream,
		&output, 
		&input, 
		operation->flush ? ZSTD_e_end : ZSTD_e_continue
	);

	/* Check for errors */
	if (ZSTD_isError(result))
	{
		return ERR_ZSTD_COMPRESSION_FAILED;
	}

	/*
	* check for possible overflow and retrun error
	*/
	if (input.pos > operation->bytesInLength || output.pos > operation->bytesOutLength)
	{
		return ERR_COMPRESSION_FAILED;
	}

	/* Update bytes read and written */
	operation->bytesRead = (uint32_t)input.pos;
	operation->bytesWritten = (uint32_t)output.pos;

	/*
	* The encoder will return 0 when the stream is finished and 
	* has no more data to flush to the output buffer. The finish
	* flag may be set to indicate that further calls to flush 
	* will not produce any more data. See above. 
	*/
	if (operation->flush && result == 0)
	{
		streamState->flags |= STREAM_FLAG_FINISHED;
	}	

	/* Return success or bytes remaining (for flush operations) */
	return (int)result;
}

int64_t ZstdGetCompressedSize(_In_ const comp_state_t* state, uint64_t length, int32_t flush)
{
	size_t compressedSize;
	
	DEBUG_ASSERT2(state != NULL, "Expected non-null compressor state");

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

