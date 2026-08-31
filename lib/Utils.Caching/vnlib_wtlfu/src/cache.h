/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: cache.h
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
* Internal header mostly used to expose internal structures for testing
* purposes. This header is not meant to be referenced anywhere but cache.c
*/

#pragma once

#ifndef VN_WTLFU_CACHE_H
#define VN_WTLFU_CACHE_H

#include "wtlfu.h"
#include "internal.h"

#include "cmsketch.h"
#include "lru.h"
#include "hashtable.h"

#define WTL_NUM_MAX_PERCENT 99

#ifndef WTL_CACHE_LINE
    /* Size in bytes of the target processor cache line size for alignment */
    #define WTL_CACHE_LINE 64		
#endif /* !WTL_CACHE_LINE */

/*
* Load factor threshold for hash table resize (as numerator over 100).
* 75 means resize when count > capacity * 75 / 100.
*/
#ifndef WTL_HASHTABLE_LOAD_FACTOR
    #define WTL_HASHTABLE_LOAD_FACTOR 75ul
#elif WTL_HASHTABLE_LOAD_FACTOR <= 0 || WTL_HASHTABLE_LOAD_FACTOR > 100
    #error Invalid hashtable load factor
#endif // !WTL_HASHTABLE_LOAD_FACTOR

struct wtl_internal_cache_config
{
    /* primary hash function's seed */
    uint64_t	keySeed;

    /* Maximum capacity of the entire cache (also max hash table slots allocated) */
    uint32_t	capacity;
    
    /* The max entry count allowed in the window cache lru */
    uint32_t	windowSize;	

    /* The max entry count allowed in the main cache protected lru */
    uint32_t	protectedSize;

    /* That max entry count allowed in the main cache probationary lru */
    uint32_t	probationSize;
};

struct WtlCtx {

    const struct wtl_internal_cache_config config;

    WtlSketch		sketch;
    WtlHashTable	table;
    WtlLruList		windowCache;

    struct {
        WtlLruList		protected;
        WtlLruList		probation;
    } mainCache;

    // .hash table slot memory
    // .sketch table memory	
};

/*
* Layout of the payload regions placed after the WtlCtx structure
* inside the caller's single memory block. Offsets are absolute from
* the start of the block.
*/
struct wtl_cache_layout
{
    uint64_t slotsOffset;
    uint64_t slotsBytes;
    uint64_t sketchOffset;
    uint64_t sketchBytes;
    uint64_t htRealCapacity;
    uint64_t total;
};

/*
* Rounds value up to the nearest alignment modulus 
*/
static _vn_inline uint64_t _alignUp(uint64_t value, uint64_t align)
{
    return (value + (align - 1)) & ~(align - 1);
}

static _vn_inline uint64_t _pow2RoundUp(uint64_t x)
{
    while ((x & (x - 1))) x++;
    return x;
}

/*
* Computes the size of the hashtable buffer (in bytes) and adds extra capacity for 
* the target load factor.
*/
static _vn_inline uint64_t _htCapacityWithOverhead(uint64_t capacity)
{
    // Add overhead capacity for extra load factor compensation
    capacity += (capacity * (100ull - WTL_HASHTABLE_LOAD_FACTOR)) / 100ull;

    return _pow2RoundUp(capacity);
}

// Internal helpers exposed for testing
vnlib_fn_internal void		wtlConfigResolveInternal(const WtlConfig* src, struct wtl_internal_cache_config* dst);
vnlib_fn_internal void		wtlConfigGetMemoryLayout(const WtlConfig* config, struct wtl_cache_layout* out);
vnlib_fn_internal WtlEntry* wtlFindEntryFromKey(WtlCtx* cache, cspan_t key);
vnlib_fn_internal void		wtlPromoteEntryToProtected(WtlCtx* cache, WtlEntry* entry);
vnlib_fn_internal WtlEntry* wtlPushNewEntry(WtlCtx* cache, WtlEntry* entry);


#endif /* !VN_WTLFU_CACHE_H */
