/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: hash.h
*
* This library is free software; you can redistribute it and/or
* modify it under the terms of the GNU Lesser General Public License
* as published by the Free Software Foundation; either version 2.1
* of the License, or (at your option) any later version.
*
* This library is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
* Lesser General Public License for more details.
*
* You should have received a copy of the GNU Lesser General Public License
* along with vnlib_wtlfu. If not, see http://www.gnu.org/licenses/.
*/

/*
* hash.h - Internal hash function interface for vnlib_wtlfu.
*
* Provides a fast non-cryptographic hash function based on xxhash3
* principles. Used for both the main hash table and the Count-Min
* Sketch sub-hashes (with different seeds per row).
*
* The implementation is in hash.c. Only the interface is exposed here.
*/

#pragma once

#ifndef VN_WTLFU_HASH_H
#define VN_WTLFU_HASH_H

#include <stdint.h>
#include <stddef.h>
#include "platform.h"
#include "span.h"

/*
* Computes a 64-bit hash of the supplied key material using a
* xxhash3-style algorithm with the specified seed.
*
* @param data Read-only span over the key bytes
* @param seed 64-bit seed for hash domain separation
* @return 64-bit hash value
*/
_VN_WTLFU_INTERNAL uint64_t wtlfuHash(cspan_t data, uint64_t seed);

/*
* Computes a 32-bit hash derived from wtlfuHash, convenient for
* hash table bucketing and sketch indexing.
*
* @param data Read-only span over the key bytes
* @param seed 64-bit seed for hash domain separation
* @return 32-bit hash value
*/
_VN_WTLFU_INTERNAL uint32_t wtlfuHash32(cspan_t data, uint64_t seed);

#endif /* !VN_WTLFU_HASH_H */
