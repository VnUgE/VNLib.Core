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
 * principles. Used for hashing key material into the 32-bit hash
 * that identifies a cache item key.
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
vnlib_fn_internal uint64_t wtlHash(cspan_t data, uint64_t seed);

/*
* Computes a 32-bit hash derived from wtlHash, convenient for
* hash table bucketing and item identification.
*
* @param data Read-only span over the key bytes
* @param seed 64-bit seed for hash domain separation
* @return 32-bit hash value
*/
vnlib_fn_internal uint32_t wtlHash32(cspan_t data, uint64_t seed);

#endif /* !VN_WTLFU_HASH_H */
