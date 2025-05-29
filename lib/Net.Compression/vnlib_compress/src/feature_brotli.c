/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_brotli.c
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

#include <brotli/encode.h>
#include "feature_brotli.h"

#define validateCompState(state) \
	CHECK_NULL_PTR(state); \
	if (!state->compressor) return ERR_BR_INVALID_STATE; \

/*
* Stream memory management functions
*/
static void* _brAllocCallback(void* opaque, size_t size)
{
	(void)sizeof(opaque);
	return vnmalloc(size, 1);
}

static void _brFreeCallback(void* opaque, void* address)
{
	(void)sizeof(opaque);
	
	/*Brotli may pass a null address to the free callback*/
	if (address)
	{
		vnfree(address);
	}
}


int BrAllocCompressor(comp_state_t* state)
{
	BrotliEncoderState* comp;

	DEBUG_ASSERT2(state != NULL, "Expected non-null compressor state argument");

	/*
	* Never allow no compression, it is not supported by the br encoder
	*/
	
	if (state->level == COMP_LEVEL_NO_COMPRESSION)
	{
		return ERR_COMP_LEVEL_NOT_SUPPORTED;
	}
	
	comp = BrotliEncoderCreateInstance(
		&_brAllocCallback, 
		&_brFreeCallback,
		NULL
	);

	if (!comp)
	{
		return ERR_OUT_OF_MEMORY;
	}
	
	state->compressor = comp;

	/*
	* Setting parameters will only return false if the parameter type is 
	* invalid, or the compressor state is not valid
	* 
	* Setup some defaults
	*/
	
	BrotliEncoderSetParameter(comp, BROTLI_PARAM_MODE, BROTLI_MODE_GENERIC);
	BrotliEncoderSetParameter(comp, BROTLI_PARAM_LGWIN, BR_DEFAULT_WINDOW);
	
	/*
	* Capture the block size as a size hint if it is greater than 0
	*/
	if (state->blockSize > 0)
	{
		BrotliEncoderSetParameter(comp, BROTLI_PARAM_SIZE_HINT, state->blockSize);
	}

	/*
	* Setup compressor quality level based on the requested compression level
	*/
	
	switch (state->level)
	{

	case COMP_LEVEL_FASTEST:
		BrotliEncoderSetParameter(comp, BROTLI_PARAM_QUALITY, BR_COMP_LEVEL_FASTEST);
		break;

	case COMP_LEVEL_OPTIMAL:
		BrotliEncoderSetParameter(comp, BROTLI_PARAM_QUALITY, BR_COMP_LEVEL_OPTIMAL);
		break;

	case COMP_LEVEL_SMALLEST_SIZE:
		BrotliEncoderSetParameter(comp, BROTLI_PARAM_QUALITY, BR_COMP_LEVEL_SMALLEST_SIZE);
		break;

	case COMP_LEVEL_NO_COMPRESSION:
	default:
		BrotliEncoderSetParameter(comp, BROTLI_PARAM_QUALITY, BR_COMP_LEVEL_DEFAULT);
		break;
	}

	return VNCMP_SUCCESS;
}

void BrFreeCompressor(comp_state_t* state)
{
	DEBUG_ASSERT2(state != NULL, "Expected non-null state parameter");	

	/*
	* Free the compressor instance if it exists
	*/
	if (state->compressor)
	{
		BrotliEncoderDestroyInstance((BrotliEncoderState*)state->compressor);
		state->compressor = NULL;
	}
}

int BrCompressBlock(_In_ const comp_state_t* state, CompressionOperation* operation)
{
	BrotliEncoderOperation brOperation;
	BROTLI_BOOL brResult;

	size_t availableIn, availableOut, totalOut;
	const uint8_t* nextIn;
	uint8_t* nextOut;

	DEBUG_ASSERT2(operation != NULL, "Expected non-null operation parameter");

	/* Validate inputs */
	validateCompState(state)

	/* Clear the result read / written fields */
	operation->bytesRead = 0;
	operation->bytesWritten = 0;

	/*
	* If the input is empty a flush is not requested, they we are waiting for 
	* more input and this was just an empty call. Should be a no-op
	*/

	if (operation->bytesInLength == 0 && operation->flush < 1)
	{	
		return VNCMP_SUCCESS;
	}

	/*
	* Determine the operation to perform
	*/

	if (operation->flush) 
	{
		brOperation = BROTLI_OPERATION_FINISH;
	}
	else
	{
		brOperation = BROTLI_OPERATION_PROCESS;
	}

	/*
	* Update lengths and data pointers from input/output spans
	* for stream variables
	*/

	availableIn = operation->bytesInLength;
	availableOut = operation->bytesOutLength;
	nextIn = operation->bytesIn;
	nextOut = operation->bytesOut;
	

	/*
	* Compress block as stream and store the result 
	* directly on the result output to pass back to the caller
	*/
	
	brResult = BrotliEncoderCompressStream(
		state->compressor,
		brOperation,
		&availableIn,
		&nextIn,
		&availableOut,
		&nextOut,
		&totalOut
	);
	
	/*
	* check for possible overflow and retrun error
	*/
	if (availableIn > operation->bytesInLength || availableOut > operation->bytesOutLength)
	{
		return ERR_COMPRESSION_FAILED;
	}

	/*
	* Regardless of the operation success we should return the
	* results to the caller. Br encoder sets the number of
	* bytes remaining in the input/output spans
	*/
	operation->bytesRead = operation->bytesInLength - (uint32_t)availableIn;
	operation->bytesWritten = operation->bytesOutLength - (uint32_t)availableOut;

	return brResult;
}


int64_t BrGetCompressedSize(_In_ const comp_state_t* state, uint64_t length, int32_t flush)
{
	size_t size;

	(void)sizeof(flush);
	/*
	* When the flush flag is set, the caller is requesting the
	* entire size of the compressed data, which can include metadata
	*/

	validateCompState(state)

	if (length <= 0)
	{
		return 0;
	}

	size = BrotliEncoderMaxCompressedSize(length);

	if (size > INT64_MAX) 
	{
		return ERR_OVERFLOW;
	}

	return (int64_t)size;
}