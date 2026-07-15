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
 * row-major counter table. Each row is indexed by a 32-bit hash derived
 * from the key and a row-specific seed. Frequency estimates return the
 * minimum counter across all rows. Counters saturate at 255 and are
 * periodically halved to age old data.
 */

#include <stdint.h>
#include <stddef.h>
#include <string.h>

#include "platform.h"
#include "debug.h"
#include "span.h"
#include "hash.h"
#include "cmsketch.h"

#if SIZE_MAX < UINT32_MAX
    #error "This library does not support sizeof(size_t) smaller than 32bits"
#endif // SIZE_MAX < UINT32_MAX

/* Opaque sketch handle */
struct WtlSketch { 
   
    /* configuration copy: width, depth, resetThreshold, seed. */
    WtlSketchConfig config;

    /*
     * Number of calls to wtlfuSketchRecord since the last aging.
     * When this reaches config.resetThreshold, all counters are
     * halved and this field is reset to zero.
     */
    uint32_t accessCount;

    /*
    * Caches the entire size of the table
    */
    uint32_t tableSize;

    /*
    * Flat row-major counter table. Row r, column c is stored at
    * index r * config.width + c. tableSize stores the total
    * number of counters (config.width * config.depth). The table
    * bytes immediately follow this struct in the caller-owned buffer.
    */
};

static _vn_inline void sketchGetTable(WtlSketch* sketch, span_t* table)
{
    DEBUG_ASSERT(sketch);    
    spanInit(table, (uint8_t*)(sketch + 1), sketch->tableSize);
}

static _vn_inline void sketchGetTableC(const WtlSketch* sketch, cspan_t* table)
{
    DEBUG_ASSERT(sketch);
    spanInitC(table, (uint8_t*)(sketch + 1), sketch->tableSize);
}


static _vn_inline uint32_t sketchGetKeyIndex(const WtlSketch* sketch, cspan_t key, uint32_t row)
{
    DEBUG_ASSERT(sketch);

    // a unique row seed adds entropy to the row hash
    uint64_t rowSeed = sketch->config.seed + row;
    uint32_t column = wtlfuHash32(key, rowSeed) % sketch->config.width;
    
    return row * sketch->config.width + column;
}

_VN_WTLFU_INTERNAL uint32_t wtlfuSketchGetMemorySize(const WtlSketchConfig* config)
{
    uint64_t counterTableSize = 0;
    DEBUG_ASSERT(config);

    // Width and depth must both be non-zero. Depth is also capped at a
    // small value because each additional row requires another hash pass.
    if (
        config->width == 0 ||
        config->depth == 0 ||
        config->depth > WTL_SKETCH_MAX_DEPTH ||
        config->resetThreshold == 0
    )
    {
        return 0;
    }

    // Guard against overflow when computing the total table size.
    counterTableSize = (uint64_t)config->width * (uint64_t)config->depth;
    if (counterTableSize > UINT32_MAX)
    {
        return 0;
    }

    return (uint32_t)sizeof(WtlSketch) + (uint32_t)counterTableSize;
}

_VN_WTLFU_INTERNAL void wtlfuSketchInit(const WtlSketchConfig* config, WtlSketch* sketch)
{
    uint32_t bufferSize;

    DEBUG_ASSERT(config);
    DEBUG_ASSERT(sketch);

    // Validate arguments before touching the allocator.
    if (!config || !sketch)
    {
        return;
    }

    // Get the structure size, also validates the configuration
    bufferSize = wtlfuSketchGetMemorySize(config);
    if (bufferSize == 0)
    {
        return;
    }

    // Zero's out the entire structure and table
    memset(sketch, 0, bufferSize);

    // Copy the config 
    sketch->config = *config;
    sketch->tableSize = (uint32_t)(bufferSize - sizeof(WtlSketch));
}

_VN_WTLFU_INTERNAL void wtlfuSketchRecord(WtlSketch* sketch, cspan_t key)
{ 
    span_t table;

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    sketchGetTable(sketch, &table);

    // An empty key is valid; it simply hashes the empty byte sequence.

    // Increment one counter per row. Each row uses a different seed so
    // that collisions in one row are unlikely to repeat in another.
    for (uint32_t row = 0; row < sketch->config.depth; row++)
    {
        uint32_t     index = sketchGetKeyIndex(sketch, key, row);
        uint8_t*  valuePtr = spanGetOffset(table, index);

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
        wtlfuSketchAge(sketch);
    }
}

_VN_WTLFU_INTERNAL uint32_t wtlfuSketchEstimate(const WtlSketch* sketch, cspan_t key)
{
    // Seed the minimum with the first row's counter. The table is
    // non-empty because create() rejects zero width/depth.
    uint32_t min = UINT8_MAX;
    cspan_t table;

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return 0;
    }

    sketchGetTableC(sketch, &table);

    // Read one counter per row and keep the smallest value. Collisions
    // can only inflate counters, so the minimum is the conservative
    // (least overestimated) frequency estimate.
    for (uint32_t row = 0; row < sketch->config.depth; row++)
    {        
        uint32_t index = sketchGetKeyIndex(sketch, key, row);
        uint8_t  value = *spanGetOffsetC(table, index);

        if (value < min)
        {
            min = value;

            // Early exit if we hit zero: no row can produce a lower
            // estimate, so the key has effectively never been seen.
            if (min == 0)
            {
                break;
            }
        }
    }

    return min;
}

_VN_WTLFU_INTERNAL void wtlfuSketchAge(WtlSketch* sketch)
{
    span_t table;

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    sketchGetTable(sketch, &table);

    // Halve every counter. Integer division naturally rounds down,
    // which is the desired exponential decay behavior.
    for (uint32_t i = 0; i < spanGetSize(table); i++)
    {
        (*spanGetOffset(table, i)) >>= 1;
    }

    // Reset the access counter so the next aging cycle starts fresh.
    sketch->accessCount = 0;
}

_VN_WTLFU_INTERNAL void wtlfuSketchReset(WtlSketch* sketch)
{
    span_t table;

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    sketchGetTable(sketch, &table);

    // Clear the table and access count
    memset(spanGetOffset(table, 0), 0, spanGetSize(table));
    sketch->accessCount = 0;
}
