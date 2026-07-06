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
* functions and a 2D array of counters. The estimate for any key
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

#ifndef _VN_WTLFU_CMSKETCH_H
#define _VN_WTLFU_CMSKETCH_H

#include <stdint.h>
#include <stddef.h>
#include "platform.h"
#include "span.h"
#include "wtlfu.h"

#define WTL_SKETCH_DEFAULT_WIDTH        1024u
#define WTL_SKETCH_DEFAULT_DEPTH        4u
#define WTL_SKETCH_DEFAULT_RESET_MULT   10u

/*
* The configured maximum depth of the sketch table. sketchCreate() validation
* prevents table depth larger > this value
*/
#define WTL_SKETCH_MAX_DEPTH 8u

/*
* Base seed for sketch sub-hashes. Each row uses seed + row index.
* This is a compile-time constant for the default configuration.
*/
#define WTL_SKETCH_BASE_SEED 0x9e3779b97f4a7c15ULL

/* Opaque sketch handle */
typedef struct WtlSketch WtlSketch;

/*
* Configuration for creating a Count-Min Sketch.
*/
typedef struct wtl_sketch_config_struct
{
    /* Number of columns (buckets per row). More = higher resolution. */
    uint32_t width;

    /* Number of rows (independent hash functions). More = fewer collisions. */
    uint32_t depth;

    /*
    * Total access count at which aging is triggered (all counters
    * halved, access counter reset). Must be greater than 0 generally desired
    * to be around 10x width.
    */
    uint32_t resetThreshold;

    /* Base seed for hash domain separation. Each row uses seed + row index. */
    uint64_t seed;
} WtlSketchConfig;

/*
* Creates a new sketch. All memory is allocated through the supplied
* allocator. Returns NULL on failure.
*
* @param config    Sketch configuration (width, depth, resetThreshold, seed)
* @param allocator  Allocator handle (retained, caller keeps it alive)
* @return New sketch handle, or NULL on failure
*/
_VN_WTLFU_INTERNAL WtlSketch* wtlfuSketchCreate(
    const WtlSketchConfig* config,
    const WtlAllocator* allocator
);

/*
 * Destroys the sketch and frees all memory through the allocator.
 *
 * @param sketch  Sketch to destroy (may be NULL, no-op)
 */
_VN_WTLFU_INTERNAL void wtlfuSketchDestroy(WtlSketch* sketch);

/*
* Records an access for the given key by incrementing the counter
* in each row at position hash(key, seed+row) % width. If the total
* access count reaches resetThreshold, aging is triggered (all
* counters halved, counter reset).
*
* @param sketch  Sketch handle
* @param key     Read-only span over the key bytes
*/
_VN_WTLFU_INTERNAL void wtlfuSketchRecord(WtlSketch* sketch, cspan_t key);

/*
* Estimates the frequency of the given key. Returns the minimum
* counter value across all rows. This is an upper bound on the
* true frequency (Count-Min Sketch never underestimates).
*
* @param sketch  Sketch handle
* @param key     Read-only span over the key bytes
* @return Estimated frequency (0 if never recorded)
*/
_VN_WTLFU_INTERNAL uint32_t wtlfuSketchEstimate(const WtlSketch* sketch, cspan_t key);

/*
* Manually triggers aging: halves all counters and resets the
* access counter. Normally automatic, but exposed for testing
* and observability.
*
* @param sketch  Sketch handle
*/
_VN_WTLFU_INTERNAL void wtlfuSketchAge(WtlSketch* sketch);

/*
* Manually restores all internal counters for entire sketch to 0
* 
* @param sketch  Sketch handle
*/
_VN_WTLFU_INTERNAL void wtlfuSketchReset(WtlSketch* sketch);

#endif /* !_VN_WTLFU_CMSKETCH_H */
