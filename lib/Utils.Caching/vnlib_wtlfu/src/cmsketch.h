/*
* Copyright (c) 2026 Vaughn Nugent
*
* Library: VNLib
* Package: vnlib_wtlfu
* File: cmsketch.h
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
 * cmsketch.h - Count-Min Sketch with periodic aging for vnlib_wtlfu.
 *
 * A Count-Min Sketch (CMS) is a probabilistic data structure that
 * estimates the frequency of items using multiple independent hash
 * functions and a 2D array of counters. The estimate for any hash
 * is the minimum across all rows.
 *
 * Aging: when the total number of recorded accesses reaches
 * resetThreshold, all counters are halved (right-shifted by 1) and
 * the access counter is reset. This keeps frequency estimates fresh
 * and prevents long-lived items from permanently dominating.
 *
 * Counters are uint8_t (0-255). The aging strategy keeps values
 * well within range for typical workloads.
 */

#pragma once

#ifndef VN_WTLFU_CMSKETCH_H
#define VN_WTLFU_CMSKETCH_H

#include <stdint.h>
#include <stddef.h>
#include "platform.h"
#include "span.h"

#define WTL_SKETCH_DEFAULT_WIDTH        1024u
#define WTL_SKETCH_DEFAULT_DEPTH        4u
#define WTL_SKETCH_DEFAULT_RESET_MULT   10u

/*
* The configured maximum depth of the sketch table. wtlSketchIsValid()
* rejects configs whose depth exceeds this value.
*/
#define WTL_SKETCH_MAX_DEPTH 8u

/*
* Base seed for sketch sub-hashes. Each row uses seed + row index.
* This is a compile-time constant for the default configuration.
*/
#define WTL_SKETCH_BASE_SEED 0x9e3779b97f4a7c15ULL


/*
* Configuration for creating a Count-Min Sketch.
*/
typedef struct wtl_sketch_config_struct
{
    /* Number of columns (buckets per row). More = higher resolution. */
    uint32_t width;

    /* Number of rows. More = fewer collisions. */
    uint32_t depth;

    /*
    * Total access count at which aging is triggered (all counters
    * halved, access counter reset). Must be greater than 0; generally
    * desired to be around 10x width.
    */
    uint32_t resetThreshold;

    /* Base seed for hash domain separation. Each row uses seed + row index. */
    uint64_t seed;
} WtlSketchConfig;

typedef struct WtlSketch {

    /* configuration copy: width, depth, resetThreshold, seed. */
    WtlSketchConfig config;

    /*
     * Number of calls to wtlSketchRecord since the last aging.
     * When this reaches config.resetThreshold, all counters are
     * halved and this field is reset to zero.
     */
    uint32_t accessCount;

    /*
    * Span pointing to the the memory containing the sketch table
    */
    span_t table;

} WtlSketch;

/*
* Validates a caller-initialized sketch before it is used. Because the
* sketch is caller-allocated, the caller owns layout correctness: the
* table span must point at exactly config.width * config.depth bytes of
* writable memory. Checks, in order:
*
*   0   Valid: config fields are in range and the table size matches
*       config.width * config.depth exactly.
*  -1   Invalid config: zero width, zero depth, depth greater than
*       WTL_SKETCH_MAX_DEPTH, zero resetThreshold, or empty table span.
*  -2   Overflow: config.width * config.depth exceeds UINT32_MAX.
*  -3   Table size mismatch: table span size differs from the configured
*       width * depth.
*
* Passing a NULL sketch is undefined behavior (asserts in debug builds).
*
* @param sketch  Pointer to the sketch structure to validate
* @return 0 on success, or a negative error code as listed above
*/
vnlib_fn_internal int wtlSketchIsValid(const WtlSketch* sketch);

/*
* Records an access for the given unique hash by incrementing the counter
* in each row at position identified by the hash. If the total
* access count reaches resetThreshold, aging is triggered (all
* counters halved, counter reset).
*
* @param sketch  Sketch handle
* @param hash    32-bit, non-zero hash of the item
*/
vnlib_fn_internal void wtlSketchRecord(WtlSketch* sketch, uint32_t hash);

/*
* Estimates the frequency of the given hash. Returns the minimum
* counter value across all rows. This is an upper bound on the
* true frequency (Count-Min Sketch never underestimates).
*
* @param sketch  Sketch handle
* @param hash    32-bit, non-zero hash of the item
* @return Estimated frequency (0 if never recorded)
*/
vnlib_fn_internal uint32_t wtlSketchEstimate(const WtlSketch* sketch, uint32_t hash);

/*
* Manually triggers aging: halves all counters and resets the
* access counter. Normally automatic, but exposed for testing
* and observability.
*
* @param sketch  Sketch handle
*/
vnlib_fn_internal void wtlSketchAge(WtlSketch* sketch);

/*
* Manually restores all internal counters for entire sketch to 0
* 
* @param sketch  Sketch handle
*/
vnlib_fn_internal void wtlSketchReset(WtlSketch* sketch);

#endif /* !VN_WTLFU_CMSKETCH_H */
