/*
* Copyright (c) 2023 Vaughn Nugent
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

#ifndef COMPRESSION_H_
#define COMPRESSION_H_

#include "util.h"
#include <stdint.h>
#include <stddef.h>
#include <stdlib.h>

#define ERR_COMP_TYPE_NOT_SUPPORTED -9
#define ERR_COMP_LEVEL_NOT_SUPPORTED -10
#define ERR_INVALID_INPUT_DATA -11
#define ERR_INVALID_OUTPUT_DATA -12

/*
* Enumerated list of supported compression types for user selection
* at runtime.
*/
typedef enum CompressorType
{
	COMP_TYPE_NONE = 0x00,
	COMP_TYPE_GZIP = 0x01,
	COMP_TYPE_DEFLATE = 0x02,
	COMP_TYPE_BROTLI = 0x04,
	COMP_TYPE_LZ4 = 0x08
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
	COMP_LEVEL_OPTIMAL = 0,
	/*
	The compression operation should complete as quickly as possible, even if the
	resulting file is not optimally compressed.
	*/
	COMP_LEVEL_FASTEST = 1,
	/*
		No compression should be performed on the file.
	*/
	COMP_LEVEL_NO_COMPRESSION = 2,
	/*
	The compression operation should create output as small as possible, even if
	the operation takes a longer time to complete.
	*/
	COMP_LEVEL_SMALLEST_SIZE = 3
} CompressionLevel;


typedef enum CompressorStatus {
	COMPRESSOR_STATUS_READY = 0x00,
	COMPRESSOR_STATUS_INITALIZED = 0x01,
	COMPRESSOR_STATUS_NEEDS_FLUSH = 0x02
} CompressorStatus;

typedef struct CompressorStateStruct{	
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
	int blockSize;

	/*
		Pointer to the underlying compressor implementation.
	*/
	void* compressor;

	/*
	 Counts the number of pending bytes since the last successful flush 
	 operation.
	*/
	uint32_t pendingBytes;

} CompressorState;

/*
* An extern caller generated structure passed to calls for 
* stream compression operations.
*/
typedef struct CompressionOperationStruct {

	/*
	* If the operation is a flush operation
	*/
	const int flush;

	/*
	* Input stream data
	*/
	const uint8_t* bytesIn;
	const int bytesInLength;

	/*
	* Output buffer/data stream
	*/
	uint8_t* bytesOut;
	const int bytesOutLength;

	/*
	* Results of the streaming operation
	*/

	int bytesRead;
	int bytesWritten;

} CompressionOperation;

#endif /* !VNLIB_COMPRESS_MAIN_H_ */