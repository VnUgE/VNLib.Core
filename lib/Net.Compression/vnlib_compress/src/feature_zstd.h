/*
* Copyright (c) 2025 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_zstd.h
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

#ifndef ZSTD_STUB_H_
#define ZSTD_STUB_H_

#include "compression.h"

#define ERR_ZSTD_INVALID_STATE -18
#define ERR_ZSTD_COMPRESSION_FAILED -19

int ZstdAllocCompressor(comp_state_t* state);

void ZstdFreeCompressor(comp_state_t* state);

int ZstdCompressBlock(_In_ const comp_state_t* state, CompressionOperation* operation);

int64_t ZstdGetCompressedSize(_In_ const comp_state_t* state, uint64_t length, int32_t flush);

#endif // ZSTD_STUB_H_
