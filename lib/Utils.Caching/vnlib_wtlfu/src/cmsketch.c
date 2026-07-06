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
 * Stores a 2D array of uint8_t counters in a single allocation. Each
 * row is indexed by a 32-bit hash derived from the key and a row-specific
 * seed. Frequency estimates return the minimum counter across all rows.
 * Counters saturate at 255 and are periodically halved to age old data.
 */

#include <stdint.h>
#include <stddef.h>
#include <string.h>

#include "platform.h"
#include "debug.h"
#include "span.h"
#include "hash.h"
#include "wtlfu.h"
#include "cmsketch.h"

#if SIZE_MAX < UINT32_MAX
    #error "This library does not support sizeof(size_t) smaller than 32bits"
#endif // SIZE_MAX < UINT32_MAX

static _vn_inline void* wtlfuAlloc(const WtlAllocator* al, size_t size, size_t alignment)
{
    DEBUG_ASSERT(al);
    return al->Alloc(al->ctx, size, alignment);
}

static _vn_inline void wtlfuFree(const WtlAllocator* al, void* ptr, size_t size)
{
    DEBUG_ASSERT(al);
    al->Free(al->ctx, ptr, size);
}

 /* Opaque sketch handle */
struct WtlSketch {
    
    /*
    * Cached allocated pointer used to allocate this structure and the counter
    * table.
    */
    const WtlAllocator* allocator;

    /* configuration copy: width, depth, resetThreshold, seed. */
    WtlSketchConfig config;

    /*
     * Number of calls to wtlfuSketchRecord since the last aging.
     * When this reaches config.resetThreshold, all counters are
     * halved and this field is reset to zero.
     */
    uint32_t accessCount;

    /*
     * Flat row-major counter table. Row r, column c is stored at
     * index r * config.width + c. table.size stores the total
     * number of counters (config.width * config.depth).
     */
    span_t table;
};

static _vn_inline uint32_t sketchGetKeyIndex(const WtlSketch* sketch, cspan_t key, uint32_t row)
{
    DEBUG_ASSERT(sketch);

    // a unique row seed adds entropy to the row hash
    uint64_t rowSeed = sketch->config.seed + row;
    uint32_t column = wtlfuHash32(key, rowSeed) % sketch->config.width;
    
    return row * sketch->config.width + column;
}

_VN_WTLFU_INTERNAL WtlSketch* wtlfuSketchCreate(const WtlSketchConfig* config, const WtlAllocator* allocator)
{
    WtlSketch* sketch;
    uint64_t counterTableSize;

    DEBUG_ASSERT(config);
    DEBUG_ASSERT(allocator);

    // Validate arguments before touching the allocator.
    if (!config || !allocator)
    {
        return NULL;
    }

    // Width and depth must both be non-zero. Depth is also capped at a
    // small value because each additional row requires another hash pass.
    if (
        config->width == 0 || 
        config->depth == 0 || 
        config->depth > WTL_SKETCH_MAX_DEPTH ||
        config->resetThreshold == 0
    )
    {
        return NULL;
    }

    // Guard against overflow when computing the total table size.
    counterTableSize = (uint64_t)config->width * (uint64_t)config->depth;
    if (counterTableSize > UINT32_MAX)
    {
        return NULL;
    }   

    // Allocate the sketch state structure.
    sketch = (WtlSketch*)wtlfuAlloc(allocator, sizeof(WtlSketch), 0);
    if (!sketch)
    {
        DEBUG_ASSERT2(0, "Failed to allocate memory for the sketch structure");
        return NULL;
    }

    memset(sketch, 0, sizeof(WtlSketch));

    // Copy configuration and allocator pointer
    sketch->config = *config; 
    sketch->allocator = allocator;

    // Allocate the flattened counter table as a separate block.
    {
        uint8_t* counterTable = (uint8_t*)wtlfuAlloc(sketch->allocator, counterTableSize, sizeof(uint8_t));
        if (!counterTable)
        {
            DEBUG_ASSERT2(0, "Failed to allocate memory for the counter table");
            // Free sketch table
            wtlfuFree(allocator, sketch, sizeof(WtlSketch));
            return NULL;
        }

        memset(counterTable, 0, counterTableSize);

        spanInit(&sketch->table, counterTable, (uint32_t)counterTableSize);
    }

    return sketch;
}


_VN_WTLFU_INTERNAL void wtlfuSketchDestroy(WtlSketch* sketch)
{
    const WtlAllocator* allocator;

    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch); 
    if (!sketch || !sketch->allocator)
    {
        return;
    }

    allocator = sketch->allocator;

    // free structures
    wtlfuFree(allocator, sketch->table.data, spanGetSize(sketch->table));
    wtlfuFree(allocator, sketch, sizeof(WtlSketch));
}

_VN_WTLFU_INTERNAL void wtlfuSketchRecord(WtlSketch* sketch, cspan_t key)
{ 
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    DEBUG_ASSERT(!spanIsNull(sketch->table));

    // An empty key is valid; it simply hashes the empty byte sequence.

    // Increment one counter per row. Each row uses a different seed so
    // that collisions in one row are unlikely to repeat in another.
    for (uint32_t row = 0; row < sketch->config.depth; row++)
    {
        uint32_t     index = sketchGetKeyIndex(sketch, key, row);
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
        wtlfuSketchAge(sketch);
    }
}


_VN_WTLFU_INTERNAL uint32_t wtlfuSketchEstimate(const WtlSketch* sketch, cspan_t key)
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
        uint32_t index = sketchGetKeyIndex(sketch, key, row);
        uint8_t  value = *spanGetOffset(sketch->table, index);       

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
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    // Halve every counter. Integer division naturally rounds down,
    // which is the desired exponential decay behavior.
    for (uint32_t i = 0; i < sketch->table.size; i++)
    {
        (*spanGetOffset(sketch->table, i)) >>= 1;
    }

    // Reset the access counter so the next aging cycle starts fresh.
    sketch->accessCount = 0;
}

_VN_WTLFU_INTERNAL void wtlfuSketchReset(WtlSketch* sketch)
{
    // Passing null internal sketch structure is a bug, should alert developers
    DEBUG_ASSERT(sketch);
    if (!sketch)
    {
        return;
    }

    // Clear the table and access count
    memset(sketch->table.data, 0, sketch->table.size);
    sketch->accessCount = 0;
}
