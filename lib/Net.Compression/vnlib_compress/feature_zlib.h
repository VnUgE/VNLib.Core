/*
* Copyright (c) 2023 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_zlib.h
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

#pragma once

#ifndef ZLIB_STUB_H_
#define ZLIB_STUB_H_

#include "compression.h"

#define ERR_GZ_INVALID_STATE -16
#define ERR_GZ_OVERFLOW -17

/* Allow user to define their own memory level value */
#ifndef GZ_DEFAULT_MEM_LEVEL
#define GZ_DEFAULT_MEM_LEVEL 8
#endif

/* Specifies the window value to enable GZIP */
#define GZ_ENABLE_GZIP_WINDOW 15 + 16
#define GZ_ENABLE_RAW_DEFLATE_WINDOW -15


int DeflateAllocCompressor(CompressorState* state);

int DeflateFreeCompressor(CompressorState* state);

int DeflateCompressBlock(CompressorState* state, CompressionOperation* operation);

int DeflateGetCompressedSize(CompressorState* state, int length, int flush);

#endif 
