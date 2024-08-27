/*
* Copyright (c) 2024 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_compress
* File: feature_brotli.h
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

#ifndef BROTLI_STUB_H_
#define BROTLI_STUB_H_

#include "util.h"
#include "compression.h"

#define ERR_BR_INVALID_STATE -24

#define BR_COMP_LEVEL_FASTEST 1
#define BR_COMP_LEVEL_OPTIMAL 11
#define BR_COMP_LEVEL_SMALLEST_SIZE 9
#define BR_COMP_LEVEL_DEFAULT 5

#define BR_DEFAULT_WINDOW 22

int BrAllocCompressor(_cmp_state_t* state);

void BrFreeCompressor(_cmp_state_t* state);

int BrCompressBlock(_In_ const _cmp_state_t* state, CompressionOperation* operation);

int64_t BrGetCompressedSize(_In_ const _cmp_state_t* state, uint64_t length, int32_t flush);

#endif /* !BROTLI_STUB_H_ */