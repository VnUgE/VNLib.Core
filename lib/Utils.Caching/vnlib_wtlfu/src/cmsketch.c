/*
 * Copyright (c) 2026 Vaughn Nugent
 *
 * Library: VNLib
 * Package: vnlib_wtlfu
 * File: cmsketch.c
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
 * cmsketch.c - Count-Min Sketch implementation for vnlib_wtlfu.
 *
 * Stores a 2D array of uint8_t counters in a single caller-owned
 * buffer: the WtlSketch header immediately followed by the flat
 * row-major counter table. Each row is indexed by a sub-hash derived
 * from a caller-supplied 32-bit hash and a row-specific seed. The
 * sub-hash expands the 32-bit hash and row seed to 64 bits and runs a
 * splitmix64-style avalanche (derived from the public-domain splitmix64
 * function by Sebastiano Vigna, 2015) so each output bit depends on
 * every input bit. Frequency estimates return the minimum counter
 * across all rows. Counters saturate at 255 and are periodically
 * halved to age old data.
 */

#include <stdint.h>
#include <stddef.h>
#include <string.h>

#include "platform.h"
#include "debug.h"
#include "span.h"
#include "cmsketch.h"

#if SIZE_MAX < UINT32_MAX
    #error "This library does not support sizeof(size_t) smaller than 32bits"
#endif // SIZE_MAX < UINT32_MAX

/*
* Mixing constants from splitmix64 (Sebastiano Vigna, 2015, CC0).
* Reference: http://vigna.di.unimi.it/ftp/papers/SplitMix12.pdf
*/
#define SPLIT_MIX_CONST_1 0x9E3779B97F4A7C15ULL
#define SPLIT_MIX_CONST_2 0xBF58476D1CE4E5B9ULL
#define SPLIT_MIX_CONST_3 0x94D049BB133111EBULL

/*
* _splitMix32 - expand an item hash plus a row seed into one
* avalanche-mixed 32-bit value.
*
* Packs both 32-bit inputs into a 64-bit word, runs one splitmix64
* step, and xor-folds back to 32 bits. The 64-bit pipeline is a
* bijection before the fold, so every output bit depends on every
* input bit, which decorrelates the per-row sub-hashes from each
* other and from the base hash.
*/
static _vn_inline uint32_t _splitMix32(uint32_t in1, uint32_t in2)
{
    uint64_t x = ((uint64_t)in1 << 32 | (uint64_t)in2);

    x += SPLIT_MIX_CONST_1;
    x = (x ^ (x >> 30)) * SPLIT_MIX_CONST_2;
    x = (x ^ (x >> 27)) * SPLIT_MIX_CONST_3;
    x = x ^ (x >> 31);

    // Fold the 64-bit mixed value down to 32 bits for row/column use.
    return (uint32_t)(x ^ (x >> 32));
}

static _vn_inline uint32_t _sketchGetHashIndex(const WtlSketch* sketch, uint32_t hash, uint32_t row)
{
    uint32_t colMix, column;

    // Derive this row's column from the item hash and a per-row seed.
    colMix = _splitMix32(hash, (uint32_t)(sketch->config.seed + row));
    column = colMix % sketch->config.width;

    return row * sketch->config.width + column;
}

_VN_WTLFU_INTERNAL int wtlSketchIsValid(const WtlSketch* sketch)
{  
    uint64_t counterTableSize = 0;
    DEBUG_ASSERT(sketch);

    // Check for zero sizes
    if (
        sketch->config.width == 0 ||
        sketch->config.depth == 0 ||
        sketch->config.depth > WTL_SKETCH_MAX_DEPTH ||
        sketch->config.resetThreshold == 0 ||
        spanGetSize(sketch->table) == 0
    )
    {
        return -1;
    }

    // Width and depth must both be non-zero. Depth is also capped at a
    // small value because each additional row requires another hash pass.
    counterTableSize = (uint64_t)sketch->config.width * (uint64_t)sketch->config.depth;

    // Guard against overflow when computing the total table size.
    if (counterTableSize > UINT32_MAX)
    {
        return -2;
    }

    // Config table parameters do not match table size
    if (counterTableSize != spanGetSize(sketch->table))
    {
        return -3;
    }   

    return 0;
}

_VN_WTLFU_INTERNAL void wtlSketchRecord(WtlSketch* sketch, uint32_t hash)
{ 
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    // Increment one counter per row. Each row uses a different seed so
    // that collisions in one row are unlikely to repeat in another.
    for (uint32_t row = 0; row < sketch->config.depth; row++)
    {
        uint32_t     index = _sketchGetHashIndex(sketch, hash, row);
        uint8_t*  valuePtr = spanGetOffset(sketch->table, index);

        // Saturate at 255 rather than wrapping back to zero.
        if (*valuePtr < UINT8_MAX)
        {
            (*valuePtr)++;
        }
    }

    // Record total recorded accesses for periodic aging.
    sketch->accessCount++;

    // When the threshold is reached, halve every counter to decay old
    // popularity and reset the access counter.
    if (sketch->accessCount >= sketch->config.resetThreshold)
    {
        wtlSketchAge(sketch);
    }
}

_VN_WTLFU_INTERNAL uint32_t wtlSketchEstimate(const WtlSketch* sketch, uint32_t hash)
{
    // Seed the minimum with the first row's counter. The table is
    // non-empty because create() rejects zero width/depth.
    uint32_t min = UINT8_MAX;    

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return 0;
    }   

    // Read one counter per row and keep the smallest value. Collisions
    // can only inflate counters, so the minimum is the conservative
    // (least overestimated) frequency estimate.
    for (uint32_t row = 0; row < sketch->config.depth; row++)
    {        
        uint32_t index = _sketchGetHashIndex(sketch, hash, row);
        uint8_t  value = *spanGetOffset(sketch->table, index);

        if (value < min)
        {
            min = value;

            // Early exit if we hit zero: no row can produce a lower
            // estimate, so the hash has effectively never been seen.
            if (min == 0)
            {
                break;
            }
        }
    }

    return min;
}

_VN_WTLFU_INTERNAL void wtlSketchAge(WtlSketch* sketch)
{
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    // Halve every counter. Integer division naturally rounds down,
    // which is the desired exponential decay behavior.
    for (uint32_t i = 0; i < spanGetSize(sketch->table); i++)
    {
        (*spanGetOffset(sketch->table, i)) >>= 1;
    }

    // Reset the access counter so the next aging cycle starts fresh.
    sketch->accessCount = 0;
}

_VN_WTLFU_INTERNAL void wtlSketchReset(WtlSketch* sketch)
{
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    // Clear the table and access count
    memset(spanGetOffset(sketch->table, 0), 0, spanGetSize(sketch->table));
    sketch->accessCount = 0;
}
