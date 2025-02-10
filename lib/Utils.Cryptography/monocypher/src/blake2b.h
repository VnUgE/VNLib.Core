/*
* Copyright (c) 2024 Vaughn Nugent
*
* vnlib_monocypher is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* vnlib_monocypher is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with vnlib_monocypher. If not, see http://www.gnu.org/licenses/.
*/

#pragma once
#ifndef VN_MONOCYPHER_BLAKE2_H
#define VN_MONOCYPHER_BLAKE2_H

#include <stdint.h>
#include "util.h"

#define ERR_HASH_LEN_INVALID -16
#define ERR_KEY_LEN_INVALID -17
#define ERR_KEY_PTR_INVALID -18

#define MC_MAX_HASH_SIZE 64
#define MC_MAX_KEY_SIZE 64

#define BLAKE2B_RESULT_SUCCESS 0

VNLIB_EXPORT uint32_t VNLIB_CC Blake2GetContextSize(void);

VNLIB_EXPORT int32_t VNLIB_CC Blake2Init(void* context, uint32_t hashlen, const void* key, uint32_t keylen);

VNLIB_EXPORT int32_t VNLIB_CC Blake2Update(void* context, const void* data, uint32_t datalen);

VNLIB_EXPORT int32_t VNLIB_CC Blake2Final(void* context, void* hash, uint32_t hashlen);

VNLIB_EXPORT int32_t VNLIB_CC Blake2GetHashSize(void* context);

#endif
